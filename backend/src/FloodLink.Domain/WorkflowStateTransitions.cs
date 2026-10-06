using FloodLink.Domain.Enums;

namespace FloodLink.Domain;

/// <summary>
/// Source of truth for legal workflow state transitions.
/// Pure validation — no I/O — so workflows can only advance along the defined
/// pipeline and any invalid transition is rejected server-side.
/// </summary>
/// <remarks>
/// Valid paths:
/// Triage → Matching → Routing → Validating → PendingApproval → Approved | Rejected
/// PendingApproval → RevisionRequested → Matching
/// Any non-terminal stage → Failed
/// Approved / Rejected / Failed are terminal.
/// </remarks>
public static class WorkflowStateTransitions
{
    private static readonly IReadOnlyDictionary<WorkflowState, WorkflowState[]> Allowed = new Dictionary<WorkflowState, WorkflowState[]>
    {
        [WorkflowState.Triage] = new[] { WorkflowState.Matching, WorkflowState.Failed },
        [WorkflowState.Matching] = new[] { WorkflowState.Routing, WorkflowState.Failed },
        [WorkflowState.Routing] = new[] { WorkflowState.Validating, WorkflowState.Failed },
        [WorkflowState.Validating] = new[] { WorkflowState.PendingApproval, WorkflowState.Failed },
        [WorkflowState.PendingApproval] = new[] { WorkflowState.Approved, WorkflowState.Rejected, WorkflowState.RevisionRequested, WorkflowState.Failed },
        [WorkflowState.RevisionRequested] = new[] { WorkflowState.Matching, WorkflowState.Failed },
        [WorkflowState.Approved] = Array.Empty<WorkflowState>(),
        [WorkflowState.Rejected] = Array.Empty<WorkflowState>(),
        [WorkflowState.Failed] = Array.Empty<WorkflowState>()
    };

    /// <summary>Returns true when a transition from <paramref name="current"/> to <paramref name="target"/> is legal.</summary>
    public static bool IsAllowed(WorkflowState current, WorkflowState target) =>
        Allowed.TryGetValue(current, out var allowed) && allowed.Contains(target);
}