using System.Diagnostics;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Infrastructure.Services;

/// <summary>
/// Stock lifecycle for allocation proposals (Member B's business operation):
/// <c>Proposed → Reserved → Committed</c>, or <c>Proposed/Reserved → Released</c>.
/// </summary>
/// <remarks>
/// <para>
/// Free stock is <c>QuantityAvailable − QuantityReserved</c>. Reserving moves stock into
/// <c>QuantityReserved</c>; committing removes it from both; releasing returns it.
/// </para>
/// <para>
/// Concurrency: <see cref="InventoryItem.Version"/> maps to PostgreSQL's <c>xmin</c>, so two
/// requests that change the same stock row cannot both succeed — the second save raises
/// <see cref="DbUpdateConcurrencyException"/>, which is reported as <c>STOCK_CONFLICT</c>.
/// </para>
/// <para>
/// The <c>Stage*</c> methods only change tracked entities, so a caller can save the stock change
/// together with its own changes (dispatch, audit, state) in one <c>SaveChanges</c> transaction.
/// </para>
/// </remarks>
public sealed class StockReservationService(AppDbContext db) : IStockReservationService
{
    public const string Proposed = "Proposed";
    public const string Reserved = "Reserved";
    public const string Committed = "Committed";
    public const string Released = "Released";

    /// <inheritdoc />
    public async Task<StockOperationResult> ReserveForRunAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var proposals = await db.AllocationProposals
            .Where(p => p.WorkflowRunId == workflowRunId && p.Status == Proposed)
            .ToListAsync(cancellationToken);
        var items = await LoadItemsAsync(proposals, cancellationToken);

        var lines = proposals.GroupBy(p => (p.DepotId, p.ItemName)).ToList();

        // Check every line before changing anything: the reservation is all or nothing.
        foreach (var line in lines)
        {
            var quantity = line.Sum(p => p.Quantity);
            var item = items.GetValueOrDefault(line.Key);
            var free = item is null ? 0 : item.QuantityAvailable - item.QuantityReserved;
            if (item is null || quantity > free)
            {
                return StockOperationResult.Fail("INSUFFICIENT_STOCK",
                    $"Depot {line.Key.DepotId} has {free} {line.Key.ItemName} free; the plan needs {quantity}.",
                    [ToolCallFor("db.inventory.reserve", workflowRunId, proposals, sw, succeeded: false)]);
            }
        }

        foreach (var line in lines)
        {
            var item = items[line.Key];
            item.QuantityReserved += line.Sum(p => p.Quantity);
            item.UpdatedAt = DateTime.UtcNow;
            foreach (var proposal in line)
                proposal.Status = Reserved;
        }

        return await SaveAsync(ToolCallFor("db.inventory.reserve", workflowRunId, proposals, sw, succeeded: true), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<StockOperationResult> ReleaseForRunAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var released = await StageReleaseAsync(workflowRunId, cancellationToken);
        return await SaveAsync(ToolCallFor("db.inventory.release", workflowRunId, released, sw, succeeded: true), cancellationToken);
    }

    /// <summary>
    /// Takes the run's stock out of the depots: reserved lines leave both available and reserved;
    /// lines that were never reserved must still fit in free stock. Does not save.
    /// </summary>
    public async Task<StockOperationResult> StageCommitAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
    {
        var proposals = await db.AllocationProposals
            .Where(p => p.WorkflowRunId == workflowRunId && (p.Status == Reserved || p.Status == Proposed))
            .ToListAsync(cancellationToken);
        var items = await LoadItemsAsync(proposals, cancellationToken);

        // Check every line before changing anything: the commit is all or nothing.
        foreach (var line in proposals.GroupBy(p => (p.DepotId, p.ItemName)))
        {
            var item = items.GetValueOrDefault(line.Key);
            var reserved = line.Where(p => p.Status == Reserved).Sum(p => p.Quantity);
            var unreserved = line.Where(p => p.Status == Proposed).Sum(p => p.Quantity);
            var enough = item is not null
                         && item.QuantityAvailable >= reserved + unreserved
                         && item.QuantityAvailable - item.QuantityReserved >= unreserved;
            if (!enough)
            {
                return StockOperationResult.Fail("INSUFFICIENT_STOCK",
                    $"Depot {line.Key.DepotId} no longer has {reserved + unreserved} {line.Key.ItemName} available.");
            }
        }

        foreach (var proposal in proposals)
        {
            var item = items[(proposal.DepotId, proposal.ItemName)];
            var wasReserved = proposal.Status == Reserved;
            item.QuantityAvailable -= proposal.Quantity;
            if (wasReserved)
                item.QuantityReserved = Math.Max(0, item.QuantityReserved - proposal.Quantity);
            item.UpdatedAt = DateTime.UtcNow;
            proposal.Status = Committed;
        }

        return StockOperationResult.Ok();
    }

