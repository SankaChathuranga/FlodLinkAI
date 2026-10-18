using System.Text.Json;
using FloodLink.Api.Auth;
using FloodLink.Api.Dtos;
using FloodLink.Api.DTOs;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Route = FloodLink.Contracts.Route;

namespace FloodLink.Api.Workflows;

/// <summary>Why a <see cref="WorkflowService"/> request was refused; the controller maps it to HTTP.</summary>
public sealed record WorkflowError(int StatusCode, string Code, string Message)
{
    public static WorkflowError BadRequest(string code, string message) => new(StatusCodes.Status400BadRequest, code, message);
    public static WorkflowError NotFound(Guid id) => new(StatusCodes.Status404NotFound, "WORKFLOW_NOT_FOUND", $"No workflow run with id {id}.");
    public static WorkflowError Conflict(string code, string message) => new(StatusCodes.Status409Conflict, code, message);
}

/// <summary>A value, or the reason there is none.</summary>
public sealed record WorkflowResult<T>(T? Value, WorkflowError? Error)
{
    public static implicit operator WorkflowResult<T>(T value) => new(value, null);
    public static implicit operator WorkflowResult<T>(WorkflowError error) => new(default, error);
}

/// <summary>
/// Workflow runs (Member C's business operation): start a run for a set of reports, list and
/// read runs, read the execution trace, report a lightweight status, and retry a failed stage.
/// The background runner (<see cref="WorkflowRunnerService"/>) advances queued runs.
/// </summary>
public sealed class WorkflowService(
    AppDbContext db,
    WorkflowOrchestrator orchestrator,
    IWorkflowRunQueue queue,
    IConfiguration configuration,
    ILogger<WorkflowService> logger)
{
    private static readonly WorkflowState[] FinishedStates =
        [WorkflowState.Approved, WorkflowState.Rejected, WorkflowState.Failed];

    /// <summary>Retries allowed per run (<c>Workflow:MaxRetries</c>, default 3).</summary>
    public int MaxRetries { get; } = configuration.GetValue("Workflow:MaxRetries", defaultValue: 3);

    /// <summary>
    /// Creates a run in Triage for <paramref name="reportIds"/> (or, when none are given, every New or
    /// Triaged report not already in an active run) and queues it.
    /// </summary>
    public async Task<WorkflowResult<WorkflowRun>> StartAsync(
        string objective, IReadOnlyCollection<int>? reportIds, int? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(objective))
            return WorkflowError.BadRequest("OBJECTIVE_REQUIRED", "Objective is required.");

        List<Report> reports;
        if (reportIds is { Count: > 0 })
        {
            var ids = reportIds.Distinct().ToList();
            reports = await db.Reports.Include(r => r.WorkflowRun)
                .Where(r => ids.Contains(r.Id))
                .ToListAsync(ct);

            var missing = ids.Except(reports.Select(r => r.Id)).ToList();
            if (missing.Count > 0)
                return WorkflowError.BadRequest("REPORT_NOT_FOUND", $"Reports not found: {string.Join(", ", missing)}.");

            var resolved = reports.Where(r => r.Status == "Resolved").Select(r => r.Id).ToList();
            if (resolved.Count > 0)
                return WorkflowError.BadRequest("REPORT_RESOLVED", $"Reports already resolved: {string.Join(", ", resolved)}.");

            var busy = reports.Where(IsInActiveRun).Select(r => r.Id).ToList();
            if (busy.Count > 0)
                return WorkflowError.Conflict("REPORT_IN_ACTIVE_WORKFLOW", $"Reports already in an active workflow: {string.Join(", ", busy)}.");
        }
        else
        {
            reports = (await db.Reports.Include(r => r.WorkflowRun)
                    .Where(r => r.Status == "New" || r.Status == "Triaged")
                    .ToListAsync(ct))
                .Where(r => !IsInActiveRun(r))
                .ToList();
            if (reports.Count == 0)
                return WorkflowError.BadRequest("NO_REPORTS", "There are no open reports to plan for.");
        }

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            Objective = objective.Trim(),
            CurrentState = WorkflowState.Triage,
            ReportIds = reports.Select(r => r.Id).OrderBy(id => id).ToList()
        };
        db.WorkflowRuns.Add(run);
        foreach (var report in reports)
            report.WorkflowRunId = run.Id;
        await db.SaveChangesAsync(ct);

        await queue.EnqueueAsync(run.Id, ct);
        logger.LogInformation("Workflow {WorkflowRunId} started by user {UserId} for {ReportCount} reports",
            run.Id, userId, run.ReportIds.Count);
        return run;
    }

    /// <summary>One page of runs. <paramref name="sort"/>: createdAt, updatedAt, -updatedAt; default newest first.</summary>
    public async Task<WorkflowResult<PaginatedResult<WorkflowRunSummary>>> ListAsync(
        string? state, string? search, string? sort, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        IQueryable<WorkflowRun> query = db.WorkflowRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(state))
        {
            if (!Enum.TryParse<WorkflowState>(state, ignoreCase: true, out var parsed))
                return WorkflowError.BadRequest("INVALID_STATE", $"Unknown workflow state '{state}'.");
            query = query.Where(r => r.CurrentState == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(r => r.Objective.ToLower().Contains(term));
        }

        query = sort?.Trim() switch
        {
            "createdAt" => query.OrderBy(r => r.CreatedAt),
            "updatedAt" => query.OrderBy(r => r.UpdatedAt),
            "-updatedAt" => query.OrderByDescending(r => r.UpdatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        // Select only the summary columns: a run's PlanJson can be large and the list never needs it.
        var runs = await query
            .Select(r => new
            {
                r.Id, r.Objective, r.CurrentState, r.FailedAtState, r.FailureReason,
                ReportCount = r.ReportIds == null ? 0 : r.ReportIds.Count,
                r.RetryCount, r.CreatedAt, r.UpdatedAt
            })
            .ToPageAsync(page, pageSize, ct);
        return new PaginatedResult<WorkflowRunSummary>
        {
            Items = runs.Items.Select(r => new WorkflowRunSummary(
                r.Id, r.Objective, r.CurrentState.ToString(), ProgressPercent(r.CurrentState),
                r.FailedAtState?.ToString(), r.FailureReason, r.ReportCount, r.RetryCount, MaxRetries,
                r.CreatedAt, r.UpdatedAt)).ToList(),
            Page = runs.Page,
            PageSize = runs.PageSize,
            TotalItems = runs.TotalItems,
            TotalPages = runs.TotalPages
        };
    }

    /// <summary>The run with its full plan, or null.</summary>
    public async Task<WorkflowRunResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var run = await db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? null : ToResponse(run);
    }

    /// <summary>The run's execution log in order, or null when the run doesn't exist.</summary>
    public async Task<IReadOnlyList<WorkflowLogEntry>?> GetLogsAsync(Guid id, bool includeInput, CancellationToken ct = default)
    {
        if (!await db.WorkflowRuns.AnyAsync(r => r.Id == id, ct))
            return null;

        var logs = await db.AgentExecutionLogs.AsNoTracking()
            .Where(l => l.WorkflowRunId == id)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);

        return logs.Select(l => new WorkflowLogEntry(
            l.Id, l.AgentName, l.Status, l.IsRetry, l.DurationMs, l.CreatedAt, l.ErrorMessage,
            ParseJson(l.ToolCallsJson), ParseJson(l.OutputJson), includeInput ? ParseJson(l.InputJson) : null)).ToList();
    }

    /// <summary>
    /// Lightweight status for polling. When <paramref name="ownerId"/> is set, a run with none of that
    /// user's reports is treated as not found. Null when not found.
    /// </summary>
    public async Task<WorkflowStatusResponse?> GetStatusAsync(Guid id, int? ownerId, CancellationToken ct = default)
    {
        if (ownerId.HasValue && !await db.UserOwnsRunAsync(id, ownerId, ct))
            return null;

        var run = await db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
            return null;

        var dispatch = await db.Dispatches.AsNoTracking()
            .Where(d => d.WorkflowRunId == id)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var delivered = dispatch is not null && await db.AuditTrail.AsNoTracking()
            .AnyAsync(a => a.DispatchId == dispatch.Id && a.EventType == "DeliveryConfirmed", ct);
        var route = WorkflowPlan.TryRead<Route>(run.PlanJson, WorkflowPlan.RouteKey);
        var legs = route is { Legs.Count: > 0 } ? await RouteLegsAsync(route, ct) : null;

        return new WorkflowStatusResponse(
            run.Id,
            run.CurrentState.ToString(),
            StageLabel(run.CurrentState),
            ProgressPercent(run.CurrentState),
            FinishedStates.Contains(run.CurrentState),
            run.CurrentState == WorkflowState.PendingApproval,
            run.FailedAtState?.ToString(),
            run.FailureReason,
            dispatch?.Decision.ToString(),
            dispatch?.ApprovalNotes,
            dispatch?.CreatedAt,
            route?.DistanceKm,
            route?.EtaMinutes,
            delivered,
            run.UpdatedAt,
            legs);
    }

    // The route's legs with both end points, so a client can draw them on a map.
    private async Task<IReadOnlyList<StatusRouteLeg>> RouteLegsAsync(Route route, CancellationToken ct)
    {
        var depotIds = route.Legs.Select(l => l.DepotId).Distinct().ToList();
        var shelterIds = route.Legs.Select(l => l.ShelterId).Distinct().ToList();
        var depots = await db.Depots.AsNoTracking().Where(d => depotIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, ct);
        var shelters = await db.Shelters.AsNoTracking().Where(s => shelterIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        return route.Legs
            .Where(l => depots.ContainsKey(l.DepotId) && shelters.ContainsKey(l.ShelterId))
            .Select(l =>
            {
                var depot = depots[l.DepotId];
                var shelter = shelters[l.ShelterId];
                return new StatusRouteLeg(depot.Id, depot.Name, depot.Latitude, depot.Longitude,
                    shelter.Id, shelter.Name, shelter.Latitude, shelter.Longitude, l.DistanceKm, l.EtaMinutes, l.Polyline);
            })
            .ToList();
    }

    /// <summary>
    /// Re-runs the stage a failed run stopped in, within <see cref="MaxRetries"/>, and queues it.
    /// </summary>
    public async Task<WorkflowResult<WorkflowRetryResponse>> RetryAsync(Guid id, int? userId, CancellationToken ct = default)
    {
        var run = await db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
            return WorkflowError.NotFound(id);

        // A report that has since joined another run must not be planned twice.
        var reportIds = run.ReportIds ?? [];
        var reassigned = await db.Reports.AsNoTracking()
            .Where(r => reportIds.Contains(r.Id) && r.WorkflowRunId != id)
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (reassigned.Count > 0)
            return WorkflowError.Conflict("REPORTS_REASSIGNED", $"Reports now belong to another workflow: {string.Join(", ", reassigned)}.");

        var outcome = await orchestrator.PrepareRetryAsync(id, MaxRetries, ct);
        WorkflowError? refused = outcome switch
        {
            RetryOutcome.NotFound => WorkflowError.NotFound(id),
            RetryOutcome.NotFailed => WorkflowError.Conflict("NOT_FAILED", $"Only a failed run can be retried; current state is {run.CurrentState}."),
            RetryOutcome.LimitReached => WorkflowError.Conflict("RETRY_LIMIT_REACHED", $"This run has already been retried {run.RetryCount} times (limit {MaxRetries})."),
            RetryOutcome.NotRetryable => WorkflowError.Conflict("NOT_RETRYABLE", "The run did not fail in an agent stage, so there is nothing to re-run."),
            _ => null
        };
        if (refused is not null)
        {
            logger.LogInformation("Retry of workflow {WorkflowRunId} by user {UserId} refused: {Code}", id, userId, refused.Code);
            return refused;
        }

        await queue.EnqueueAsync(id, ct);
        logger.LogInformation("Workflow {WorkflowRunId} retried by user {UserId} at {State} (retry {RetryCount}/{MaxRetries})",
            id, userId, run.FailedAtState, run.RetryCount + 1, MaxRetries);
        return new WorkflowRetryResponse(id, run.FailedAtState?.ToString(), run.RetryCount + 1);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsInActiveRun(Report report)
        => report.WorkflowRun is not null && !FinishedStates.Contains(report.WorkflowRun.CurrentState);

    public static WorkflowRunResponse ToResponse(WorkflowRun run) => new(
        run.Id,
        run.Objective,
        run.CurrentState.ToString(),
        run.FailedAtState?.ToString(),
        run.FailureReason,
        run.ReportIds ?? [],
        run.RetryCount,
        run.CreatedAt,
        run.UpdatedAt,
        run.PlanJson
    );

    private static string StageLabel(WorkflowState state) => state switch
    {
        WorkflowState.Triage => "Assessing urgency of the reports",
        WorkflowState.Matching => "Matching needs to depot stock",
        WorkflowState.Routing => "Planning delivery routes",
        WorkflowState.Validating => "Running safety checks",
        WorkflowState.PendingApproval => "Awaiting coordinator approval",
        WorkflowState.Approved => "Approved — supplies dispatched",
        WorkflowState.Rejected => "Rejected by the coordinator",
        WorkflowState.RevisionRequested => "Revision requested — re-planning",
        WorkflowState.Failed => "Stopped — the plan could not be completed",
        _ => state.ToString()
    };

    private static int ProgressPercent(WorkflowState state) => state switch
    {
        WorkflowState.Triage => 15,
        WorkflowState.Matching => 35,
        WorkflowState.RevisionRequested => 35,
        WorkflowState.Routing => 55,
        WorkflowState.Validating => 75,
        WorkflowState.PendingApproval => 90,
        _ => 100
    };

    private static JsonElement? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(json);
        }
    }
}
