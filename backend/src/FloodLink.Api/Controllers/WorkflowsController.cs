using System.Text.Json;
using FloodLink.Api.Dtos;
using FloodLink.Api.DTOs;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Route = FloodLink.Contracts.Route;

namespace FloodLink.Api.Controllers;

/// <summary>
/// Workflow orchestration endpoints (Member C): start a run, monitor it, read its execution
/// trace, and retry a failed stage. Coordinator decisions live in <see cref="DispatchController"/>.
/// </summary>
[ApiController]
[Route("api/workflows")]
public class WorkflowsController : ControllerBase
{
    private static readonly WorkflowState[] FinishedStates =
        [WorkflowState.Approved, WorkflowState.Rejected, WorkflowState.Failed];

    private readonly WorkflowOrchestrator _orchestrator;
    private readonly IWorkflowRunQueue _queue;
    private readonly AppDbContext _db;
    private readonly int _maxRetries;

    public WorkflowsController(WorkflowOrchestrator orchestrator, IWorkflowRunQueue queue, AppDbContext db, IConfiguration configuration)
    {
        _orchestrator = orchestrator;
        _queue = queue;
        _db = db;
        _maxRetries = configuration.GetValue("Workflow:MaxRetries", defaultValue: 3);
    }

    // ── POST /api/workflows ───────────────────────────────────────────────────
    /// <summary>
    /// Creates a WorkflowRun for an objective and a set of field reports and queues it. The
    /// background runner takes it through Triage, Matching, Routing and Validating; poll
    /// GET /api/workflows/{id}/status until it reaches PendingApproval or Failed.
    /// </summary>
    /// <remarks>A report can only belong to one active (not yet decided) run at a time.</remarks>
    [HttpPost]
    [Authorize(Roles = "Coordinator")]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Objective))
            return ValidationProblem("Objective is required.");

        List<Report> reports;
        if (request.ReportIds is { Count: > 0 })
        {
            var ids = request.ReportIds.Distinct().ToList();
            reports = await _db.Reports.Include(r => r.WorkflowRun)
                .Where(r => ids.Contains(r.Id))
                .ToListAsync(ct);

            var missing = ids.Except(reports.Select(r => r.Id)).ToList();
            if (missing.Count > 0)
                return BadRequest(new { error = "REPORT_NOT_FOUND", message = $"Reports not found: {string.Join(", ", missing)}." });

            var resolved = reports.Where(r => r.Status == "Resolved").Select(r => r.Id).ToList();
            if (resolved.Count > 0)
                return BadRequest(new { error = "REPORT_RESOLVED", message = $"Reports already resolved: {string.Join(", ", resolved)}." });

            var busy = reports.Where(IsInActiveRun).Select(r => r.Id).ToList();
            if (busy.Count > 0)
                return Conflict(new { error = "REPORT_IN_ACTIVE_WORKFLOW", message = $"Reports already in an active workflow: {string.Join(", ", busy)}." });
        }
        else
        {
            reports = (await _db.Reports.Include(r => r.WorkflowRun)
                    .Where(r => r.Status == "New" || r.Status == "Triaged")
                    .ToListAsync(ct))
                .Where(r => !IsInActiveRun(r))
                .ToList();
            if (reports.Count == 0)
                return BadRequest(new { error = "NO_REPORTS", message = "There are no open reports to plan for." });
        }

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            Objective = request.Objective.Trim(),
            CurrentState = WorkflowState.Triage,
            ReportIds = reports.Select(r => r.Id).OrderBy(id => id).ToList()
        };
        _db.WorkflowRuns.Add(run);
        foreach (var report in reports)
            report.WorkflowRunId = run.Id;
        await _db.SaveChangesAsync(ct);

        await _queue.EnqueueAsync(run.Id, ct);
        return AcceptedAtAction(nameof(GetStatus), new { id = run.Id }, ToResponse(run));
    }

    // ── GET /api/workflows ────────────────────────────────────────────────────
    /// <summary>
    /// Lists workflow runs, newest first by default. Filter by <paramref name="state"/>, search the
    /// objective, sort by <c>createdAt</c> or <c>updatedAt</c> (prefix <c>-</c> for descending).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Coordinator")]
    [ProducesResponseType(typeof(PaginatedResult<WorkflowRunSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? state,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        IQueryable<WorkflowRun> query = _db.WorkflowRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(state))
        {
            if (!Enum.TryParse<WorkflowState>(state, ignoreCase: true, out var parsed))
                return BadRequest(new { error = "INVALID_STATE", message = $"Unknown workflow state '{state}'." });
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

        var total = await query.CountAsync(ct);
        var runs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new PaginatedResult<WorkflowRunSummary>
        {
            Items = runs.Select(r => new WorkflowRunSummary(
                r.Id, r.Objective, r.CurrentState.ToString(), ProgressPercent(r.CurrentState),
                r.FailedAtState?.ToString(), r.FailureReason, r.ReportIds?.Count ?? 0, r.RetryCount,
                r.CreatedAt, r.UpdatedAt)).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    // ── GET /api/workflows/{id} ───────────────────────────────────────────────
    /// <summary>Returns the run with its full plan (every agent's output, keyed by section).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is not null ? Ok(ToResponse(run)) : NotFound();
    }

    // ── GET /api/workflows/{id}/logs ──────────────────────────────────────────
    /// <summary>
    /// Execution summary of a run in order: each agent and orchestration step with status,
    /// duration, retry flag, tool calls, output and error. Inputs (the plan snapshot each step
    /// saw) are large and only included with <c>includeInput=true</c>.
    /// </summary>
    [HttpGet("{id:guid}/logs")]
    [Authorize(Roles = "Coordinator")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogs(Guid id, [FromQuery] bool includeInput = false, CancellationToken ct = default)
    {
        if (!await _db.WorkflowRuns.AnyAsync(r => r.Id == id, ct))
            return NotFound();

        var logs = await _db.AgentExecutionLogs.AsNoTracking()
            .Where(l => l.WorkflowRunId == id)
            .OrderBy(l => l.CreatedAt)
            .ToListAsync(ct);

        return Ok(logs.Select(l => new WorkflowLogEntry(
            l.Id, l.AgentName, l.Status, l.IsRetry, l.DurationMs, l.CreatedAt, l.ErrorMessage,
            ParseJson(l.ToolCallsJson), ParseJson(l.OutputJson), includeInput ? ParseJson(l.InputJson) : null)).ToList());
    }

    // ── GET /api/workflows/{id}/status ────────────────────────────────────────
    /// <summary>
    /// Lightweight status for polling: stage, progress, failure reason, the coordinator's latest
    /// decision and note, the delivery ETA once routed, and whether delivery was confirmed.
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(WorkflowStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
            return NotFound();

        var dispatch = await _db.Dispatches.AsNoTracking()
            .Where(d => d.WorkflowRunId == id)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);
        var delivered = dispatch is not null && await _db.AuditTrail.AsNoTracking()
            .AnyAsync(a => a.DispatchId == dispatch.Id && a.EventType == "DeliveryConfirmed", ct);
        var route = WorkflowPlan.TryRead<Route>(run.PlanJson, WorkflowPlan.RouteKey);

        return Ok(new WorkflowStatusResponse(
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
            run.UpdatedAt));
    }

    // ── POST /api/workflows/{id}/retry ────────────────────────────────────────
    /// <summary>
    /// Re-runs the stage a failed run stopped in (e.g. after restocking, or once Mapbox is back).
    /// Limited to <c>Workflow:MaxRetries</c> retries per run (default 3).
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    [Authorize(Roles = "Coordinator")]
    [ProducesResponseType(typeof(WorkflowStatusResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null)
            return NotFound();

        // A report that has since joined another run must not be planned twice.
        var reportIds = run.ReportIds ?? [];
        var reassigned = await _db.Reports.AsNoTracking()
            .Where(r => reportIds.Contains(r.Id) && r.WorkflowRunId != id)
            .Select(r => r.Id)
            .ToListAsync(ct);
        if (reassigned.Count > 0)
            return Conflict(new { error = "REPORTS_REASSIGNED", message = $"Reports now belong to another workflow: {string.Join(", ", reassigned)}." });

        var outcome = await _orchestrator.PrepareRetryAsync(id, _maxRetries, ct);
        switch (outcome)
        {
            case RetryOutcome.NotFound:
                return NotFound();
            case RetryOutcome.NotFailed:
                return Conflict(new { error = "NOT_FAILED", message = $"Only a failed run can be retried; current state is {run.CurrentState}." });
            case RetryOutcome.LimitReached:
                return Conflict(new { error = "RETRY_LIMIT_REACHED", message = $"This run has already been retried {run.RetryCount} times (limit {_maxRetries})." });
            case RetryOutcome.NotRetryable:
                return Conflict(new { error = "NOT_RETRYABLE", message = "The run did not fail in an agent stage, so there is nothing to re-run." });
        }

        await _queue.EnqueueAsync(id, ct);
        return AcceptedAtAction(nameof(GetStatus), new { id }, new { id, state = run.FailedAtState?.ToString(), retryCount = run.RetryCount + 1 });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsInActiveRun(Report report)
        => report.WorkflowRun is not null && !FinishedStates.Contains(report.WorkflowRun.CurrentState);

    private static WorkflowRunResponse ToResponse(WorkflowRun run) => new(
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
