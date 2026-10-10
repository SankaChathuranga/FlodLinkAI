using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;

namespace FloodLink.Domain;

/// <summary>
/// Owns all workflow state machine logic for <see cref="WorkflowRun"/>.
/// This is the ONLY place <see cref="WorkflowRun.CurrentState"/> may be changed.
/// </summary>
/// <remarks>
/// Design: enum + guard-clause switch expression (chosen over the Stateless library —
/// the transitions fit cleanly in a switch, with zero external dependencies).
///
/// Valid transition table:
/// <code>
/// Triage            → Matching          (agent success)
/// Matching          → Routing           (agent success)
/// Routing           → Validating        (agent success)
/// Validating        → PendingApproval   (agent success + stock reserved)
/// PendingApproval   → Approved          (coordinator action)
/// PendingApproval   → Rejected          (coordinator action)
/// PendingApproval   → RevisionRequested (coordinator action)
/// RevisionRequested → Matching          (orchestrator re-queues)
/// any non-terminal  → Failed            (unrecoverable error, reason recorded)
/// Failed            → FailedAtState     (coordinator retry of the failed agent stage)
/// </code>
/// Approved and Rejected are terminal. All other transitions are rejected.
/// </remarks>
public static class WorkflowEngine
{
    /// <summary>The stages in which an agent runs; only these can be retried after a failure.</summary>
    public static readonly IReadOnlySet<WorkflowState> AgentStages = new HashSet<WorkflowState>
    {
        WorkflowState.Triage, WorkflowState.Matching, WorkflowState.Routing, WorkflowState.Validating
    };

    /// <summary>States the background runner keeps advancing without human input.</summary>
    public static readonly IReadOnlySet<WorkflowState> AutomaticStates = new HashSet<WorkflowState>
    {
        WorkflowState.Triage, WorkflowState.Matching, WorkflowState.Routing, WorkflowState.Validating,
        WorkflowState.RevisionRequested
    };

    /// <summary>
    /// Returns true when moving from <paramref name="current"/> to <paramref name="target"/> is legal.
    /// <paramref name="failedAtState"/> is only consulted for retries out of
    /// <see cref="WorkflowState.Failed"/>: a run may only re-enter the stage it failed in.
    /// </summary>
    public static bool CanTransition(WorkflowState current, WorkflowState target, WorkflowState? failedAtState = null)
        => (current, target) switch
        {
            // ── Agent-driven transitions (orchestrator calls these) ──────────────
            (WorkflowState.Triage,            WorkflowState.Matching)          => true,
            (WorkflowState.Matching,          WorkflowState.Routing)           => true,
            (WorkflowState.Routing,           WorkflowState.Validating)        => true,
            (WorkflowState.Validating,        WorkflowState.PendingApproval)   => true,

            // ── Coordinator-driven transitions (API endpoints call these) ────────
            (WorkflowState.PendingApproval,   WorkflowState.Approved)          => true,
            (WorkflowState.PendingApproval,   WorkflowState.Rejected)          => true,
            (WorkflowState.PendingApproval,   WorkflowState.RevisionRequested) => true,

            // ── Post-revision re-queue (orchestrator calls this automatically) ───
            (WorkflowState.RevisionRequested, WorkflowState.Matching)          => true,

            // ── Safe failure from any non-terminal state ─────────────────────────
            (WorkflowState.Triage or WorkflowState.Matching or WorkflowState.Routing or
             WorkflowState.Validating or WorkflowState.PendingApproval or
             WorkflowState.RevisionRequested, WorkflowState.Failed)            => true,

            // ── Retry: only back into the agent stage that failed ────────────────
            (WorkflowState.Failed, _) => failedAtState == target && AgentStages.Contains(target),

            // ── Everything else is invalid ───────────────────────────────────────
            _ => false
        };

    /// <summary>
    /// Attempts to move <paramref name="run"/> to <paramref name="target"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the transition is valid and was applied;
    /// <see langword="false"/> if it is not in the allowed table — the run is left unchanged.
    /// </returns>
    /// <remarks>
    /// Moving to <see cref="WorkflowState.Failed"/> records the stage the failure occurred in
    /// (<see cref="WorkflowRun.FailedAtState"/>). Leaving Failed on a retry clears it, along
    /// with the recorded failure reason.
    /// </remarks>
    public static bool TryTransition(WorkflowRun run, WorkflowState target)
    {
        if (!CanTransition(run.CurrentState, target, run.FailedAtState))
            return false;

        if (target == WorkflowState.Failed)
        {
            run.FailedAtState = run.CurrentState;
        }
        else if (run.CurrentState == WorkflowState.Failed)
        {
            run.FailedAtState = null;
            run.FailureReason = null;
        }

        run.CurrentState = target;
        run.UpdatedAt = DateTime.UtcNow;
        return true;
    }
}
