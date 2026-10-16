namespace FloodLink.Contracts;

/// <summary>
/// The Planner step's plan for a run, stored under the <c>plan</c> key of the run's PlanJson and
/// shown in the trace. Advisory: the orchestrator always runs Triage → Matching → Routing →
/// Validation; a plan that names other agents or another order is replaced by the default plan.
/// </summary>
public record ExecutionPlan
{
    public required IReadOnlyList<ExecutionPlanStep> Steps { get; init; }

    /// <summary>"llm" when the model's plan passed the checks, "default" otherwise.</summary>
    public required string Source { get; init; }

    /// <summary>Prompt version used for the LLM attempt (also set when the default plan was used).</summary>
    public string? PromptVersion { get; init; }

    /// <summary>Why the default plan was used (LLM unavailable, or which check the output failed).</summary>
    public string? FallbackReason { get; init; }
}

public record ExecutionPlanStep
{
    /// <summary>One of Triage, Matching, Routing, Validation.</summary>
    public required string Agent { get; init; }
    public required string Purpose { get; init; }

    /// <summary>Why this step matters for this objective (≤ 200 characters).</summary>
    public required string Rationale { get; init; }
}
