using FloodLink.Domain.Enums;

namespace FloodLink.Domain.Entities;

/// <summary>
/// Represents one end-to-end run of the four-agent relief coordination pipeline.
/// Owned by Member C. Maps to the <c>WorkflowRuns</c> table.
/// </summary>
/// <remarks>
/// The state machine logic that drives <see cref="CurrentState"/> transitions is
/// implemented separately — this entity class carries state but enforces no transitions.
/// </remarks>
public class WorkflowRun
{
    /// <summary>Primary key (UUID).</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable description of the objective this run was started for
    /// (e.g. "Shelter #12 — Water and Food needs, Region Colombo").
    /// </summary>
    public required string Objective { get; set; }

    /// <summary>The current stage of this workflow run through the agent pipeline.</summary>
    public WorkflowState CurrentState { get; set; } = WorkflowState.Triage;

    /// <summary>
    /// Accumulated plan JSON blob. Populated and extended by each agent as it
    /// contributes its output to the shared plan.
    /// </summary>
    public string? PlanJson { get; set; }

    /// <summary>UTC timestamp when this workflow run was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the last state change.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
