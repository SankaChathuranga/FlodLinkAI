using FloodLink.Domain.Enums;

namespace FloodLink.Domain.Entities;

/// <summary>
/// Records a coordinator's decision on a complete validation-passed plan and
/// marks the point at which supplies are actually committed. Maps to the
/// <c>Dispatches</c> table. Owned by Member D (Ijini).
/// </summary>
public class Dispatch
{
    /// <summary>Primary key (UUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK → <see cref="WorkflowRun"/> this dispatch concludes.</summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>Navigation property.</summary>
    public WorkflowRun? WorkflowRun { get; set; }

    /// <summary>The coordinator's decision (Approved / Rejected / RevisionRequested).</summary>
    public DispatchDecision Decision { get; set; }

    /// <summary>
    /// Human-readable notes. Mandatory for Rejected and RevisionRequested decisions.
    /// </summary>
    public string? ApprovalNotes { get; set; }

    /// <summary>FK → the Users row of the coordinator who made the decision.</summary>
    public int? ApprovedById { get; set; }

    /// <summary>Navigation property for the deciding coordinator.</summary>
    public User? ApprovedBy { get; set; }

    /// <summary>UTC timestamp the dispatch was executed (set only when Approved).</summary>
    public DateTime? DispatchedAt { get; set; }

    /// <summary>UTC timestamp when this record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}