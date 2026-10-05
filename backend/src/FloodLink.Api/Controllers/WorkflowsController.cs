using FloodLink.Api.Dtos;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/workflows")]
public class WorkflowsController : ControllerBase
{
    private readonly WorkflowOrchestrator _orchestrator;
    private readonly IWorkflowRunRepository _runs;

    public WorkflowsController(WorkflowOrchestrator orchestrator, IWorkflowRunRepository runs)
    {
        _orchestrator = orchestrator;
        _runs = runs;
    }

    // ── POST /api/workflows ───────────────────────────────────────────────────
    /// <summary>
    /// Creates a new WorkflowRun (starting in Triage) and immediately kicks off
    /// the orchestration pipeline for the first agent step.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Objective))
            return ValidationProblem("Objective is required.");

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            Objective = request.Objective,
            CurrentState = WorkflowState.Triage
        };

        await _runs.AddAsync(run, ct);
        await _runs.SaveAsync(ct);

        // Kick off the first agent step (Triage) immediately after creation.
        // If the agent step fails the run transitions to Failed — that is the
        // expected safe-failure path, not an exception to surface to the caller.
        try
        {
            run = await _orchestrator.AdvanceAsync(run.Id, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Cannot advance workflow");
        }

        return CreatedAtAction(nameof(Get), new { id = run.Id }, ToResponse(run));
    }

    // ── GET /api/workflows/{id} ───────────────────────────────────────────────
    /// <summary>
    /// Returns the current state and plan output for a single WorkflowRun.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(id, ct);
        return run is not null ? Ok(ToResponse(run)) : NotFound();
    }

    // ── POST /api/workflows/{id}/decision ─────────────────────────────────────
    /// <summary>
    /// Submits a coordinator decision on a run that is in PendingApproval.
    /// Accepted values for Decision: "Approved", "Rejected", "RevisionRequested".
    /// Caller must supply the header X-User-Role: Coordinator.
    /// </summary>
    [HttpPost("{id:guid}/decision")]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MakeDecision(
        Guid id,
        [FromBody] CoordinatorDecisionRequest request,
        CancellationToken ct)
    {
        // Lightweight role gate — header-based until JWT auth is wired up (Phase 7).
        var role = Request.Headers["X-User-Role"].FirstOrDefault();
        if (role != "Coordinator")
            return Problem(
                detail: "Only coordinators can submit approval decisions.",
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized");

        // Only these three states are valid coordinator decisions.
        if (!TryParseCoordinatorDecision(request.Decision, out var decisionState))
            return Problem(
                detail: $"'{request.Decision}' is not a valid decision. Use: Approved, Rejected, or RevisionRequested.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid decision");

        var run = await _runs.GetByIdAsync(id, ct);
        if (run is null)
            return NotFound();

        try
        {
            var updated = await _orchestrator.ApplyCoordinatorDecisionAsync(id, decisionState, ct);
            return Ok(ToResponse(updated));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Invalid decision");
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest, title: "Cannot apply decision");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WorkflowRunResponse ToResponse(WorkflowRun run) => new(
        run.Id,
        run.Objective,
        run.CurrentState.ToString(),
        run.FailedAtState?.ToString(),
        run.CreatedAt,
        run.UpdatedAt,
        run.PlanJson
    );

    /// <summary>
    /// Only Approved / Rejected / RevisionRequested are valid coordinator decisions.
    /// Rejects other states even if they exist in the WorkflowState enum.
    /// </summary>
    private static bool TryParseCoordinatorDecision(string? input, out WorkflowState state)
    {
        state = default;
        if (!Enum.TryParse<WorkflowState>(input, ignoreCase: true, out var parsed))
            return false;

        if (parsed is not (WorkflowState.Approved or WorkflowState.Rejected or WorkflowState.RevisionRequested))
            return false;

        state = parsed;
        return true;
    }
}

public record CreateWorkflowRequest(string Objective);
public record CoordinatorDecisionRequest(string? Decision);
