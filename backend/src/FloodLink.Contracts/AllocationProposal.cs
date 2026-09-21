namespace FloodLink.Contracts;

/// <summary>
/// Output of the Logistics/Matching Agent (Member B).
/// This is the input contract for the Route/ETA Agent (Member C).
/// Field names are fixed per Section 3.3 of the build specification — do not rename.
/// </summary>
public record AllocationProposal
{
    /// <summary>The workflow run this proposal belongs to.</summary>
    public required Guid WorkflowRunId { get; init; }

    /// <summary>
    /// The concrete supply allocations the Logistics Agent has matched from inventory.
    /// Each entry specifies which depot supplies which item/quantity to which shelter.
    /// </summary>
    public required IReadOnlyList<AllocationItem> Allocations { get; init; }

    /// <summary>
    /// Needs from the <see cref="TriagePlan"/> that could not be fulfilled due to
    /// insufficient stock. Unmatched needs must be explicitly listed here — they
    /// must never be silently dropped.
    /// </summary>
    public required IReadOnlyList<UnfulfillableItem> Unfulfillable { get; init; }
}

/// <summary>A single matched allocation within an <see cref="AllocationProposal"/>.</summary>
public record AllocationItem
{
    /// <summary>The depot supplying this item.</summary>
    public required int DepotId { get; init; }

    /// <summary>The shelter receiving this item.</summary>
    public required int ShelterId { get; init; }

    /// <summary>The name of the item being allocated.</summary>
    public required string ItemName { get; init; }

    /// <summary>The quantity being allocated.</summary>
    public required double Quantity { get; init; }
}

/// <summary>A need that the Logistics Agent could not fulfil from available inventory.</summary>
public record UnfulfillableItem
{
    /// <summary>The shelter that has the unmet need.</summary>
    public required int ShelterId { get; init; }

    /// <summary>The item name that could not be sourced.</summary>
    public required string ItemName { get; init; }

    /// <summary>Human-readable reason the need could not be fulfilled (e.g. "Insufficient stock across all depots").</summary>
    public required string Reason { get; init; }
}
