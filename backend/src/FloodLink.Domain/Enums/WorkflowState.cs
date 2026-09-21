namespace FloodLink.Domain.Enums;

/// <summary>
/// The ordered stages of a single workflow run through the four-agent pipeline.
/// Owned by Member C. Transition logic lives in the state machine (to be implemented
/// in a later session) — this enum is the source of truth for valid state names.
/// </summary>
/// <remarks>
/// Valid forward path: Triage → Matching → Routing → Validating → PendingApproval
///                             → Approved | Rejected
///                     PendingApproval → RevisionRequested (loops back to Matching)
/// Any stage → Failed (on unrecoverable error, with a recorded reason).
/// </remarks>
public enum WorkflowState
{
    /// <summary>The Triage/Planner Agent (Member A) is running or has run.</summary>
    Triage,

    /// <summary>The Logistics/Matching Agent (Member B) is running or has run.</summary>
    Matching,

    /// <summary>The Route/ETA Agent (Member C) is running or has run.</summary>
    Routing,

    /// <summary>The Validation/Safety Agent (Member D) is running or has run.</summary>
    Validating,

    /// <summary>All agents passed; a coordinator is reviewing the plan.</summary>
    PendingApproval,

    /// <summary>A coordinator has approved the dispatch.</summary>
    Approved,

    /// <summary>A coordinator has rejected the plan with a stated reason.</summary>
    Rejected,

    /// <summary>
    /// A coordinator has requested changes; the plan loops back to re-matching.
    /// </summary>
    RevisionRequested,

    /// <summary>An unrecoverable error occurred in one of the agent stages.</summary>
    Failed
}
