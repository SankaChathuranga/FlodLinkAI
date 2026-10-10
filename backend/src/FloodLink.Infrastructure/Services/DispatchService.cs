using System.Text.Json;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Infrastructure.Services;

/// <summary>
/// Coordinator decisions on a validated plan (Member D's business operation): approve,
/// reject, request revision, and the field team's delivery confirmation.
/// </summary>
/// <remarks>
/// Each decision is one unit of work: the stock change, the dispatch record, the audit entry,
/// report status updates and the workflow state change are saved together in a single
/// <c>SaveChanges</c> (one database transaction). This is the only code path that commits stock.
/// </remarks>
public sealed class DispatchService(AppDbContext db, StockReservationService stock, IWorkflowRunQueue queue)
{
    /// <summary>Approves a PendingApproval plan: commits its stock and records the dispatch.</summary>
    public async Task<DispatchOutcome> ApproveAsync(Guid workflowRunId, string? notes, int? actorId, CancellationToken ct = default)
    {
        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);
        if (run is null)
            return DispatchOutcome.NotFound(workflowRunId);
        if (run.CurrentState != WorkflowState.PendingApproval)
            return DispatchOutcome.InvalidState($"Only a PendingApproval plan can be approved; current state is {run.CurrentState}.");

        var commit = await stock.StageCommitAsync(run.Id, ct);
        if (!commit.Succeeded)
            return DispatchOutcome.Conflict(commit.ErrorCode!, commit.ErrorMessage!);

        var dispatch = NewDispatch(run, DispatchDecision.Approved, notes, actorId);
        dispatch.DispatchedAt = DateTime.UtcNow;
        AddAudit(dispatch, "DispatchApproved", actorId, new { workflowRunId, notes });
        await SetReportStatusAsync(run, "InPlan", ct);
        WorkflowEngine.TryTransition(run, WorkflowState.Approved);