    /// <summary>Returns the run's reserved stock and marks its open proposals Released. Does not save.</summary>
    public async Task<IReadOnlyList<AllocationProposalEntity>> StageReleaseAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
    {
        var proposals = await db.AllocationProposals
            .Where(p => p.WorkflowRunId == workflowRunId && (p.Status == Reserved || p.Status == Proposed))
            .ToListAsync(cancellationToken);
        var items = await LoadItemsAsync(proposals, cancellationToken);

        foreach (var proposal in proposals)
        {
            if (proposal.Status == Reserved && items.TryGetValue((proposal.DepotId, proposal.ItemName), out var item))
            {
                item.QuantityReserved = Math.Max(0, item.QuantityReserved - proposal.Quantity);
                item.UpdatedAt = DateTime.UtcNow;
            }

            proposal.Status = Released;
        }

        return proposals;
    }

    /// <summary>Manually holds <paramref name="quantity"/> of an item (depot staff), if it is free.</summary>
    public async Task<(StockOperationResult Result, InventoryItem? Item)> ReserveItemAsync(
        int inventoryItemId, double quantity, CancellationToken cancellationToken = default)
    {
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == inventoryItemId, cancellationToken);
        if (item is null)
            return (StockOperationResult.Fail("NOT_FOUND", $"Inventory item {inventoryItemId} was not found."), null);

        var free = item.QuantityAvailable - item.QuantityReserved;
        if (quantity > free)
            return (StockOperationResult.Fail("INSUFFICIENT_STOCK", $"Only {free} {item.ItemName} is free to reserve."), item);

        item.QuantityReserved += quantity;
        item.UpdatedAt = DateTime.UtcNow;
        return (await SaveAsync(null, cancellationToken), item);
    }

    /// <summary>Manually releases up to <paramref name="quantity"/> of an item's reserved stock.</summary>
    public async Task<(StockOperationResult Result, InventoryItem? Item)> ReleaseItemAsync(
        int inventoryItemId, double quantity, CancellationToken cancellationToken = default)
    {
        var item = await db.InventoryItems.FirstOrDefaultAsync(i => i.Id == inventoryItemId, cancellationToken);
        if (item is null)
            return (StockOperationResult.Fail("NOT_FOUND", $"Inventory item {inventoryItemId} was not found."), null);

        if (quantity > item.QuantityReserved)
            return (StockOperationResult.Fail("NOT_RESERVED", $"Only {item.QuantityReserved} {item.ItemName} is reserved."), item);

        item.QuantityReserved -= quantity;
        item.UpdatedAt = DateTime.UtcNow;
        return (await SaveAsync(null, cancellationToken), item);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<Dictionary<(int DepotId, string ItemName), InventoryItem>> LoadItemsAsync(
        IReadOnlyCollection<AllocationProposalEntity> proposals, CancellationToken ct)
    {
        if (proposals.Count == 0)
            return new();

        var depotIds = proposals.Select(p => p.DepotId).Distinct().ToList();
        var itemNames = proposals.Select(p => p.ItemName).Distinct().ToList();
        var items = await db.InventoryItems
            .Where(i => depotIds.Contains(i.DepotId) && itemNames.Contains(i.ItemName))
            .ToListAsync(ct);
        return items.ToDictionary(i => (i.DepotId, i.ItemName));
    }

    private async Task<StockOperationResult> SaveAsync(ToolCall? toolCall, CancellationToken ct)
    {
        IReadOnlyList<ToolCall>? calls = toolCall is null ? null : [toolCall];
        try
        {
            await db.SaveChangesAsync(ct);
            return StockOperationResult.Ok(calls);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed the same stock first. Discard our in-memory changes so
            // the context can still be used, and report the conflict.
            foreach (var entry in db.ChangeTracker.Entries()
                         .Where(e => e.Entity is InventoryItem or AllocationProposalEntity && e.State == EntityState.Modified)
                         .ToList())
            {
                await entry.ReloadAsync(ct);
            }

            return StockOperationResult.Fail("STOCK_CONFLICT",
                "Stock changed while this request was being processed. Try again.", calls);
        }
    }

    private static ToolCall ToolCallFor(string tool, Guid workflowRunId,
        IReadOnlyCollection<AllocationProposalEntity> lines, Stopwatch sw, bool succeeded) => new()
    {
        Tool = tool,
        Input = new Dictionary<string, object?> { ["workflowRunId"] = workflowRunId },
        Output = new Dictionary<string, object?>
        {
            ["lines"] = lines.Count,
            ["totalQuantity"] = lines.Sum(l => l.Quantity)
        },
        Succeeded = succeeded,
        DurationMs = sw.ElapsedMilliseconds
    };
}
