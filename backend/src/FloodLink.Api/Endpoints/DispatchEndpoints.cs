using System.Text.Json;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Endpoints;

/// <summary>
/// Coordinator approval, dispatch and audit endpoints (Member D — Ijini).
/// Enforces the human-in-the-loop checkpoint: a plan can only be dispatched after
/// validation passed and an authorized coordinator explicitly approves it.
/// </summary>
public static class DispatchEndpoints
{
    /// <summary>Body contract for POST /api/dispatches/{id}/approve.</summary>
    public sealed record ApproveRequest(string? Notes);

    /// <summary>Body contract for POST /api/dispatches/{id}/reject (Reason required).</summary>
    public sealed record RejectRequest(string Reason);

    /// <summary>Body contract for POST /api/dispatches/{id}/request-revision (Notes required).</summary>
    public sealed record RevisionRequest(string Notes);

    /// <summary>Wires the /api/dispatches and /api/audit route groups.</summary>
    public static void MapDispatchEndpoints(this IEndpointRouteBuilder app)
    {
        var dispatches = app.MapGroup("/api/dispatches");

        dispatches.MapPost("/{workflowRunId:guid}/approve", ApproveAsync)
            .WithName("ApproveDispatch")
            .WithTags("Dispatches");

        dispatches.MapPost("/{workflowRunId:guid}/reject", RejectAsync)
            .WithName("RejectDispatch")
            .WithTags("Dispatches");

        dispatches.MapPost("/{workflowRunId:guid}/request-revision", RequestRevisionAsync)
            .WithName("RequestRevision")
            .WithTags("Dispatches");

        dispatches.MapGet("/", ListAsync)
            .WithName("ListDispatches")
            .WithTags("Dispatches");

        dispatches.MapGet("/summary", SummaryAsync)
            .WithName("DispatchSummary")
            .WithTags("Dispatches");

        var audit = app.MapGroup("/api/audit");
        audit.MapGet("/{dispatchId:guid}", GetAuditAsync)
            .WithName("GetAuditTrail")
            .WithTags("Audit");
    }

    /// <summary>Approves a validated plan: commits stock, records the dispatch and audit entry.</summary>
    private static async Task<IResult> ApproveAsync(
        Guid workflowRunId,
        ApproveRequest? request,
        AppDbContext db,
        IWorkflowStateService workflow,
        CancellationToken ct)
    {
        var run = await db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return Results.NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        if (run.CurrentState != WorkflowState.PendingApproval)
        {
            return Results.Conflict(new
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

        db.Dispatches.Add(dispatch);
        db.AuditTrail.Add(new AuditTrail
        {
            DispatchId = dispatch.Id,
            EventType = "DispatchApproved",
            EventDetailJson = JsonSerializer.Serialize(new { workflowRunId, notes = dispatch.ApprovalNotes })
        });

        var transition = await workflow.TryTransitionAsync(run.Id, WorkflowState.Approved, ct);
        if (!transition.Succeeded)
        {
            return Results.Conflict(new { error = transition.ErrorCode, message = "Approve failed to advance the workflow state." });
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new DispatchResponse(dispatch.Id, run.Id, dispatch.Decision, dispatch.ApprovalNotes, dispatch.DispatchedAt, WorkflowState.Approved));
    }

    /// <summary>Rejects a plan with a mandatory reason; releases nothing and records the rejection.</summary>
    private static async Task<IResult> RejectAsync(
        Guid workflowRunId,
        RejectRequest? request,
        AppDbContext db,
        IWorkflowStateService workflow,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Reason))
        {
            return Results.BadRequest(new { error = "REASON_REQUIRED", message = "A rejection reason is mandatory." });
        }

        var run = await db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return Results.NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        if (run.CurrentState != WorkflowState.PendingApproval)
        {
            return Results.Conflict(new
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

        db.Dispatches.Add(dispatch);
        db.AuditTrail.Add(new AuditTrail
        {
            DispatchId = dispatch.Id,
            EventType = "DispatchRejected",
            EventDetailJson = JsonSerializer.Serialize(new { workflowRunId, reason = request.Reason })
        });

        var transition = await workflow.TryTransitionAsync(run.Id, WorkflowState.Rejected, ct);
        if (!transition.Succeeded)
        {
            return Results.Conflict(new { error = transition.ErrorCode, message = "Reject failed to advance the workflow state." });
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new DispatchResponse(dispatch.Id, run.Id, dispatch.Decision, dispatch.ApprovalNotes, dispatch.DispatchedAt, WorkflowState.Rejected));
    }

    /// <summary>Sends a plan back for re-matching with mandatory coordinator notes.</summary>
    private static async Task<IResult> RequestRevisionAsync(
        Guid workflowRunId,
        RevisionRequest? request,
        AppDbContext db,
        IWorkflowStateService workflow,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Notes))
        {
            return Results.BadRequest(new { error = "NOTES_REQUIRED", message = "Revision notes are mandatory." });
        }

        var run = await db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return Results.NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        if (run.CurrentState != WorkflowState.PendingApproval)
        {
            return Results.Conflict(new
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

        db.Dispatches.Add(dispatch);
        db.AuditTrail.Add(new AuditTrail
        {
            DispatchId = dispatch.Id,
            EventType = "RevisionRequested",
            EventDetailJson = JsonSerializer.Serialize(new { workflowRunId, notes = request.Notes })
        });

        var transition = await workflow.TryTransitionAsync(run.Id, WorkflowState.RevisionRequested, ct);
        if (!transition.Succeeded)
        {
            return Results.Conflict(new { error = transition.ErrorCode, message = "Request-revision failed to advance the workflow state." });
        }

        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            dispatchId = dispatch.Id,
            workflowRunId,
            decision = dispatch.Decision,
            notes = dispatch.ApprovalNotes,
            state = WorkflowState.RevisionRequested,
            loopsBackTo = WorkflowState.Matching
        });
    }

