namespace FloodLink.Domain.Entities;

/// <summary>
/// Stores the routing output produced by the Route/ETA Agent for a single workflow run.
/// Owned by Member C. Maps to the <c>Routes</c> table.
/// </summary>
/// <remarks>
/// Fixed schema per Phase 5 architecture decision:
/// WorkflowRunId (FK), DistanceMeters (int), EstimatedDurationSeconds (int),
/// PolylineString (text). No additional columns without a concrete downstream need.
/// </remarks>
public class RouteEntity
{
    /// <summary>Primary key (UUID).</summary>
    public Guid Id { get; set; }

    /// <summary>FK → WorkflowRuns.Id. Identifies which pipeline run this route belongs to.</summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>Navigation property for the owning workflow run.</summary>
    public WorkflowRun WorkflowRun { get; set; } = null!;

    /// <summary>Driving distance in metres as returned by the Mapbox Directions API.</summary>
    public int DistanceMeters { get; set; }

    /// <summary>Estimated driving duration in seconds as returned by the Mapbox Directions API.</summary>
    public int EstimatedDurationSeconds { get; set; }

    /// <summary>
    /// Mapbox-encoded polyline string (Polyline5 format) for map display.
    /// Empty string if the API response did not include geometry.
    /// </summary>
    public string PolylineString { get; set; } = string.Empty;

    /// <summary>UTC timestamp when this route record was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
