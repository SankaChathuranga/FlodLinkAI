using FloodLink.Api.Dtos;
using FloodLink.Api.DTOs;
using FloodLink.Api.Workflows;
using System.ComponentModel.DataAnnotations;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure.Services;
using FloodLink.Infrastructure;
using FloodLink.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>Allocation proposals made by the Matching Agent (Member B), with their stock lifecycle status.</summary>
[ApiController]
[Route("api/allocations")]
[Authorize(Policy = Policies.StockReader)]
public sealed class AllocationsController(AppDbContext context, WorkflowService workflows, StockReservationService stock) : ControllerBase
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
        DateTime CreatedAt,
        double? CountedQuantity = null,
        DateTime? ConfirmedAt = null,
        string? ConfirmedBy = null,
        string? ConfirmationNote = null);

    /// <summary>Body for POST /api/allocations/{id}/confirm-stock: what was counted on the shelf.</summary>
    public sealed record ConfirmStockRequest(
        [Range(0, double.MaxValue)] double CountedQuantity,
        [StringLength(500)] string? Note);

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
        [FromQuery] bool? managedByMe,
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
        if (managedByMe == true)
        {
            var userId = User.GetUserId();
            query = query.Where(a => a.Depot!.ManagerId == userId);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AllocationDto(
                a.Id, a.WorkflowRunId, a.DepotId, a.Depot!.Name, a.ShelterId, a.Shelter!.Name,
                a.ItemName, a.Quantity, a.Status, a.CreatedAt,
                a.CountedQuantity, a.ConfirmedAt, a.ConfirmedBy != null ? a.ConfirmedBy.Name : null, a.ConfirmationNote))
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

    /// <summary>
    /// POST /api/allocations/{id}/trigger-matching — runs Matching again for the workflow run this
    /// allocation belongs to, when that run failed (e.g. after a restock). Same rules and limit as
    /// POST /api/workflows/{id}/retry, which it delegates to. To change a plan that is awaiting
    /// approval, use request-revision instead.
    /// </summary>
    [HttpPost("{id:int}/trigger-matching")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType<WorkflowRetryResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerMatching(int id, CancellationToken ct)
    {
        var runId = await context.AllocationProposals.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => (Guid?)a.WorkflowRunId)
            .FirstOrDefaultAsync(ct);
        if (runId is null)
            return NotFound(new { error = "ALLOCATION_NOT_FOUND", message = $"No allocation with id {id}." });

        var result = await workflows.RetryAsync(runId.Value, User.GetUserId(), ct);
        return result.Error is not null
            ? StatusCode(result.Error.StatusCode, new { error = result.Error.Code, message = result.Error.Message })
            : Accepted($"/api/workflows/{runId}/status", result.Value);
    }

    /// <summary>
    /// POST /api/allocations/{id}/confirm-stock — the depot manager's physical check of a reserved line before
    /// dispatch (8.05): how many were counted on the shelf, and a note. A count below the reserved quantity is
    /// reported to the coordinator as a shortfall. Changes no stock quantities.
    /// </summary>
    [HttpPost("{id:int}/confirm-stock")]
    [Authorize(Policy = Policies.DepotManager)]
    [ProducesResponseType<AllocationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmStock(int id, [FromBody] ConfirmStockRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? throw new UnauthorizedException("Token has no user id.");
        var (result, allocation) = await stock.ConfirmReservedStockAsync(
            id, request.CountedQuantity, request.Note, userId, User.IsInRole(Roles.Admin), ct);
        if (!result.Succeeded)
        {
            var body = new { error = result.ErrorCode, message = result.ErrorMessage };
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(body),
                "NOT_YOUR_DEPOT" => StatusCode(StatusCodes.Status403Forbidden, body),
                _ => Conflict(body)
            };
        }

        return Ok(await context.AllocationProposals.AsNoTracking().Where(a => a.Id == allocation!.Id)
            .Select(a => new AllocationDto(
                a.Id, a.WorkflowRunId, a.DepotId, a.Depot!.Name, a.ShelterId, a.Shelter!.Name,
                a.ItemName, a.Quantity, a.Status, a.CreatedAt,
                a.CountedQuantity, a.ConfirmedAt, a.ConfirmedBy != null ? a.ConfirmedBy.Name : null, a.ConfirmationNote))
            .SingleAsync(ct));
    }
}
