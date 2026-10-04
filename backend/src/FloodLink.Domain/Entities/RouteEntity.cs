namespace FloodLink.Domain.Entities;

/// <summary>
/// Stores the routing output for a specific allocation proposal.
/// Owned by Member C. Maps to the <c>Routes</c> table.
/// </summary>
/// <remarks>
/// TODO (Member C — Week 2/3): Confirm whether <see cref="RoutePolyline"/> should be
/// stored as a raw encoded string, GeoJSON, or a PostGIS geometry type.
/// Coordinate precision (decimal places) should be agreed with the team before
/// the first migration that includes this table.
/// </remarks>
public class RouteEntity
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>FK → AllocationProposals (owned by Member B).</summary>
    public int AllocationProposalId { get; set; }

    // ── Origin (depot) coordinates ────────────────────────────────────────────

    /// <summary>Depot latitude. TODO: confirm decimal precision with the team.</summary>
    public double OriginLat { get; set; }

    /// <summary>Depot longitude. TODO: confirm decimal precision with the team.</summary>
    public double OriginLng { get; set; }

    // ── Destination (shelter) coordinates ─────────────────────────────────────

    /// <summary>Shelter latitude. TODO: confirm decimal precision with the team.</summary>
    public double DestLat { get; set; }

    /// <summary>Shelter longitude. TODO: confirm decimal precision with the team.</summary>
    public double DestLng { get; set; }

    // ── Routing API output ────────────────────────────────────────────────────

    /// <summary>Road distance in kilometres as returned by the routing API.</summary>
    public double DistanceKm { get; set; }

    /// <summary>Estimated travel time in minutes as returned by the routing API.</summary>
    public double EtaMinutes { get; set; }

    /// <summary>
    /// Encoded route polyline string for map display (e.g. Google Encoded Polyline format).
    /// TODO: decide encoding format with the team before finalising schema.
    /// </summary>
    public string? RoutePolyline { get; set; }

    /// <summary>UTC timestamp when this route record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
