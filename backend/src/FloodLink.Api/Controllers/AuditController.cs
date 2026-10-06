using FloodLink.Infrastructure;
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
[Authorize(Roles = "Coordinator")]
public sealed class AuditController : ControllerBase
{
    private readonly AppDbContext _db;

    public AuditController(AppDbContext db) => _db = db;

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