namespace FloodLink.Domain.Entities;

public class InventoryItem
{
    public int Id { get; set; }

    public int DepotId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal QuantityAvailable { get; set; }

    public decimal QuantityReserved { get; set; }

    public decimal ReorderThreshold { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Depot Depot { get; set; } = null!;
}