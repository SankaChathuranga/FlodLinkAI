using System.ComponentModel.DataAnnotations;

namespace FloodLink.Api.DTOs;

public sealed class CreateDepotDto
{
    [Required, StringLength(150, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    /// <summary>Optional DepotManager responsible for the depot.</summary>
    [Range(1, int.MaxValue)]
    public int? ManagerId { get; init; }
}

public sealed class CreateInventoryItemDto
{
    [Range(1, int.MaxValue)]
    public int DepotId { get; init; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string ItemName { get; init; } = string.Empty;

    [Required, StringLength(30, MinimumLength = 1)]
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

public sealed class UpdateDepotDto
{
    [Required, StringLength(150, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    /// <summary>The DepotManager responsible for the depot; null clears the assignment.</summary>
    [Range(1, int.MaxValue)]
    public int? ManagerId { get; init; }
}

/// <summary>A depot as the API returns it, with its manager's name and how many stock lines it holds.</summary>
public sealed record DepotDto(
    int Id,
    string Name,
    double Latitude,
    double Longitude,
    int? ManagerId,
    string? ManagerName,
    int ItemCount,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static readonly System.Linq.Expressions.Expression<Func<FloodLink.Domain.Entities.Depot, DepotDto>> Projection =
        d => new DepotDto(d.Id, d.Name, d.Latitude, d.Longitude, d.ManagerId,
            d.Manager != null ? d.Manager.Name : null, d.InventoryItems.Count, d.CreatedAt, d.UpdatedAt);
}
