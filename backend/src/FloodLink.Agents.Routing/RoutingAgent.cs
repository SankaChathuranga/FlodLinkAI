using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure.Mapbox;

namespace FloodLink.Agents.Routing;

/// <summary>
/// Real implementation of <see cref="IRoutingAgentInvoker"/>.
/// Calls the Mapbox Directions API via <see cref="IMapboxClient"/>,
/// persists the result to the <c>Routes</c> table, and returns a
/// <see cref="AgentResult{Route}"/> — never throwing for anticipated failures.
/// </summary>
/// <remarks>
/// Design decisions:
/// <list type="bullet">
///   <item>Coordinates come from the first allocation item's depot/shelter.
///   If the <see cref="AllocationProposal"/> contains no allocations the agent
///   returns <c>AgentResult.Fail</c> with code <c>NO_ALLOCATIONS</c>.</item>
///   <item>Depot and shelter coordinates are looked up via hardcoded placeholder
///   values here. In a production system these would come from a Depots/Shelters
///   table. The TODO below marks this for a future team task.</item>
/// </list>
/// </remarks>
public sealed class RoutingAgentInvoker : IRoutingAgentInvoker
{
    private readonly IMapboxClient _mapbox;
    private readonly IRouteRepository _routes;

    public RoutingAgentInvoker(IMapboxClient mapbox, IRouteRepository routes)
    {
        _mapbox = mapbox;
        _routes = routes;
    }

    /// <inheritdoc />
    public async Task<AgentResult<Route>> ExecuteAsync(
        AllocationProposal proposal,
        CancellationToken ct = default)
    {
        if (proposal.Allocations.Count == 0)
            return AgentResult<Route>.Fail(
                "NO_ALLOCATIONS",
                "AllocationProposal contains no allocations — cannot compute a route.");

        // TODO (team): Replace hardcoded coordinates with a lookup against
        // a Depots/Shelters table once Member B's entities are registered in AppDbContext.
        // For now we use the first allocation's depot and shelter IDs as lookup keys
        // in a placeholder dictionary so the agent is testable end-to-end.
        var first = proposal.Allocations[0];
        var (originLng, originLat) = GetDepotCoords(first.DepotId);
        var (destLng, destLat) = GetShelterCoords(first.ShelterId);

        try
        {
            var result = await _mapbox.GetRouteAsync(originLng, originLat, destLng, destLat, ct);

            if (result is null)
                return AgentResult<Route>.Fail(
                    "NO_ROUTE",
                    $"Mapbox returned no route for depot {first.DepotId} → shelter {first.ShelterId}.");

            var routeEntity = new RouteEntity
            {
                WorkflowRunId = proposal.WorkflowRunId,
                DistanceMeters = result.DistanceMeters,
                EstimatedDurationSeconds = result.DurationSeconds,
                PolylineString = result.EncodedPolyline
            };
            await _routes.AddAsync(routeEntity, ct);
            await _routes.SaveAsync(ct);

            var contractRoute = new Route
            {
                WorkflowRunId = proposal.WorkflowRunId,
                AllocationProposalId = first.DepotId, // placeholder until Member B's ID is real
                DistanceKm = result.DistanceMeters / 1000.0,
                EtaMinutes = result.DurationSeconds / 60.0,
                Polyline = result.EncodedPolyline
            };

            return AgentResult<Route>.Ok(contractRoute);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return AgentResult<Route>.Fail(
                "MAPBOX_TIMEOUT",
                "Mapbox Directions API request timed out after exhausting retries.");
        }
        catch (HttpRequestException ex)
        {
            return AgentResult<Route>.Fail(
                "MAPBOX_HTTP_ERROR",
                $"Mapbox Directions API HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            // Unexpected exceptions are caught here to satisfy the no-throw contract.
            // They still flow to the orchestrator's StepAsync which logs and fails the run.
            return AgentResult<Route>.Fail(
                "ROUTING_UNEXPECTED_ERROR",
                $"Unexpected error in RoutingAgentInvoker: {ex.Message}");
        }
    }

    // ── Coordinate helpers ─────────────────────────────────────────────────────
    // ponytail: hardcoded placeholder — replace with DB lookup once Member B's
    // Depots/Shelters tables exist. Using Colombo area coordinates for Sri Lanka context.

    private static (double Lng, double Lat) GetDepotCoords(int depotId) => depotId switch
    {
        1 => (79.8612, 6.9271),  // Colombo central depot (placeholder)
        2 => (80.6337, 7.2906),  // Kandy depot (placeholder)
        _ => (79.8612, 6.9271)   // Default to Colombo
    };

    private static (double Lng, double Lat) GetShelterCoords(int shelterId) => shelterId switch
    {
        1 => (80.0000, 7.1000),  // Shelter 1 (placeholder)
        2 => (80.3000, 7.5000),  // Shelter 2 (placeholder)
        _ => (80.0000, 7.1000)   // Default
    };
}
