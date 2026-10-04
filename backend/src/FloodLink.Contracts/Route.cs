namespace FloodLink.Contracts;

/// <summary>
/// Output of the Route/ETA Agent (Member C).
/// This is the input contract for the Validation/Safety Agent (Member D).
/// Field names are fixed per Section 3.3 of the build specification — do not rename.
/// </summary>
public record Route
{
    /// <summary>The workflow run this route belongs to.</summary>
    public required Guid WorkflowRunId { get; init; }

    /// <summary>The allocation proposal this route was calculated for.</summary>
    public required int AllocationProposalId { get; init; }

    /// <summary>Great-circle or road distance in kilometres returned by the routing API.</summary>
    public required double DistanceKm { get; init; }

    /// <summary>Estimated travel time in minutes returned by the routing API.</summary>
    public required double EtaMinutes { get; init; }

    /// <summary>
    /// Encoded route geometry (e.g. Google-encoded polyline) for map display.
    /// May be an empty string if the routing API did not return one.
    /// </summary>
    public required string Polyline { get; init; }
}
