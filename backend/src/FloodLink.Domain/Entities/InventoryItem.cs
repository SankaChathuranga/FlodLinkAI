using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>Current stock for one named supply at one depot. Owned by Member B.</summary>
public class InventoryItem
{
    public int Id { get; set; }

    public int DepotId { get; set; }

    public Depot? Depot { get; set; }

    [Required]
    [MaxLength(100)]
    public string ItemName { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Unit { get; set; } = "units";

    public double QuantityAvailable { get; set; }

    public int QuantityReserved { get; set; } = 0;

    public int ReorderThreshold { get; set; } = 10;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<StockReservation> Reservations { get; set; } = new List<StockReservation>();
}
