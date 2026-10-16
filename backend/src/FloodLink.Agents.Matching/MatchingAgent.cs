using System.Diagnostics;
using System.Text.Json.Nodes;
using FloodLink.Contracts;
using FloodLink.Domain.Llm;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Llm;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Matching;

/// <summary>
/// Logistics/Matching Agent (Member B). The LLM proposes allocations from a snapshot of free
/// stock (not reserved, above each depot's reserve floor), following the coordinator's revision
/// notes when there are any. The proposal is checked (<see cref="TryAcceptProposal"/>); when the
/// call fails or any check fails, the greedy matcher allocates instead (first depot with stock,
/// in priority order).
/// Inventory is read-only here: stock is reserved after validation and committed only after
/// a coordinator approves a dispatch.
/// </summary>
public sealed class MatchingAgent(AppDbContext context, ILlmClient? llm = null) : IMatchingAgent
{
    public const string PromptVersion = "matching.v1";
    public const int MaxReasonLength = 200;
    public const int MaxRevisionNotesLength = 1000;
    private const double Epsilon = 1e-6;

    private static readonly Lazy<string> Prompt = new(() => PromptLibrary.Load(typeof(MatchingAgent).Assembly, PromptVersion));

    /// <inheritdoc />
    public async Task<AgentResult<AllocationProposal>> ExecuteAsync(
        TriagePlan triagePlan,
        string? revisionNotes = null,
        CancellationToken cancellationToken = default)
    {
        if (triagePlan.PriorityItems.Count == 0)
            return AgentResult<AllocationProposal>.Fail("EMPTY_TRIAGE_PLAN", "The triage plan contains no needs to match.");

        // Tool: read-only inventory snapshot.
        var sw = Stopwatch.StartNew();
        var stock = await context.InventoryItems
            .AsNoTracking()
            .OrderBy(item => item.DepotId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var toolCalls = new List<ToolCall>
        {
            new()
            {
                Tool = "db.inventory.read",
                Input = new Dictionary<string, object?>
                {
                    ["itemNames"] = triagePlan.PriorityItems.Select(need => need.NeedType).Distinct().ToList()
                },
                Output = new Dictionary<string, object?> { ["stockLines"] = stock.Count },
                Succeeded = true,
                DurationMs = sw.ElapsedMilliseconds
            }
        };

        var (allocations, unfulfillable) =
            await ProposeWithLlmAsync(triagePlan, stock, revisionNotes, toolCalls, cancellationToken)
            ?? Greedy(triagePlan, stock);

        if (allocations.Count == 0)
            return AgentResult<AllocationProposal>.Fail("NO_STOCK_AVAILABLE", "No requested supplies are available at any depot.")
                .WithToolCalls(toolCalls);

        var entities = allocations.Select(a => new AllocationProposalEntity
        {
            WorkflowRunId = triagePlan.WorkflowRunId,
            DepotId = a.DepotId,
            ShelterId = a.ShelterId,
            ItemName = a.ItemName,
            Quantity = a.Quantity
        }).ToList();
        context.AllocationProposals.AddRange(entities);
        await context.SaveChangesAsync(cancellationToken);

        return AgentResult<AllocationProposal>.Ok(new AllocationProposal
        {
            WorkflowRunId = triagePlan.WorkflowRunId,
            AllocationProposalId = entities[0].Id,
            Allocations = allocations,
            Unfulfillable = unfulfillable
        }).WithToolCalls(toolCalls);
    }

    // Greedy fallback: each need, in priority order, takes stock from the first depots that have it.
    private static (List<AllocationItem>, List<UnfulfillableItem>) Greedy(TriagePlan triagePlan, List<InventoryItem> stock)
    {
        var available = stock.ToDictionary(item => item.Id, AllocatableQuantity);
        var allocations = new List<AllocationItem>();
        var unfulfillable = new List<UnfulfillableItem>();

        foreach (var need in triagePlan.PriorityItems)
        {
            var remaining = need.Quantity;
            foreach (var item in stock.Where(item =>
                         string.Equals(item.ItemName, need.NeedType, StringComparison.OrdinalIgnoreCase)))
            {
                var quantity = Math.Min(remaining, available[item.Id]);
                if (quantity <= 0)
                    continue;

                allocations.Add(new AllocationItem
                {
                    DepotId = item.DepotId,
                    ShelterId = need.ShelterId,
                    ItemName = item.ItemName,
                    Quantity = quantity
                });
                available[item.Id] -= quantity;
                remaining -= quantity;

                if (remaining <= 0)
                    break;
            }

            if (remaining > 0)
            {
                unfulfillable.Add(new UnfulfillableItem
                {
                    ShelterId = need.ShelterId,
                    ItemName = need.NeedType,
                    Reason = remaining < need.Quantity
                        ? $"Only {need.Quantity - remaining} of {need.Quantity} could be allocated."
                        : "No free stock above the reserve floor at any depot."
                });
            }
        }

        return (allocations, unfulfillable);
    }

    // Returns the accepted LLM proposal, or null to use the greedy matcher.
    private async Task<(List<AllocationItem>, List<UnfulfillableItem>)?> ProposeWithLlmAsync(
        TriagePlan triagePlan, List<InventoryItem> stock, string? revisionNotes, List<ToolCall> toolCalls, CancellationToken ct)
    {
        if (llm is null)
            return null;

        var needs = new JsonArray(triagePlan.PriorityItems.Select((need, rank) => (JsonNode)new JsonObject
        {
            ["rank"] = rank + 1,
            ["shelterId"] = need.ShelterId,
            ["itemName"] = need.NeedType,
            ["quantity"] = need.Quantity,
            ["priorityScore"] = need.PriorityScore
        }).ToArray());
        var lines = new JsonArray(stock
            .Where(item => AllocatableQuantity(item) > 0)
            .Select(item => (JsonNode)new JsonObject
            {
                ["depotId"] = item.DepotId,
                ["itemName"] = item.ItemName,
                ["freeQuantity"] = AllocatableQuantity(item)
            }).ToArray());
        var content = $"<needs>{needs.ToJsonString()}</needs>\n<stock>{lines.ToJsonString()}</stock>";
        if (!string.IsNullOrWhiteSpace(revisionNotes))
            content += "\n" + LlmData.Fence("revision_notes", revisionNotes, MaxRevisionNotesLength);

        var request = new LlmRequest(PromptVersion, Prompt.Value, content, ResponseSchema);
        var result = await llm.GenerateJsonAsync(request, ct);
        if (!result.Succeeded)
        {
            toolCalls.Add(LlmToolCall.From(request, result, usedFallback: true));
            return null;
        }

        var accepted = TryAcceptProposal(result.Json, triagePlan, stock, out var allocations, out var unfulfillable, out var reason);
        toolCalls.Add(LlmToolCall.From(request, result, usedFallback: !accepted, reason));
        return accepted ? (allocations, unfulfillable) : null;
    }

    /// <summary>
    /// The deterministic check on an LLM proposal: every allocation uses a depot and item that
    /// exist in the stock snapshot, for a shelter that needs that item, with a positive quantity;
    /// no stock line is used beyond its free quantity and no need gets more than it asked for;
    /// every need left short is listed as unfulfillable (and nothing else is); and at least one
    /// allocation is made. Any failure rejects the whole proposal.
    /// </summary>
    public static bool TryAcceptProposal(
        JsonNode? json,
        TriagePlan triagePlan,
        IReadOnlyList<InventoryItem> stock,
        out List<AllocationItem> allocations,
        out List<UnfulfillableItem> unfulfillable,
        out string? reason)
    {
        allocations = [];
        unfulfillable = [];
        reason = null;
        if (json?["allocations"] is not JsonArray proposed)
        {
            reason = "missing allocations";
            return false;
        }

        var comparer = StringComparer.OrdinalIgnoreCase;
        var free = stock
            .GroupBy(item => (item.DepotId, Item: item.ItemName.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.Sum(AllocatableQuantity));
        var needed = triagePlan.PriorityItems
            .GroupBy(need => (need.ShelterId, Item: need.NeedType.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.Sum(need => need.Quantity));
        var given = needed.Keys.ToDictionary(k => k, _ => 0.0);

        foreach (var entry in proposed)
        {
            if (!TryInt(entry?["depotId"], out var depotId) || !TryInt(entry?["shelterId"], out var shelterId)
                || entry?["itemName"]?.GetValue<string>() is not { Length: > 0 } itemName
                || !TryDouble(entry["quantity"], out var quantity))
            {
                reason = "an allocation is missing depotId, shelterId, itemName or quantity";
                return false;
            }
            var item = itemName.ToLowerInvariant();
            if (quantity <= 0)
            {
                reason = $"non-positive quantity for depot {depotId} {itemName}";
                return false;
            }
            if (!free.TryGetValue((depotId, item), out var left))
            {
                reason = $"depot {depotId} has no free {itemName}";
                return false;
            }
            if (!needed.ContainsKey((shelterId, item)))
            {
                reason = $"shelter {shelterId} did not ask for {itemName}";
                return false;
            }
            if (quantity > left + Epsilon)
            {
                reason = $"over-allocates {itemName} at depot {depotId} ({quantity} > {left} free)";
                return false;
            }
            if (given[(shelterId, item)] + quantity > needed[(shelterId, item)] + Epsilon)
            {
                reason = $"gives shelter {shelterId} more {itemName} than it needs";
                return false;
            }

            free[(depotId, item)] = left - quantity;
            given[(shelterId, item)] += quantity;
            var stockName = stock.First(s => s.DepotId == depotId && comparer.Equals(s.ItemName, itemName)).ItemName;
            allocations.Add(new AllocationItem { DepotId = depotId, ShelterId = shelterId, ItemName = stockName, Quantity = quantity });
        }

        if (allocations.Count == 0)
        {
            reason = "no allocations proposed";
            return false;
        }

        var listed = new HashSet<(int, string)>();
        foreach (var entry in json["unfulfillable"] as JsonArray ?? [])
        {
            if (!TryInt(entry?["shelterId"], out var shelterId) || entry?["itemName"]?.GetValue<string>() is not { Length: > 0 } itemName)
            {
                reason = "an unfulfillable entry is missing shelterId or itemName";
                return false;
            }
            var key = (shelterId, itemName.ToLowerInvariant());
            if (!needed.ContainsKey(key))
            {
                reason = $"unfulfillable lists {itemName} for shelter {shelterId}, which was not requested";
                return false;
            }
            if (given[key] >= needed[key] - Epsilon)
            {
                reason = $"unfulfillable lists {itemName} for shelter {shelterId}, which is fully allocated";
                return false;
            }
            if (!listed.Add(key))
                continue;
            unfulfillable.Add(new UnfulfillableItem
            {
                ShelterId = shelterId,
                ItemName = triagePlan.PriorityItems.First(n => n.ShelterId == shelterId && comparer.Equals(n.NeedType, itemName)).NeedType,
                Reason = LlmData.CleanOutput(entry["reason"]?.GetValue<string>(), MaxReasonLength) is { Length: > 0 } r
                    ? r
                    : $"Only {given[key]} of {needed[key]} could be allocated."
            });
        }

        var unlisted = needed.Keys.FirstOrDefault(k => given[k] < needed[k] - Epsilon && !listed.Contains(k));
        if (unlisted != default)
        {
            reason = $"shelter {unlisted.ShelterId} {unlisted.Item} is short but not listed as unfulfillable";
            return false;
        }
        return true;
    }

    private static bool TryInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue v && v.TryGetValue(out value);
    }

    private static bool TryDouble(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue v && v.TryGetValue(out value);
    }

    public static readonly JsonObject ResponseSchema = (JsonObject)JsonNode.Parse("""
    {
      "type": "OBJECT",
      "properties": {
        "allocations": {
          "type": "ARRAY",
          "items": {
            "type": "OBJECT",
            "properties": {
              "depotId": { "type": "INTEGER" },
              "shelterId": { "type": "INTEGER" },
              "itemName": { "type": "STRING" },
              "quantity": { "type": "NUMBER" }
            },
            "required": ["depotId", "shelterId", "itemName", "quantity"],
            "propertyOrdering": ["depotId", "shelterId", "itemName", "quantity"]
          }
        },
        "unfulfillable": {
          "type": "ARRAY",
          "items": {
            "type": "OBJECT",
            "properties": {
              "shelterId": { "type": "INTEGER" },
              "itemName": { "type": "STRING" },
              "reason": { "type": "STRING" }
            },
            "required": ["shelterId", "itemName", "reason"],
            "propertyOrdering": ["shelterId", "itemName", "reason"]
          }
        }
      },
      "required": ["allocations", "unfulfillable"]
    }
    """)!;

    // Free stock above the reserve floor; never negative.
    private static double AllocatableQuantity(InventoryItem item)
        => Math.Max(0, item.QuantityAvailable - item.QuantityReserved - item.ReorderThreshold);
}
