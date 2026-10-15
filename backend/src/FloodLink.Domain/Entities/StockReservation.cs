using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>Audit record for reserved inventory stock. Owned by Member B.</summary>
public class StockReservation
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }

    public InventoryItem? InventoryItem { get; set; }

    public int? AllocationProposalId { get; set; }

    public AllocationProposalEntity? AllocationProposal { get; set; }

    public int Quantity { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    [MaxLength(100)]
    public string ReservedBy { get; set; } = string.Empty;

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReleasedAt { get; set; }
}
