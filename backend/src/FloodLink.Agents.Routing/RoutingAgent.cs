using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Mapbox;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Routing;

/// <summary>
/// Real implementation of <see cref="IRoutingAgentInvoker"/>.
/// Calls the Mapbox Directions API via <see cref="IMapboxClient"/>,
/// persists the result to the <c>Routes</c> table, and returns a
/// <see cref="AgentResult{Route}"/> — never throwing for anticipated failures.
/// </summary>
public sealed class RoutingAgentInvoker : IRoutingAgentInvoker
{
    private readonly IMapboxClient _mapbox;
    private readonly IRouteRepository _routes;
    private readonly AppDbContext _context;

    public RoutingAgentInvoker(IMapboxClient mapbox, IRouteRepository routes, AppDbContext context)
    {
        _mapbox = mapbox;
        _routes = routes;
        _context = context;
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

        var first = proposal.Allocations[0];
        var depot = await _context.Depots.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == first.DepotId, ct);
        var shelter = await _context.Shelters.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == first.ShelterId, ct);

        if (depot is null || shelter is null)
            return AgentResult<Route>.Fail(
                "ROUTING_LOCATION_NOT_FOUND",
                $"Depot {first.DepotId} or shelter {first.ShelterId} does not exist.");

        try
        {
            var result = await _mapbox.GetRouteAsync(
                depot.Longitude, depot.Latitude, shelter.Longitude, shelter.Latitude, ct);

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
                AllocationProposalId = proposal.AllocationProposalId,
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

}
