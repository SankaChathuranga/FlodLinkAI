using System.Text.Json;
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

    private readonly AppDbContext _db;
    private readonly IWorkflowStateService _workflow;

    public DispatchController(AppDbContext db, IWorkflowStateService workflow)
    {
        _db = db;
        _workflow = workflow;
    }

    /// <summary>Approves a validated plan: commits stock, records the dispatch and audit entry.</summary>
    [HttpPost("{workflowRunId:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveAsync(Guid workflowRunId, [FromBody] ApproveRequest? request, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        if (run.CurrentState != WorkflowState.PendingApproval)
        {
            return Conflict(new
            {
                error = "INVALID_STATE",
                message = $"Only a PendingApproval plan can be approved; current state is {run.CurrentState}."
            });
        }

        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = run.Id,
            Decision = DispatchDecision.Approved,
            ApprovalNotes = request?.Notes,
            DispatchedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.Dispatches.Add(dispatch);
        _db.AuditTrail.Add(new AuditTrail
        {
            DispatchId = dispatch.Id,
            EventType = "DispatchApproved",
            EventDetailJson = JsonSerializer.Serialize(new { workflowRunId, notes = dispatch.ApprovalNotes })
        });

        var transition = await _workflow.TryTransitionAsync(run.Id, WorkflowState.Approved, ct);
        if (!transition.Succeeded)
        {
            return Conflict(new { error = transition.ErrorCode, message = "Approve failed to advance the workflow state." });
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new DispatchResponse(dispatch.Id, run.Id, dispatch.Decision, dispatch.ApprovalNotes, dispatch.DispatchedAt, WorkflowState.Approved));
    }

    /// <summary>Rejects a plan with a mandatory reason; records the rejection.</summary>
    [HttpPost("{workflowRunId:guid}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAsync(Guid workflowRunId, [FromBody] RejectRequest? request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new { error = "REASON_REQUIRED", message = "A rejection reason is mandatory." });
        }

        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        if (run.CurrentState != WorkflowState.PendingApproval)
        {
            return Conflict(new
            {
                error = "INVALID_STATE",
                message = $"Only a PendingApproval plan can be rejected; current state is {run.CurrentState}."
            });
        }

        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = run.Id,
            Decision = DispatchDecision.Rejected,
            ApprovalNotes = request.Reason,
            CreatedAt = DateTime.UtcNow
        };

        _db.Dispatches.Add(dispatch);
        _db.AuditTrail.Add(new AuditTrail
        {
            DispatchId = dispatch.Id,
            EventType = "DispatchRejected",
            EventDetailJson = JsonSerializer.Serialize(new { workflowRunId, reason = request.Reason })
        });

        var transition = await _workflow.TryTransitionAsync(run.Id, WorkflowState.Rejected, ct);
        if (!transition.Succeeded)
        {
            return Conflict(new { error = transition.ErrorCode, message = "Reject failed to advance the workflow state." });
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new DispatchResponse(dispatch.Id, run.Id, dispatch.Decision, dispatch.ApprovalNotes, dispatch.DispatchedAt, WorkflowState.Rejected));
    }

    /// <summary>Sends a plan back for re-matching with mandatory coordinator notes.</summary>
    [HttpPost("{workflowRunId:guid}/request-revision")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRevisionAsync(Guid workflowRunId, [FromBody] RevisionRequest? request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Notes))
        {
            return BadRequest(new { error = "NOTES_REQUIRED", message = "Revision notes are mandatory." });
        }

        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        if (run.CurrentState != WorkflowState.PendingApproval)
        {
            return Conflict(new
            {
                error = "INVALID_STATE",
                message = $"Only a PendingApproval plan can be sent for revision; current state is {run.CurrentState}."
            });
        }

        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = run.Id,
            Decision = DispatchDecision.RevisionRequested,
            ApprovalNotes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _db.Dispatches.Add(dispatch);
        _db.AuditTrail.Add(new AuditTrail
        {
            DispatchId = dispatch.Id,
            EventType = "RevisionRequested",
            EventDetailJson = JsonSerializer.Serialize(new { workflowRunId, notes = request.Notes })
        });

        var transition = await _workflow.TryTransitionAsync(run.Id, WorkflowState.RevisionRequested, ct);
        if (!transition.Succeeded)
        {
            return Conflict(new { error = transition.ErrorCode, message = "Request-revision failed to advance the workflow state." });
        }

        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            dispatchId = dispatch.Id,
            workflowRunId,
            decision = dispatch.Decision,
            notes = dispatch.ApprovalNotes,
            state = WorkflowState.RevisionRequested,
            loopsBackTo = WorkflowState.Matching
        });
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

    private sealed record DispatchResponse(
        Guid DispatchId,
        Guid WorkflowRunId,
        DispatchDecision Decision,
        string? ApprovalNotes,
        DateTime? DispatchedAt,
        WorkflowState State);
}