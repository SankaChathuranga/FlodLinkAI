namespace FloodLink.Contracts.Agents;

/// <summary>
/// Represents an allocation proposed by the logistics matching agent.
/// </summary>
public class AllocationProposalDto
{
    /// <summary>
    /// Gets or sets the depot supplying the item.
    /// </summary>
    public int DepotId { get; set; }

    /// <summary>
    /// Gets or sets the shelter receiving the item.
    /// </summary>
    public int ShelterId { get; set; }

    /// <summary>
    /// Gets or sets the name of the item being allocated.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity proposed for allocation.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets the status of the allocation proposal.
    /// </summary>
    public string Status { get; set; } = "Proposed";
}