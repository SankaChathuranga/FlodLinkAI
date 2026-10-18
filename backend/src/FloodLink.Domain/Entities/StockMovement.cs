namespace FloodLink.Domain.Entities;

/// <summary>
/// One change to a depot's stock line (8.03), written in the same transaction as the change: what
/// happened, how much, the line's on-hand and held stock afterwards, and who or which plan caused it.
/// Append-only. Owned by Member B.
/// </summary>
public class StockMovement
{
    public long Id { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    /// <summary>One of <see cref="StockMovementKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The amount moved (always positive; the kind says which way).</summary>
    public double Quantity { get; set; }

    /// <summary>The line's on-hand stock after the change.</summary>
    public double AvailableAfter { get; set; }

    /// <summary>The line's held (reserved) stock after the change.</summary>
    public double ReservedAfter { get; set; }

    /// <summary>The workflow run whose plan caused the change, for plan movements.</summary>
    public Guid? WorkflowRunId { get; set; }

    /// <summary>The user who made the change; null for system steps (e.g. the reservation after validation).</summary>
    public int? UserId { get; set; }

    public User? User { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>The kinds of <see cref="StockMovement"/>.</summary>
public static class StockMovementKinds
{
    /// <summary>A new stock line was added with its opening quantity.</summary>
    public const string Opening = "Opening";
    /// <summary>Received stock was added to on-hand.</summary>
    public const string CheckIn = "CheckIn";
    /// <summary>Depot staff held stock aside by hand.</summary>
    public const string ManualHold = "ManualHold";
    /// <summary>Depot staff released stock they held.</summary>
    public const string ManualRelease = "ManualRelease";
    /// <summary>A validated plan reserved stock while awaiting approval.</summary>
    public const string PlanReserve = "PlanReserve";
    /// <summary>An approved plan took stock out of the depot.</summary>
    public const string PlanCommit = "PlanCommit";
    /// <summary>A rejected or revised plan returned its reserved stock.</summary>
    public const string PlanRelease = "PlanRelease";
    /// <summary>Depot staff counted the reserved stock on the shelf (8.05); no quantity changes.</summary>
    public const string PhysicalCount = "PhysicalCount";

    public static readonly IReadOnlyList<string> All =
        [Opening, CheckIn, ManualHold, ManualRelease, PlanReserve, PlanCommit, PlanRelease, PhysicalCount];
}
