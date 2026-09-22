namespace FloodLink.Domain.Entities;

public class Depot
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public int? ManagerId { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<InventoryItem> InventoryItems { get; set; }
        = new List<InventoryItem>();

    public ICollection<AllocationProposal> AllocationProposals { get; set; }
        = new List<AllocationProposal>();
}