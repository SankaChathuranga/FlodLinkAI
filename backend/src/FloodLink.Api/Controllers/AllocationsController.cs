using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>Allocation proposals made by the Matching Agent (Member B), with their stock lifecycle status.</summary>
[ApiController]
[Route("api/allocations")]
[Authorize]
public sealed class AllocationsController(AppDbContext context) : ControllerBase
{
    /// <summary>One allocation line: which depot sends how much of what to which shelter.</summary>
    public sealed record AllocationDto(
        int Id,
        Guid WorkflowRunId,
        int DepotId,
        string? DepotName,
        int ShelterId,
        string? ShelterName,
        string ItemName,
        double Quantity,
        string Status,
        DateTime CreatedAt);

    /// <summary>
    /// Lists allocation proposals, newest first. Filter by <paramref name="status"/>
    /// (Proposed, Reserved, Committed, Released), shelter or workflow run.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<AllocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] int? shelterId,
        [FromQuery] Guid? workflowRunId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        IQueryable<AllocationProposalEntity> query = context.AllocationProposals.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(a => a.Status == status.Trim());
        if (shelterId.HasValue)
            query = query.Where(a => a.ShelterId == shelterId.Value);
        if (workflowRunId.HasValue)
            query = query.Where(a => a.WorkflowRunId == workflowRunId.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AllocationDto(
                a.Id, a.WorkflowRunId, a.DepotId, a.Depot!.Name, a.ShelterId, a.Shelter!.Name,
                a.ItemName, a.Quantity, a.Status, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(new PaginatedResult<AllocationDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }
}
