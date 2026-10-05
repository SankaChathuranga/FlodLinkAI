namespace FloodLink.Contracts.Agents;

/// <summary>
/// Represents the prioritized list of resource needs produced by the triage process.
/// </summary>
public class TriagePlan
{
    /// <summary>
    /// Gets or sets the resource needs that must be fulfilled.
    /// </summary>
    public List<TriageNeed> Needs { get; set; } = new();
}

/// <summary>
/// Represents a resource requirement for a shelter.
/// </summary>
public class TriageNeed
{
    /// <summary>
    /// Gets or sets the shelter requiring the resource.
    /// </summary>
    public int ShelterId { get; set; }

    /// <summary>
    /// Gets or sets the name of the required item.
    /// </summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity required by the shelter.
    /// </summary>
    public decimal QuantityRequired { get; set; }

    /// <summary>
    /// Gets or sets the priority of the resource need.
    /// Lower values represent higher priority.
    /// </summary>
    public int Priority { get; set; }

    /// <summary>Optional shelter latitude used for nearest-depot selection.</summary>
    public decimal? ShelterLatitude { get; set; }

    /// <summary>Optional shelter longitude used for nearest-depot selection.</summary>
    public decimal? ShelterLongitude { get; set; }
}