        return await SaveAsync(dispatch, run, ct);
    }

    /// <summary>Rejects a PendingApproval plan with a mandatory reason and releases its stock.</summary>
    public async Task<DispatchOutcome> RejectAsync(Guid workflowRunId, string? reason, int? actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return DispatchOutcome.BadRequest("REASON_REQUIRED", "A rejection reason is mandatory.");

        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);
        if (run is null)
            return DispatchOutcome.NotFound(workflowRunId);
        if (run.CurrentState != WorkflowState.PendingApproval)
            return DispatchOutcome.InvalidState($"Only a PendingApproval plan can be rejected; current state is {run.CurrentState}.");

        await stock.StageReleaseAsync(run.Id, ct);
        var dispatch = NewDispatch(run, DispatchDecision.Rejected, reason, actorId);
        AddAudit(dispatch, "DispatchRejected", actorId, new { workflowRunId, reason });
        WorkflowEngine.TryTransition(run, WorkflowState.Rejected);

        return await SaveAsync(dispatch, run, ct);
    }

    /// <summary>
    /// Sends a PendingApproval plan back for re-matching with mandatory notes. Releases its stock,
    /// stores the notes in the plan for the Matching step, and queues the run.
    /// </summary>
    public async Task<DispatchOutcome> RequestRevisionAsync(Guid workflowRunId, string? notes, int? actorId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return DispatchOutcome.BadRequest("NOTES_REQUIRED", "Revision notes are mandatory.");

        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);
        if (run is null)
            return DispatchOutcome.NotFound(workflowRunId);
        if (run.CurrentState != WorkflowState.PendingApproval)
            return DispatchOutcome.InvalidState($"Only a PendingApproval plan can be sent for revision; current state is {run.CurrentState}.");

        await stock.StageReleaseAsync(run.Id, ct);
        var dispatch = NewDispatch(run, DispatchDecision.RevisionRequested, notes, actorId);
        AddAudit(dispatch, "RevisionRequested", actorId, new { workflowRunId, notes });
        run.PlanJson = WorkflowPlan.Merge(run.PlanJson, WorkflowPlan.RevisionKey,
            new RevisionRequest(notes, actorId, DateTime.UtcNow));
        WorkflowEngine.TryTransition(run, WorkflowState.RevisionRequested);

        var outcome = await SaveAsync(dispatch, run, ct);
        if (outcome.Status == DispatchOutcomeStatus.Ok)
            await queue.EnqueueAsync(run.Id, ct);
        return outcome;
    }

    /// <summary>
    /// Records that the supplies of an Approved dispatch arrived and resolves the run's reports.
    /// The workflow stays Approved (terminal); delivery is an audit event.
    /// </summary>
    public async Task<DispatchOutcome> ConfirmDeliveryAsync(Guid workflowRunId, string? notes, string? photoReference,
        int? actorId, CancellationToken ct = default)
    {
        var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);
        if (run is null)
            return DispatchOutcome.NotFound(workflowRunId);
        if (run.CurrentState != WorkflowState.Approved)
            return DispatchOutcome.InvalidState($"Delivery confirmation requires state Approved; current state is {run.CurrentState}.");

        var dispatch = await db.Dispatches
            .Where(d => d.WorkflowRunId == workflowRunId && d.Decision == DispatchDecision.Approved)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (dispatch is null)
            return DispatchOutcome.Conflict("DISPATCH_NOT_FOUND", "No approved dispatch exists for this workflow run.");

        AddAudit(dispatch, "DeliveryConfirmed", actorId,
            new { workflowRunId, notes, photoReference, deliveredAt = DateTime.UtcNow });
        await SetReportStatusAsync(run, "Resolved", ct);

        return await SaveAsync(dispatch, run, ct);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private Dispatch NewDispatch(WorkflowRun run, DispatchDecision decision, string? notes, int? actorId)
    {
        var dispatch = new Dispatch
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = run.Id,
            Decision = decision,
            ApprovalNotes = notes,
            ApprovedById = actorId,
            CreatedAt = DateTime.UtcNow
        };
        db.Dispatches.Add(dispatch);
        return dispatch;
    }

    private void AddAudit(Dispatch dispatch, string eventType, int? actorId, object detail)
        => db.AuditTrail.Add(new AuditTrail
        {
            DispatchId = dispatch.Id,
            EventType = eventType,
            ActorId = actorId,
            EventDetailJson = JsonSerializer.Serialize(detail)
        });

    private async Task SetReportStatusAsync(WorkflowRun run, string status, CancellationToken ct)
    {
        var reportIds = run.ReportIds ?? [];
        var reports = await db.Reports
            .Where(r => r.WorkflowRunId == run.Id || reportIds.Contains(r.Id))
            .ToListAsync(ct);
        foreach (var report in reports)
            report.Status = status;
    }

    private async Task<DispatchOutcome> SaveAsync(Dispatch dispatch, WorkflowRun run, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return DispatchOutcome.Ok(dispatch, run.CurrentState);
        }
        catch (DbUpdateConcurrencyException)
        {
            return DispatchOutcome.Conflict("STOCK_CONFLICT",
                "The plan or its stock changed while this decision was being saved. Reload and try again.");
        }
    }

    /// <summary>Coordinator revision notes stored under the plan's <c>revision</c> key.</summary>
    public sealed record RevisionRequest(string Notes, int? RequestedById, DateTime RequestedAt);
}

/// <summary>How a dispatch decision ended.</summary>
public enum DispatchOutcomeStatus
{
    Ok,
    NotFound,
    InvalidState,
    BadRequest,
    Conflict
}

/// <summary>Result of a <see cref="DispatchService"/> operation; controllers map it to HTTP.</summary>
public sealed record DispatchOutcome(
    DispatchOutcomeStatus Status,
    string? ErrorCode = null,
    string? Message = null,
    Dispatch? Dispatch = null,
    WorkflowState? State = null)
{
    public static DispatchOutcome Ok(Dispatch dispatch, WorkflowState state) =>
        new(DispatchOutcomeStatus.Ok, Dispatch: dispatch, State: state);

    public static DispatchOutcome NotFound(Guid workflowRunId) =>
        new(DispatchOutcomeStatus.NotFound, "WORKFLOW_NOT_FOUND", $"No workflow run with id {workflowRunId}.");

    public static DispatchOutcome InvalidState(string message) =>
        new(DispatchOutcomeStatus.InvalidState, "INVALID_STATE", message);

    public static DispatchOutcome BadRequest(string code, string message) =>
        new(DispatchOutcomeStatus.BadRequest, code, message);

    public static DispatchOutcome Conflict(string code, string message) =>
        new(DispatchOutcomeStatus.Conflict, code, message);
}
