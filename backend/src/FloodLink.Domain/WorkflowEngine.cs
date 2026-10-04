using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;

namespace FloodLink.Domain;

/// <summary>
/// Owns all workflow state machine logic for <see cref="WorkflowRun"/>.
/// This is the ONLY place <see cref="WorkflowRun.CurrentState"/> may be changed.
/// </summary>
/// <remarks>
/// Design: enum + guard-clause switch expression (chosen over the Stateless library
/// in Phase 1 — 12 transitions fit cleanly in a switch, zero external dependencies,
/// fully explainable in a viva). See <c>context/architecture.md § Phase 1</c>.
///
/// Valid transition table (from Phase 1 design):
/// <code>
/// Triage            → Matching          (agent success)
/// Triage            → Failed            (agent failure)
/// Matching          → Routing           (agent success)
/// Matching          → Failed            (agent failure)
/// Routing           → Validating        (agent success)
/// Routing           → Failed            (agent failure)
/// Validating        → PendingApproval   (agent success)
/// Validating        → Failed            (agent failure)
/// PendingApproval   → Approved          (coordinator action)
/// PendingApproval   → Rejected          (coordinator action)
/// PendingApproval   → RevisionRequested (coordinator action)
/// RevisionRequested → Matching          (orchestrator re-queues)
/// </code>
/// All other transitions are invalid and will be rejected by <see cref="TryTransition"/>.
/// </remarks>
public static class WorkflowEngine
{
    /// <summary>
    /// Attempts to move <paramref name="run"/> to <paramref name="target"/>.
    /// </summary>
    /// <param name="run">The workflow run to transition. Its state will be mutated on success.</param>
    /// <param name="target">The desired next state.</param>
    /// <returns>
    /// <see langword="true"/> if the transition is valid and was applied;
    /// <see langword="false"/> if the transition is not in the allowed table — the run is
    /// left unchanged and no exception is thrown.
    /// </returns>
    /// <remarks>
    /// When <paramref name="target"/> is <see cref="WorkflowState.Failed"/>, this method
    /// additionally sets <see cref="WorkflowRun.FailedAtState"/> to the run's current state
    /// before changing it — recording which stage the failure occurred in.
    /// </remarks>
    public static bool TryTransition(WorkflowRun run, WorkflowState target)
    {
        bool valid = (run.CurrentState, target) switch
        {
            // ── Agent-driven transitions (orchestrator calls these) ──────────────
            (WorkflowState.Triage,            WorkflowState.Matching)          => true,
            (WorkflowState.Triage,            WorkflowState.Failed)            => true,
            (WorkflowState.Matching,          WorkflowState.Routing)           => true,
            (WorkflowState.Matching,          WorkflowState.Failed)            => true,
            (WorkflowState.Routing,           WorkflowState.Validating)        => true,
            (WorkflowState.Routing,           WorkflowState.Failed)            => true,
            (WorkflowState.Validating,        WorkflowState.PendingApproval)   => true,
            (WorkflowState.Validating,        WorkflowState.Failed)            => true,

            // ── Coordinator-driven transitions (API endpoint calls these) ────────
            (WorkflowState.PendingApproval,   WorkflowState.Approved)          => true,
            (WorkflowState.PendingApproval,   WorkflowState.Rejected)          => true,
            (WorkflowState.PendingApproval,   WorkflowState.RevisionRequested) => true,

            // ── Post-revision re-queue (orchestrator calls this automatically) ───
            (WorkflowState.RevisionRequested, WorkflowState.Matching)          => true,

            // ── Everything else is invalid ───────────────────────────────────────
            _ => false
        };

        if (!valid)
            return false;

        // When failing, record which stage the failure occurred in (Phase 1, Task 4).
        if (target == WorkflowState.Failed)
            run.FailedAtState = run.CurrentState;

        run.CurrentState = target;
        run.UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
