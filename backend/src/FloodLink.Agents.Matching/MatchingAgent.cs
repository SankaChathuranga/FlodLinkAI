using System.Diagnostics;
using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Matching;

/// <summary>
/// Greedily matches prioritized needs to the first depot with remaining stock.
/// Only stock that is free (not reserved by another plan) and above the depot's reserve
/// floor (<see cref="InventoryItem.ReorderThreshold"/>) is offered.
/// Inventory is read-only here: stock is reserved after validation and committed only after
/// a coordinator approves a dispatch.
/// </summary>
public sealed class MatchingAgent(AppDbContext context) : IMatchingAgent
{
    /// <inheritdoc />
    public async Task<AgentResult<AllocationProposal>> ExecuteAsync(
        TriagePlan triagePlan,
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
        var inventoryRead = new ToolCall
        {
            Tool = "db.inventory.read",
            Input = new Dictionary<string, object?>
            {
                ["itemNames"] = triagePlan.PriorityItems.Select(need => need.NeedType).Distinct().ToList()
            },
            Output = new Dictionary<string, object?> { ["stockLines"] = stock.Count },
            Succeeded = true,
            DurationMs = sw.ElapsedMilliseconds
        };

        var available = stock.ToDictionary(item => item.Id, AllocatableQuantity);
        var allocations = new List<AllocationProposalEntity>();
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

                allocations.Add(new AllocationProposalEntity
                {
                    WorkflowRunId = triagePlan.WorkflowRunId,
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
                    Reason = allocations.Any(allocation => allocation.ShelterId == need.ShelterId &&
                                                          string.Equals(allocation.ItemName, need.NeedType, StringComparison.OrdinalIgnoreCase))
                        ? $"Only {need.Quantity - remaining} of {need.Quantity} could be allocated."
                        : "No free stock above the reserve floor at any depot."
                });
            }
        }

        if (allocations.Count == 0)
            return AgentResult<AllocationProposal>.Fail("NO_STOCK_AVAILABLE", "No requested supplies are available at any depot.")
                .WithToolCalls([inventoryRead]);

        context.AllocationProposals.AddRange(allocations);
        await context.SaveChangesAsync(cancellationToken);

        return AgentResult<AllocationProposal>.Ok(new AllocationProposal
        {
            WorkflowRunId = triagePlan.WorkflowRunId,
            AllocationProposalId = allocations[0].Id,
            Allocations = allocations.Select(allocation => new AllocationItem
            {
                DepotId = allocation.DepotId,
                ShelterId = allocation.ShelterId,
                ItemName = allocation.ItemName,
                Quantity = allocation.Quantity
            }).ToList(),
            Unfulfillable = unfulfillable
        }).WithToolCalls([inventoryRead]);
    }

    // Free stock above the reserve floor; never negative.
    private static double AllocatableQuantity(InventoryItem item)
        => Math.Max(0, item.QuantityAvailable - item.QuantityReserved - item.ReorderThreshold);
}
