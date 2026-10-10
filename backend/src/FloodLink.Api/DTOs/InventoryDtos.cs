using System.ComponentModel.DataAnnotations;

namespace FloodLink.Api.DTOs;

public sealed class CreateDepotDto
{
    [Required, StringLength(150)]
    public string Name { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }
}

public sealed class CreateInventoryItemDto
{
    [Range(1, int.MaxValue)]
    public int DepotId { get; init; }

    [Required, StringLength(100)]
    public string ItemName { get; init; } = string.Empty;

    [Required, StringLength(30)]
    public string Unit { get; init; } = "units";

    [Range(0, double.MaxValue)]
    public double QuantityAvailable { get; init; }

    /// <summary>Reserve floor: plans must leave at least this much free stock.</summary>
    [Range(0, double.MaxValue)]
    public double ReorderThreshold { get; init; }
}

public sealed class StockCheckInDto
{
    [Range(0.000001, double.MaxValue)]
    public double QuantityReceived { get; init; }
}

public sealed class StockQuantityDto
{
    [Range(0.000001, double.MaxValue)]
    public double Quantity { get; init; }
}
