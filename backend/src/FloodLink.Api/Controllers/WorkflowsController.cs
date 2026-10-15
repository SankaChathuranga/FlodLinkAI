using FloodLink.Api.Dtos;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
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

    // ── POST /api/workflows/{id}/advance ──────────────────────────────────────
    [HttpPost("{id:guid}/advance")]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Advance(Guid id, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(id, ct);
        if (run is null)
            return NotFound();

        if (run.CurrentState is WorkflowState.Approved or WorkflowState.Rejected or WorkflowState.Failed or WorkflowState.PendingApproval)
        {
            return Problem(
                detail: $"WorkflowRun {id} is in state '{run.CurrentState}' and cannot be advanced.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Invalid workflow state transition");
        }

        try
        {
            var updated = await _orchestrator.AdvanceAsync(id, ct);
            return Ok(ToResponse(updated));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: "Cannot advance workflow");
        }
    }

    // ── GET /api/workflows/{id}/allocation-proposal ───────────────────────────
    [HttpGet("{id:guid}/allocation-proposal")]
    [ProducesResponseType(typeof(FloodLink.Contracts.AllocationProposal), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAllocationProposal(Guid id, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(id, ct);
        if (run is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(run.PlanJson))
            return NotFound(new { message = $"WorkflowRun {id} does not contain plan data." });

        try
        {
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(run.PlanJson);
            if (dict is null || !dict.TryGetValue("allocationProposal", out var element))
                return NotFound(new { message = $"WorkflowRun {id} does not contain an allocation proposal." });

            var proposal = System.Text.Json.JsonSerializer.Deserialize<FloodLink.Contracts.AllocationProposal>(element.GetRawText());
            return proposal is not null ? Ok(proposal) : NotFound();
        }
        catch
        {
            return NotFound();
        }
    }

    // ── GET /api/workflows/{id} ───────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(id, ct);
        return run is not null ? Ok(ToResponse(run)) : NotFound();
    }

    // ── POST /api/workflows/{id}/decision ─────────────────────────────────────
    [HttpPost("{id:guid}/decision")]
    [Authorize(Roles = "Coordinator")]
    [ProducesResponseType(typeof(WorkflowRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MakeDecision(
        Guid id,
        [FromBody] CoordinatorDecisionRequest request,
        CancellationToken ct)
    {
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
