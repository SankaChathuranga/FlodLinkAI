using FloodLink.Api.DTOs;
using FloodLink.Infrastructure;
using FloodLink.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>
/// Audit-trail endpoints (Member D — Ijini). Append-only compliance view proving
/// who authorized a dispatch and when. Coordinator-only.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Policy = Policies.Coordinator)]
public sealed class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db) => _db = db;

    /// <summary>One audit event with the acting user's name and the run its dispatch belongs to.</summary>
    public sealed record AuditEventDto(
        Guid Id,
        string EventType,
        string? EventDetailJson,
        Guid? DispatchId,
        Guid? WorkflowRunId,
        int? ActorId,
        string? ActorName,
        DateTime CreatedAt);

    /// <summary>
    /// GET /api/audit?eventType=&amp;actorId=&amp;dispatchId=&amp;workflowRunId=&amp;dateFrom=&amp;dateTo=&amp;page=&amp;pageSize=
    /// The whole audit trail, newest first, filtered by event type (e.g. DispatchApproved), acting
    /// user, dispatch, workflow run or date range. <c>actorId=0</c> keeps system events (no actor).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PaginatedResult<AuditEventDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<AuditEventDto>>> ListAsync(
        [FromQuery] string? eventType,
        [FromQuery] int? actorId,
        [FromQuery] Guid? dispatchId,
        [FromQuery] Guid? workflowRunId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var query = _db.AuditTrail.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            var type = eventType.Trim();
            query = query.Where(a => a.EventType == type);
        }
        if (actorId == 0)
            query = query.Where(a => a.ActorId == null);
        else if (actorId.HasValue)
            query = query.Where(a => a.ActorId == actorId.Value);
        if (dispatchId.HasValue)
            query = query.Where(a => a.DispatchId == dispatchId.Value);
        if (workflowRunId.HasValue)
            query = query.Where(a => a.Dispatch != null && a.Dispatch.WorkflowRunId == workflowRunId.Value);
        if (dateFrom.HasValue)
            query = query.Where(a => a.CreatedAt >= dateFrom.Value.AsUtc());
        if (dateTo.HasValue)
            query = query.Where(a => a.CreatedAt <= dateTo.Value.AsUtc());

        return Ok(await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => new AuditEventDto(
                a.Id, a.EventType, a.EventDetailJson, a.DispatchId,
                a.Dispatch != null ? a.Dispatch.WorkflowRunId : null,
                a.ActorId, a.Actor != null ? a.Actor.Name : null, a.CreatedAt))
            .ToPageAsync(page, pageSize, ct));
    }

    /// <summary>GET /api/audit/event-types — the distinct event types recorded so far (for filters).</summary>
    [HttpGet("event-types")]
    [ProducesResponseType<IReadOnlyList<string>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> EventTypesAsync(CancellationToken ct)
        => Ok(await _db.AuditTrail.AsNoTracking().Select(a => a.EventType).Distinct().OrderBy(t => t).ToListAsync(ct));

    /// <summary>Returns the full audit trail for one dispatch (compliance view).</summary>
    [HttpGet("{dispatchId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuditAsync(Guid dispatchId, CancellationToken ct)
    {
        var dispatch = await _db.Dispatches.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dispatchId, ct);

        if (dispatch is null)
        {
            return NotFound(new { error = "DISPATCH_NOT_FOUND", message = $"No dispatch with id {dispatchId}." });
        }

        var events = await _db.AuditTrail.AsNoTracking()
            .Where(a => a.DispatchId == dispatchId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.EventType, a.EventDetailJson, a.ActorId, a.CreatedAt })
            .ToListAsync(ct);

        return Ok(new { dispatchId, dispatch.Decision, events });
    }
}