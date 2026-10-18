namespace FloodLink.Contracts;

/// <summary>
/// A plain-language explanation of a plan's safety checks for the coordinator (display only: the
/// rule-based <see cref="ValidationResults"/> decide pass/fail). Stored under the plan's
/// <c>validationExplanation</c> key.
/// </summary>
public record ValidationExplanation
{
    /// <summary>"llm" when the model's text passed the checks, otherwise "template".</summary>
    public required string Source { get; init; }

    public string? PromptVersion { get; init; }

    /// <summary>Why the model's text was not used, when <see cref="Source"/> is "template".</summary>
    public string? FallbackReason { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyList<ValidationExplanationPoint> Points { get; init; }

    /// <summary>Whether the explained checks passed (copied from the rules, never from the model).</summary>
    public required bool OverallPassed { get; init; }

    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

public record ValidationExplanationPoint
{
    public required string CheckName { get; init; }

    public required bool Passed { get; init; }

    public required string Explanation { get; init; }
}
