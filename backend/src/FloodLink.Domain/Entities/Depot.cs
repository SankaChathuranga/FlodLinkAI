using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>Physical source of relief supplies. Owned by Member B.</summary>
public class Depot : IHasUpdatedAt
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    /// <summary>The DepotManager account responsible for this depot (optional).</summary>
    public int? ManagerId { get; set; }

    public User? Manager { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time of the last change; stamped automatically on save.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InventoryItem> InventoryItems { get; set; } = new List<InventoryItem>();
}
