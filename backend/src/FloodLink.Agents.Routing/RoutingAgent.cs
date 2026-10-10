using System.Diagnostics;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Mapbox;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Routing;

/// <summary>
/// Real implementation of <see cref="IRoutingAgentInvoker"/>.
/// Calls the Mapbox Directions API via <see cref="IMapboxClient"/> once per distinct
/// depot → shelter pair in the proposal, persists one <c>Routes</c> row per leg, and returns
/// a <see cref="AgentResult{Route}"/> — never throwing for anticipated failures.
/// </summary>
/// <remarks>
/// The route's top-level distance, ETA and polyline describe the longest leg (the last delivery
/// to arrive). If any leg can't be routed the whole step fails, so a plan with an incomplete
/// route never reaches validation.
/// </remarks>
public sealed class RoutingAgentInvoker : IRoutingAgentInvoker
{
    private const string Tool = "mapbox.directions";

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

        var pairs = proposal.Allocations
            .Select(a => (a.DepotId, a.ShelterId))
            .Distinct()
            .ToList();

        var depotIds = pairs.Select(p => p.DepotId).Distinct().ToList();
        var shelterIds = pairs.Select(p => p.ShelterId).Distinct().ToList();
        var depots = await _context.Depots.AsNoTracking()
            .Where(d => depotIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);
        var shelters = await _context.Shelters.AsNoTracking()
            .Where(s => shelterIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        var toolCalls = new List<ToolCall>();
        var legs = new List<RouteLeg>();
        var entities = new List<RouteEntity>();

        foreach (var (depotId, shelterId) in pairs)
        {
            if (!depots.TryGetValue(depotId, out var depot) || !shelters.TryGetValue(shelterId, out var shelter))
                return AgentResult<Route>.Fail(
                    "ROUTING_LOCATION_NOT_FOUND",
                    $"Depot {depotId} or shelter {shelterId} does not exist.").WithToolCalls(toolCalls);

            var input = new Dictionary<string, object?>
            {
                ["depotId"] = depotId,
                ["shelterId"] = shelterId,
                ["from"] = new[] { depot.Longitude, depot.Latitude },
                ["to"] = new[] { shelter.Longitude, shelter.Latitude }
            };
            var sw = Stopwatch.StartNew();

            try
            {
                var result = await _mapbox.GetRouteAsync(
                    depot.Longitude, depot.Latitude, shelter.Longitude, shelter.Latitude, ct);
                sw.Stop();

                if (result is null)
                {
                    toolCalls.Add(Failed(input, sw, "No route returned."));
                    return AgentResult<Route>.Fail(
                        "NO_ROUTE",
                        $"Mapbox returned no route for depot {depotId} → shelter {shelterId}.").WithToolCalls(toolCalls);
                }

                var leg = new RouteLeg
                {
                    DepotId = depotId,
                    ShelterId = shelterId,
                    DistanceKm = result.DistanceMeters / 1000.0,
                    EtaMinutes = result.DurationSeconds / 60.0,
                    Polyline = result.EncodedPolyline
                };
                legs.Add(leg);
                toolCalls.Add(new ToolCall
                {
                    Tool = Tool,
                    Input = input,
                    Output = new Dictionary<string, object?>
                    {
                        ["distanceKm"] = leg.DistanceKm,
                        ["etaMinutes"] = leg.EtaMinutes
                    },
                    Succeeded = true,
                    DurationMs = sw.ElapsedMilliseconds
                });

                entities.Add(new RouteEntity
                {
                    WorkflowRunId = proposal.WorkflowRunId,
                    DepotId = depotId,
                    ShelterId = shelterId,
                    DistanceMeters = result.DistanceMeters,
                    EstimatedDurationSeconds = result.DurationSeconds,
                    PolylineString = result.EncodedPolyline
                });
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                toolCalls.Add(Failed(input, sw, "Timed out."));
                return AgentResult<Route>.Fail(
                    "MAPBOX_TIMEOUT",
                    "Mapbox Directions API request timed out after exhausting retries.").WithToolCalls(toolCalls);
            }
            catch (HttpRequestException ex)
            {
                toolCalls.Add(Failed(input, sw, ex.Message));
                return AgentResult<Route>.Fail(
                    "MAPBOX_HTTP_ERROR",
                    $"Mapbox Directions API HTTP error: {ex.Message}").WithToolCalls(toolCalls);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Unexpected exceptions are caught here to satisfy the no-throw contract.
                toolCalls.Add(Failed(input, sw, ex.Message));
                return AgentResult<Route>.Fail(
                    "ROUTING_UNEXPECTED_ERROR",
                    $"Unexpected error in RoutingAgentInvoker: {ex.Message}").WithToolCalls(toolCalls);
            }
        }

        // Persist only once every leg has been routed, so a failed step leaves no partial routes.
        foreach (var entity in entities)
            await _routes.AddAsync(entity, ct);
        await _routes.SaveAsync(ct);

        var longest = legs.MaxBy(leg => leg.EtaMinutes)!;
        return AgentResult<Route>.Ok(new Route
        {
            WorkflowRunId = proposal.WorkflowRunId,
            AllocationProposalId = proposal.AllocationProposalId,
            DistanceKm = longest.DistanceKm,
            EtaMinutes = longest.EtaMinutes,
            Polyline = longest.Polyline,
            Legs = legs
        }).WithToolCalls(toolCalls);
    }

    private static ToolCall Failed(IReadOnlyDictionary<string, object?> input, Stopwatch sw, string error) => new()
    {
        Tool = Tool,
        Input = input,
        Succeeded = false,
        DurationMs = sw.ElapsedMilliseconds,
        Error = error
    };
}
