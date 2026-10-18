using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>Persisted allocation produced by the Matching Agent. Owned by Member B.</summary>
public class AllocationProposalEntity : IHasUpdatedAt
{
    public int Id { get; set; }

    public Guid WorkflowRunId { get; set; }

    public WorkflowRun? WorkflowRun { get; set; }

    public int DepotId { get; set; }

    public Depot? Depot { get; set; }

    public int ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    [Required]
    [MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;

    public double Quantity { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Proposed";

    /// <summary>
    /// Depot staff's physical check of a reserved line before dispatch (8.05): how many they counted on the
    /// shelf, when, by whom, and any note. Null until checked.
    /// </summary>
    public double? CountedQuantity { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public int? ConfirmedById { get; set; }

    public User? ConfirmedBy { get; set; }

    public string? ConfirmationNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time of the last change; stamped automatically on save.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
