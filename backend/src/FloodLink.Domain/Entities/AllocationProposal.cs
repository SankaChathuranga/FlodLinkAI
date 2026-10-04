namespace FloodLink.Domain.Entities;

public class AllocationProposal
{
    public int Id { get; set; }

    public int? WorkflowRunId { get; set; }

    public int DepotId { get; set; }

    public int ShelterId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public string Status { get; set; } = "Proposed";

    public DateTime CreatedAt { get; set; }

    public Depot Depot { get; set; } = null!;
}