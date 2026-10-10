namespace FloodLink.Domain.Entities;

/// <summary>
/// Append-only log of every meaningful event in the approval/dispatch lifecycle
/// (validation pass/fail, approval, rejection, revision request, dispatch).
/// Maps to the <c>AuditTrail</c> table. Owned by Member D (Ijini).
/// </summary>
public class AuditTrail
{
    /// <summary>Primary key (UUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK → <see cref="Dispatch"/> this audit entry belongs to (nullable for pre-dispatch events).</summary>
    public Guid? DispatchId { get; set; }

    /// <summary>Navigation property.</summary>
    public Dispatch? Dispatch { get; set; }

    /// <summary>Machine-readable event type (e.g. "PlanValidated", "DispatchApproved", "DispatchRejected").</summary>
    public required string EventType { get; set; }

    /// <summary>Arbitrary JSON detail payload for the event.</summary>
    public string? EventDetailJson { get; set; }

    /// <summary>FK → Users row that performed the action; null for system-generated events.</summary>
    public int? ActorId { get; set; }

    /// <summary>Navigation property for the acting user.</summary>
    public User? Actor { get; set; }

    /// <summary>UTC timestamp when the event occurred.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}