using System.ComponentModel.DataAnnotations;
using FloodLink.Api.Auth;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Mapbox;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>
/// Delivery routes (Member C): a stored route leg of a workflow run, and an on-demand depot → shelter
/// route from Mapbox for planning ("how far is this shelter from that depot?"). The Route agent
/// stores one leg per depot → shelter pair of a plan; this controller only reads them.
/// </summary>
[ApiController]
[Route("api/routes")]
[Authorize(Policy = Policies.Coordinator)]
public sealed class RoutesController(AppDbContext db, IMapboxClient mapbox, ILogger<RoutesController> logger) : ControllerBase
{
    /// <summary>Body for POST /api/routes/calculate.</summary>
    public sealed record CalculateRouteRequest([Range(1, int.MaxValue)] int DepotId, [Range(1, int.MaxValue)] int ShelterId);

    /// <summary>A depot → shelter route: distance, driving time and the encoded polyline (Mapbox polyline5).</summary>
    public sealed record RouteDto(
        Guid? Id,
        Guid? WorkflowRunId,
        int? DepotId,
        string? DepotName,
        int? ShelterId,
        string? ShelterName,
        double DistanceKm,
        double EtaMinutes,
        string Polyline,
        DateTime? CreatedAt);

    /// <summary>
    /// POST /api/routes/calculate — asks Mapbox for the driving route from a depot to a shelter now.
    /// Nothing is stored. 502 when Mapbox can't route it (timeout, error or no road).
    /// </summary>
    [HttpPost("calculate")]
    [ProducesResponseType<RouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Calculate([FromBody] CalculateRouteRequest request, CancellationToken ct)
    {
        var depot = await db.Depots.AsNoTracking().FirstOrDefaultAsync(d => d.Id == request.DepotId, ct);
        var shelter = await db.Shelters.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ShelterId, ct);
        if (depot is null || shelter is null)
            return NotFound(new { error = "LOCATION_NOT_FOUND", message = $"Depot {request.DepotId} or shelter {request.ShelterId} does not exist." });

        MapboxRouteResult? result;
        try
        {
            result = await mapbox.GetRouteAsync(depot.Longitude, depot.Latitude, shelter.Longitude, shelter.Latitude, ct);
        }
        catch (Exception ex) when (ex is TaskCanceledException or HttpRequestException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Route calculation depot {DepotId} → shelter {ShelterId} failed: {Error}", depot.Id, shelter.Id, ex.Message);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = ex is TaskCanceledException ? "MAPBOX_TIMEOUT" : "MAPBOX_HTTP_ERROR", message = "Mapbox could not be reached. Try again." });
        }

        if (result is null)
            return StatusCode(StatusCodes.Status502BadGateway,
                new { error = "NO_ROUTE", message = $"Mapbox found no driving route from {depot.Name} to {shelter.Name}." });

        return Ok(new RouteDto(null, null, depot.Id, depot.Name, shelter.Id, shelter.Name,
            result.DistanceMeters / 1000.0, result.DurationSeconds / 60.0, result.EncodedPolyline, null));
    }

    /// <summary>GET /api/routes/{id} — one stored route leg.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<RouteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var route = await Project(db.Routes.Where(r => r.Id == id)).FirstOrDefaultAsync(ct);
        return route is null
            ? NotFound(new { error = "ROUTE_NOT_FOUND", message = $"No route with id {id}." })
            : Ok(route);
    }

    /// <summary>GET /api/routes?workflowRunId= — the stored legs of a run, longest first.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RouteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery, Required] Guid workflowRunId, CancellationToken ct)
        => Ok(await Project(db.Routes.Where(r => r.WorkflowRunId == workflowRunId).OrderByDescending(r => r.DistanceMeters)).ToListAsync(ct));

    // Filter and sort on the entity before projecting: EF can't translate either after the record constructor.
    private IQueryable<RouteDto> Project(IQueryable<FloodLink.Domain.Entities.RouteEntity> routes) =>
        routes.AsNoTracking().Select(r => new RouteDto(
            r.Id,
            r.WorkflowRunId,
            r.DepotId,
            db.Depots.Where(d => d.Id == r.DepotId).Select(d => d.Name).FirstOrDefault(),
            r.ShelterId,
            db.Shelters.Where(s => s.Id == r.ShelterId).Select(s => s.Name).FirstOrDefault(),
            r.DistanceMeters / 1000.0,
            r.EstimatedDurationSeconds / 60.0,
            r.PolylineString,
            r.CreatedAt));
}