    /// <summary>Lists, filters and paginates dispatch history.</summary>
    private static async Task<IResult> ListAsync(
        string? status,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        AppDbContext db,
        CancellationToken ct)
    {
        if (page < 1) page = 1;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        IQueryable<Dispatch> query = db.Dispatches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DispatchDecision>(status, ignoreCase: true, out var decision))
            {
                return Results.BadRequest(new { error = "INVALID_STATUS", message = $"Unknown dispatch status '{status}'." });
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

        return Results.Ok(new { page, pageSize, total, items });
    }

    /// <summary>Reporting/analytics: totals, approval turnaround and rejection reasons.</summary>
    private static async Task<IResult> SummaryAsync(AppDbContext db, CancellationToken ct)
    {
        var dispatches = await db.Dispatches.AsNoTracking().ToListAsync(ct);

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

        return Results.Ok(new
        {
            total = dispatches.Count,
            approved = dispatches.Count(d => d.Decision == DispatchDecision.Approved),
            rejected = dispatches.Count(d => d.Decision == DispatchDecision.Rejected),
            revisionsRequested = dispatches.Count(d => d.Decision == DispatchDecision.RevisionRequested),
            avgApprovalMinutes = approvedMinutes.Count == 0 ? null : (double?)approvedMinutes.Average(),
            topRejectionReasons = rejectionReasons
        });
    }

    /// <summary>Returns the full audit trail for one dispatch (compliance view).</summary>
    private static async Task<IResult> GetAuditAsync(
        Guid dispatchId,
        AppDbContext db,
        CancellationToken ct)
    {
        var dispatch = await db.Dispatches.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == dispatchId, ct);

        if (dispatch is null)
        {
            return Results.NotFound(new { error = "DISPATCH_NOT_FOUND", message = $"No dispatch with id {dispatchId}." });
        }

        var events = await db.AuditTrail.AsNoTracking()
            .Where(a => a.DispatchId == dispatchId)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new { a.Id, a.EventType, a.EventDetailJson, a.ActorId, a.CreatedAt })
            .ToListAsync(ct);

        return Results.Ok(new { dispatchId, dispatch.Decision, events });
    }

    private sealed record DispatchResponse(
        Guid DispatchId,
        Guid WorkflowRunId,
        DispatchDecision Decision,
        string? ApprovalNotes,
        DateTime? DispatchedAt,
        WorkflowState State);
}