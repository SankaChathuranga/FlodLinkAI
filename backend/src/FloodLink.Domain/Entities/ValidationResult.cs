namespace FloodLink.Domain.Entities;

/// <summary>
/// A single persisted check result produced by the Validation/Safety Agent.
/// One row per <c>ValidationCheck</c> output. Maps to the <c>ValidationResults</c>
/// table (plural) to match the contract naming. Owned by Member D (Ijini).
/// </summary>
public class ValidationResult
{
    /// <summary>Primary key (UUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK → <see cref="WorkflowRun"/> the check was run against.</summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>Navigation property.</summary>
    public WorkflowRun? WorkflowRun { get; set; }

    /// <summary>Stable machine-readable check name (e.g. "StockAvailability").</summary>
    public required string CheckName { get; set; }

    /// <summary>Whether this specific check passed.</summary>
    public bool Passed { get; set; }

    /// <summary>Human-readable violation detail when the check failed; null on pass.</summary>
    public string? ViolationDetail { get; set; }

    /// <summary>UTC timestamp when the check ran.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}