namespace FloodLink.Infrastructure.Mapbox;

/// <summary>
/// Response model for a single route leg returned by the Mapbox Directions API.
/// Only the fields required by the Route/ETA Agent are mapped — raw API responses
/// are not stored wholesale (see code-standards.md §Data and Storage).
/// </summary>
public sealed record MapboxRouteResult
{
    /// <summary>Total route distance in metres.</summary>
    public required int DistanceMeters { get; init; }

    /// <summary>Total estimated travel duration in seconds.</summary>
    public required int DurationSeconds { get; init; }

    /// <summary>
    /// Mapbox-encoded polyline string (Polyline5) for map display.
    /// Empty string if the API response did not include geometry.
    /// </summary>
    public required string EncodedPolyline { get; init; }
}
