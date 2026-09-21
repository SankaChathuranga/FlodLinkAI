namespace FloodLink.Contracts;

/// <summary>
/// Output of the Validation/Safety Agent (Member D).
/// This feeds the coordinator approval UI in the React dashboard.
/// Field names are fixed per Section 3.3 of the build specification — do not rename.
/// </summary>
public record ValidationResults
{
    /// <summary>The workflow run these results belong to.</summary>
    public required Guid WorkflowRunId { get; init; }

    /// <summary>
    /// Individual check results. Every check must be explicitly listed — none may be
    /// silently skipped. Checks are deterministic and rule-based (no LLM call).
    /// </summary>
    public required IReadOnlyList<ValidationCheck> Checks { get; init; }

    /// <summary>
    /// True when every check in <see cref="Checks"/> passed.
    /// On true, the workflow transitions to <c>PendingApproval</c>.
    /// On false, it transitions to <c>Failed</c> with a recorded reason.
    /// </summary>
    public required bool OverallPassed { get; init; }
}

/// <summary>A single deterministic validation check result.</summary>
public record ValidationCheck
{
    /// <summary>A stable, machine-readable name for this check (e.g. "StockAvailability", "CapacityLimit").</summary>
    public required string CheckName { get; init; }

    /// <summary>Whether this check passed.</summary>
    public required bool Passed { get; init; }

    /// <summary>
    /// Human-readable description of what was violated if <see cref="Passed"/> is false.
    /// Null when the check passed.
    /// </summary>
    public string? ViolationDetail { get; init; }
}
