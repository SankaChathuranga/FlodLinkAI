using FloodLink.Api.Auth;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>
/// Coordinator approval and dispatch endpoints (Member D — Ijini).
/// Enforces the human-in-the-loop checkpoint: a plan can only be dispatched after
/// validation passed and an authorized coordinator explicitly approves it.
/// Coordinator-only.
/// </summary>
[ApiController]
[Route("api/dispatches")]
[Authorize(Roles = "Coordinator")]
public sealed class DispatchController : ControllerBase
{
    /// <summary>Body contract for POST /api/dispatches/{id}/approve.</summary>
    public sealed record ApproveRequest(string? Notes);

    /// <summary>Body contract for POST /api/dispatches/{id}/reject (Reason required).</summary>
    public sealed record RejectRequest(string Reason);

    /// <summary>Body contract for POST /api/dispatches/{id}/request-revision (Notes required).</summary>
    public sealed record RevisionRequest(string Notes);

    /// <summary>Body contract for POST /api/dispatches/{id}/confirm-delivery (Notes and PhotoReference optional).</summary>
    public sealed record ConfirmDeliveryRequest(string? Notes, string? PhotoReference);

    private readonly AppDbContext _db;
    private readonly DispatchService _dispatches;

    public DispatchController(AppDbContext db, DispatchService dispatches)
    {
        _db = db;
        _dispatches = dispatches;
    }

