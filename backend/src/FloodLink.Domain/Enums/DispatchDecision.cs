namespace FloodLink.Domain.Enums;

/// <summary>
/// The decision recorded on a <see cref="Entities.Dispatch"/> by an authorized
/// relief coordinator and the workflow outcome it maps to.
/// Owned by Member D (Ijini).
/// </summary>
public enum DispatchDecision
{
    /// <summary>The coordinator approved the plan; supplies are committed and a truck is dispatched.</summary>
    Approved,

    /// <summary>The coordinator rejected the plan with a stated reason; no stock is consumed.</summary>
    Rejected,

    /// <summary>The coordinator requested changes; the plan loops back to re-matching.</summary>
    RevisionRequested
}