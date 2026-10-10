using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

    /// <summary>Quantity physically on hand at the depot, including reserved stock.</summary>
    public double QuantityAvailable { get; set; }

    /// <summary>Quantity held for plans awaiting coordinator approval. Free stock is available minus reserved.</summary>
    public double QuantityReserved { get; set; }

    /// <summary>Reserve floor: allocations must leave at least this much free stock at the depot.</summary>
    public double ReorderThreshold { get; set; }

    /// <summary>Optimistic-concurrency token (PostgreSQL <c>xmin</c>); concurrent stock writes fail instead of overwriting.</summary>
    [JsonIgnore]
    public uint Version { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