    /// <summary>Approves a validated plan: commits its reserved stock, records the dispatch and audit entry.</summary>
    [HttpPost("{workflowRunId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveAsync(Guid workflowRunId, [FromBody] ApproveRequest? request, CancellationToken ct)
    {
        var outcome = await _dispatches.ApproveAsync(workflowRunId, request?.Notes, User.GetUserId(), ct);
        return outcome.Status == DispatchOutcomeStatus.Ok ? Ok(ToResponse(outcome)) : Error(outcome);
    }

    /// <summary>Rejects a plan with a mandatory reason; releases its stock and records the rejection.</summary>
    [HttpPost("{workflowRunId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAsync(Guid workflowRunId, [FromBody] RejectRequest? request, CancellationToken ct)
    {
        var outcome = await _dispatches.RejectAsync(workflowRunId, request?.Reason, User.GetUserId(), ct);
        return outcome.Status == DispatchOutcomeStatus.Ok ? Ok(ToResponse(outcome)) : Error(outcome);
    }

    /// <summary>
    /// Sends a plan back for re-matching with mandatory coordinator notes. Its stock is released
    /// and the run is queued; it returns to PendingApproval with a new proposal.
    /// </summary>
    [HttpPost("{workflowRunId:guid}/request-revision")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRevisionAsync(Guid workflowRunId, [FromBody] RevisionRequest? request, CancellationToken ct)
    {
        var outcome = await _dispatches.RequestRevisionAsync(workflowRunId, request?.Notes, User.GetUserId(), ct);
        if (outcome.Status != DispatchOutcomeStatus.Ok)
            return Error(outcome);

        return Ok(new
        {
            dispatchId = outcome.Dispatch!.Id,
            workflowRunId,
            decision = outcome.Dispatch.Decision,
            notes = outcome.Dispatch.ApprovalNotes,
            state = WorkflowState.RevisionRequested,
            loopsBackTo = WorkflowState.Matching
        });
    }

    /// <summary>
    /// Returns the current dispatch status for a workflow run (decision, reason, state).
    /// </summary>
    [HttpGet("by-workflow/{workflowRunId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByWorkflowAsync(Guid workflowRunId, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        var dispatch = await _db.Dispatches.AsNoTracking()
            .Where(d => d.WorkflowRunId == workflowRunId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new { d.Id, d.Decision, d.ApprovalNotes, d.DispatchedAt, d.CreatedAt })
            .FirstOrDefaultAsync(ct);

        var deliveryEvent = await _db.AuditTrail.AsNoTracking()
            .Where(a => a.EventType == "DeliveryConfirmed" && a.Dispatch != null && a.Dispatch.WorkflowRunId == workflowRunId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.CreatedAt })
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            workflowRunId,
            state = run.CurrentState,
            decision = dispatch?.Decision,
            notes = dispatch?.ApprovalNotes,
            dispatchedAt = dispatch?.DispatchedAt,
            isDelivered = deliveryEvent is not null,
            deliveredAt = deliveryEvent?.CreatedAt
        });
    }

    /// <summary>
    /// Field-worker delivery confirmation. Only an Approved dispatch can be
    /// confirmed. The spec fixes exactly 9 workflow states, so delivery does not
    /// advance the state machine — it is recorded as an append-only audit event
    /// ("DeliveryConfirmed") that the dispatch-status endpoint surfaces as
    /// isDelivered / deliveredAt, and the run's reports become Resolved. State stays Approved (terminal).
    /// </summary>
    [HttpPost("{workflowRunId:guid}/confirm-delivery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmDeliveryAsync(Guid workflowRunId, [FromBody] ConfirmDeliveryRequest? request, CancellationToken ct)
    {
        var outcome = await _dispatches.ConfirmDeliveryAsync(
            workflowRunId, request?.Notes, request?.PhotoReference, User.GetUserId(), ct);
        if (outcome.Status != DispatchOutcomeStatus.Ok)
            return Error(outcome);

        return Ok(new { workflowRunId, state = WorkflowState.Approved, isDelivered = true, notes = request?.Notes });
    }

    /// <summary>
    /// Returns the approval queue: workflow runs awaiting a coordinator decision.
    /// This powers the coordinator's "Approval Queue" screen (Member D — React).
    /// Only runs in PendingApproval appear — they have already passed validation.
    /// </summary>
    [HttpGet("approval-queue")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApprovalQueueAsync(CancellationToken ct)
    {
        var items = await _db.WorkflowRuns.AsNoTracking()
            .Where(r => r.CurrentState == WorkflowState.PendingApproval)
            .OrderBy(r => r.CreatedAt)
            .Select(r => new
            {
                workflowRunId = r.Id,
                objective = r.Objective,
                createdAt = r.CreatedAt,
                state = r.CurrentState
            })
            .ToListAsync(ct);

        return Ok(new { items });
    }

    /// <summary>Lists, filters and paginates dispatch history.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        IQueryable<Dispatch> query = _db.Dispatches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DispatchDecision>(status, ignoreCase: true, out var decision))
            {
                return BadRequest(new { error = "INVALID_STATUS", message = $"Unknown dispatch status '{status}'." });
            }
            query = query.Where(d => d.Decision == decision);
        }

        if (dateFrom.HasValue) query = query.Where(d => d.CreatedAt >= dateFrom.Value);
        if (dateTo.HasValue) query = query.Where(d => d.CreatedAt <= dateTo.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.WorkflowRunId,
                d.Decision,
                d.ApprovalNotes,
                d.ApprovedById,
                d.DispatchedAt,
                d.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }

    /// <summary>Reporting/analytics: totals, approval turnaround and rejection reasons.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SummaryAsync(CancellationToken ct)
    {
        var dispatches = await _db.Dispatches.AsNoTracking().ToListAsync(ct);

        var approvedMinutes = dispatches
            .Where(d => d.Decision == DispatchDecision.Approved && d.DispatchedAt.HasValue)
            .Select(d => (d.DispatchedAt!.Value - d.CreatedAt).TotalMinutes)
            .ToList();

        var rejectionReasons = dispatches
            .Where(d => d.Decision == DispatchDecision.Rejected && d.ApprovalNotes is not null)
            .GroupBy(d => d.ApprovalNotes!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new { reason = g.Key, count = g.Count() })
            .ToList();

        return Ok(new
        {
            total = dispatches.Count,
            approved = dispatches.Count(d => d.Decision == DispatchDecision.Approved),
            rejected = dispatches.Count(d => d.Decision == DispatchDecision.Rejected),
            revisionsRequested = dispatches.Count(d => d.Decision == DispatchDecision.RevisionRequested),
            avgApprovalMinutes = approvedMinutes.Count == 0 ? null : (double?)approvedMinutes.Average(),
            topRejectionReasons = rejectionReasons
        });
    }

    private static DispatchResponse ToResponse(DispatchOutcome outcome) => new(
        outcome.Dispatch!.Id,
        outcome.Dispatch.WorkflowRunId,
        outcome.Dispatch.Decision,
        outcome.Dispatch.ApprovalNotes,
        outcome.Dispatch.ApprovedById,
        outcome.Dispatch.DispatchedAt,
        outcome.State!.Value);

    private IActionResult Error(DispatchOutcome outcome)
    {
        var body = new { error = outcome.ErrorCode, message = outcome.Message };
        return outcome.Status switch
        {
            DispatchOutcomeStatus.NotFound => NotFound(body),
            DispatchOutcomeStatus.BadRequest => BadRequest(body),
            _ => Conflict(body)
        };
    }

    private sealed record DispatchResponse(
        Guid DispatchId,
        Guid WorkflowRunId,
        DispatchDecision Decision,
        string? ApprovalNotes,
        int? ApprovedById,
        DateTime? DispatchedAt,
        WorkflowState State);
}