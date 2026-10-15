using FloodLink.Api.Auth;
using FloodLink.Api.Dtos;
using FloodLink.Api.DTOs;
using FloodLink.Api.Workflows;
using FloodLink.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FloodLink.Api.Controllers;

/// <summary>
/// Workflow orchestration endpoints (Member C): start a run, monitor it, read its execution
/// trace, and retry a failed stage. Logic lives in <see cref="WorkflowService"/>; coordinator
/// decisions live in <see cref="DispatchController"/>.
/// </summary>
[ApiController]
[Route("api/workflows")]
[Authorize]
public class WorkflowsController(WorkflowService workflows) : ControllerBase
{
    // ── POST /api/workflows ───────────────────────────────────────────────────
    /// <summary>
    /// Creates a WorkflowRun for an objective and a set of field reports and queues it. The
    /// background runner takes it through Triage, Matching, Routing and Validating; poll
    /// GET /api/workflows/{id}/status until it reaches PendingApproval or Failed.
    /// </summary>
    /// <remarks>A report can only belong to one active (not yet decided) run at a time.</remarks>
    [HttpPost]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest request, CancellationToken ct)
    {
        var result = await workflows.StartAsync(request.Objective, request.ReportIds, User.GetUserId(), ct);
        return result.Error is not null
            ? Refused(result.Error)
            : AcceptedAtAction(nameof(GetStatus), new { id = result.Value!.Id }, WorkflowService.ToResponse(result.Value));
    }

    // ── GET /api/workflows ────────────────────────────────────────────────────
    /// <summary>
    /// Lists workflow runs, newest first by default. Filter by <paramref name="state"/>, search the
    /// objective, sort by <c>createdAt</c> or <c>updatedAt</c> (prefix <c>-</c> for descending).
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(typeof(PaginatedResult<WorkflowRunSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? state,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await workflows.ListAsync(state, search, sort, page, pageSize, ct);
        return result.Error is not null ? Refused(result.Error) : Ok(result.Value);
    }

    // ── GET /api/workflows/{id} ───────────────────────────────────────────────
    /// <summary>Returns the run with its full plan (every agent's output, keyed by section).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var run = await workflows.GetAsync(id, ct);
        return run is not null ? Ok(run) : NotFound();
    }

    // ── GET /api/workflows/{id}/logs ──────────────────────────────────────────
    /// <summary>
    /// Execution summary of a run in order: each agent and orchestration step with status,
    /// duration, retry flag, tool calls, output and error. Inputs (the plan snapshot each step
    /// saw) are large and only included with <c>includeInput=true</c>.
    /// </summary>
    [HttpGet("{id:guid}/logs")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(typeof(IReadOnlyList<WorkflowLogEntry>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogs(Guid id, [FromQuery] bool includeInput = false, CancellationToken ct = default)
    {
        var logs = await workflows.GetLogsAsync(id, includeInput, ct);
        return logs is not null ? Ok(logs) : NotFound();
    }

    // ── GET /api/workflows/{id}/status ────────────────────────────────────────
    /// <summary>
    /// Lightweight status for polling: stage, progress, failure reason, the coordinator's latest
    /// decision and note, the delivery ETA once routed, and whether delivery was confirmed.
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(WorkflowStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(Guid id, CancellationToken ct)
    {
        // Access matrix: the Volunteer who reported into the run, or an Admin.
        if (!User.IsInAnyRoleOrAdmin(Roles.Volunteer))
            return Forbid();

        int? ownerId = User.IsInRole(Roles.Admin) ? null : User.GetUserId() ?? -1;
        var status = await workflows.GetStatusAsync(id, ownerId, ct);
        return status is not null ? Ok(status) : NotFound();
    }

    // ── POST /api/workflows/{id}/retry ────────────────────────────────────────
    /// <summary>
    /// Re-runs the stage a failed run stopped in (e.g. after restocking, or once Mapbox is back).
    /// Limited to <c>Workflow:MaxRetries</c> retries per run (default 3).
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(typeof(WorkflowRetryResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var result = await workflows.RetryAsync(id, User.GetUserId(), ct);
        return result.Error is not null
            ? Refused(result.Error)
            : AcceptedAtAction(nameof(GetStatus), new { id }, result.Value);
    }

    private ObjectResult Refused(WorkflowError error)
        => StatusCode(error.StatusCode, new { error = error.Code, message = error.Message });
}
