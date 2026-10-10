using FloodLink.Domain.Enums;

namespace FloodLink.Domain;

/// <summary>
/// Context-free view of the legal workflow transitions, for callers that only know the two
/// states involved. Delegates to <see cref="WorkflowEngine"/>, which owns the single table.
/// </summary>
/// <remarks>
/// Retries out of Failed depend on the stage the run failed in, so they are never allowed
/// here; use <see cref="WorkflowEngine.TryTransition"/> with the run itself for retries.
/// </remarks>
public static class WorkflowStateTransitions
{
    /// <summary>Returns true when a transition from <paramref name="current"/> to <paramref name="target"/> is legal.</summary>
    public static bool IsAllowed(WorkflowState current, WorkflowState target) =>
        WorkflowEngine.CanTransition(current, target);
}
