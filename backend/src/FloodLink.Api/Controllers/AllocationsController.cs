using FloodLink.Api.DTOs;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>Read-only access to allocations persisted by the Matching Agent (Member B).</summary>
[ApiController]
[Route("api/allocations")]
public sealed class AllocationsController(AppDbContext context) : ControllerBase
{
    // Statuses an AllocationProposals row can hold today. Stock is only reserved at approval (D3),
    // so Matching writes "Proposed"; extend this set when a later stage writes another status.
    private static readonly string[] KnownStatuses = ["Proposed"];

   
    /// <remarks>
    /// Filters are optional and combine with AND. A malformed <c>workflowRunId</c>, a
    /// <c>shelterId</c> below 1, or an unknown <c>status</c> returns 400.
    /// Unfulfillable needs are not stored as rows; read them from
    /// <c>GET /api/workflows/{id}/allocation-proposal</c>.
    /// </remarks>
    /// <response code="200">The matching allocations (possibly empty).</response>
    /// <response code="400">A filter value is invalid.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<AllocationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<AllocationResponseDto>>> GetAllocations(
        [FromQuery] AllocationQueryDto query,
        CancellationToken ct)
    {
        string? status = null;
        if (query.Status is not null)
        {
            status = KnownStatuses.FirstOrDefault(known =>
                string.Equals(known, query.Status.Trim(), StringComparison.OrdinalIgnoreCase));
            if (status is null)
            {
                ModelState.AddModelError(nameof(query.Status),
                    $"Unknown status '{query.Status}'. Valid values: {string.Join(", ", KnownStatuses)}.");
                return ValidationProblem(ModelState);
            }
        }

        var rows = context.AllocationProposals.AsNoTracking();
        if (query.WorkflowRunId is { } runId)
            rows = rows.Where(row => row.WorkflowRunId == runId);
        if (status is not null)
            rows = rows.Where(row => row.Status == status);
        if (query.ShelterId is { } shelterId)
            rows = rows.Where(row => row.ShelterId == shelterId);

        var allocations = await rows
            .OrderBy(row => row.Id)
            .Select(row => new AllocationResponseDto(
                row.Id,
                row.WorkflowRunId,
                row.DepotId,
                row.Depot != null ? row.Depot.Name : string.Empty,
                row.ShelterId,
                row.Shelter != null ? row.Shelter.Name : string.Empty,
                row.ItemName,
                row.Quantity,
                row.Status,
                row.CreatedAt))
            .ToListAsync(ct);

        return Ok(allocations);
    }
}
