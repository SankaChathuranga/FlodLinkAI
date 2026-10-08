using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>Persisted allocation produced by the Matching Agent. Owned by Member B.</summary>
public class AllocationProposalEntity
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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
