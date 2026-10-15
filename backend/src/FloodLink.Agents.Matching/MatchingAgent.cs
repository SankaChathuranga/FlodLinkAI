using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Matching;

/// <summary>
/// Greedily matches prioritized needs to the first depot with remaining stock.
/// Inventory is read-only here: stock is committed only after a coordinator approves a dispatch.
/// </summary>
/// <remarks>
/// Rule (group report FR-04, §10.3):
/// <list type="bullet">
/// <item>M1: needs are served in the order Triage ranked them (highest priority first).</item>
/// <item>M2 / D7: an item matches only when its ItemName equals the need type ignoring case
/// (Water, Food, Medical, Shelter-Repair, Other); no synonyms or substrings, so "Bottled water"
/// never satisfies a Water need.</item>
/// <item>M3: a need is split across depots in DepotId, then item Id, order until covered.</item>
/// <item>M4: any shortfall goes to Unfulfillable with a partial-allocation or no-stock reason.</item>
/// <item>M5: stock is loaded once with AsNoTracking and decremented in an in-memory copy, so later
/// needs in the same plan never double-count it.</item>
/// <item>M6: never changes, reserves or decrements InventoryItems; only AllocationProposals rows are
/// written, replacing any earlier rows for the same run.</item>
/// </list>
/// Failure codes (returned as <see cref="AgentResult{T}"/>, never thrown):
/// <c>EMPTY_TRIAGE_PLAN</c> when the plan has no needs, and <c>NO_STOCK_AVAILABLE</c> when nothing at
/// all could be allocated (nothing is persisted in either case).
/// </remarks>
public sealed class MatchingAgent(AppDbContext context) : IMatchingAgent
{
    /// <inheritdoc />
    public async Task<AgentResult<AllocationProposal>> ExecuteAsync(
        TriagePlan triagePlan,
        CancellationToken cancellationToken = default)
    {
        if (triagePlan.PriorityItems.Count == 0)
            return AgentResult<AllocationProposal>.Fail("EMPTY_TRIAGE_PLAN", "The triage plan contains no needs to match.");

        var stock = await context.InventoryItems
            .AsNoTracking()
            .OrderBy(item => item.DepotId)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var available = stock.ToDictionary(item => item.Id, item => item.QuantityAvailable);
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
                        : "No stock is available at any depot."
                });
            }
        }

        if (allocations.Count == 0)
            return AgentResult<AllocationProposal>.Fail("NO_STOCK_AVAILABLE", "No requested supplies are available at any depot.");

        // A re-run after a revision replaces this run's previous rows. Delete and insert share one
        // SaveChangesAsync, so they commit together; earlier proposals remain in AgentExecutionLogs.
        var previous = await context.AllocationProposals
            .Where(row => row.WorkflowRunId == triagePlan.WorkflowRunId)
            .ToListAsync(cancellationToken);
        context.AllocationProposals.RemoveRange(previous);
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
        });
    }
}
