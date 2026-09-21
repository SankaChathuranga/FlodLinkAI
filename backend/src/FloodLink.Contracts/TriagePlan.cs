namespace FloodLink.Contracts;

/// <summary>
/// Output of the Triage/Planner Agent (Member A).
/// This is the input contract for the Logistics/Matching Agent (Member B).
/// Field names are fixed per Section 3.3 of the build specification — do not rename.
/// </summary>
public record TriagePlan
{
    /// <summary>The workflow run this plan belongs to.</summary>
    public required Guid WorkflowRunId { get; init; }

    /// <summary>
    /// Ranked list of shelter needs, ordered from highest to lowest priority.
    /// Each item references a real report ID and carries a justification.
    /// </summary>
    public required IReadOnlyList<TriagePriorityItem> PriorityItems { get; init; }
}

/// <summary>
/// A single prioritized need entry within a <see cref="TriagePlan"/>.
/// Field names match the JSON contract in Section 3.3.
/// </summary>
public record TriagePriorityItem
{
    /// <summary>The ID of the field report this item is derived from.</summary>
    public required int ReportId { get; init; }

    /// <summary>The shelter this need belongs to.</summary>
    public required int ShelterId { get; init; }

    /// <summary>Type of need (Water / Food / Medical / Shelter-Repair / Other).</summary>
    public required string NeedType { get; init; }

    /// <summary>Quantity required to fulfil this need.</summary>
    public required double Quantity { get; init; }

    /// <summary>Priority score in the range 0–100 (higher = more urgent).</summary>
    public required double PriorityScore { get; init; }

    /// <summary>Short agent-generated justification for the assigned priority score.</summary>
    public required string Justification { get; init; }
}
