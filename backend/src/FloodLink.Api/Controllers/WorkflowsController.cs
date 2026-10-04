using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController : ControllerBase
{
    private readonly WorkflowOrchestrator _orchestrator;
    private readonly IWorkflowRunRepository _runs;

    public WorkflowsController(WorkflowOrchestrator orchestrator, IWorkflowRunRepository runs)
    {
        _orchestrator = orchestrator;
        _runs = runs;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest request, CancellationToken ct)
    {
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            Objective = request.Objective,
            CurrentState = WorkflowState.Triage
        };
        await _runs.AddAsync(run, ct);
        await _runs.SaveAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = run.Id }, run);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var run = await _runs.GetByIdAsync(id, ct);
        return run is not null ? Ok(run) : NotFound();
    }

    [HttpPost("{id}/advance")]
    public async Task<IActionResult> Advance(Guid id, CancellationToken ct)
    {
        try
        {
            var run = await _orchestrator.AdvanceAsync(id, ct);
            return Ok(run);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id}/decision")]
    public async Task<IActionResult> MakeDecision(Guid id, [FromBody] CoordinatorDecisionRequest request, CancellationToken ct)
    {
        // TODO (Week 2): Temporary auth check - validate caller is a coordinator
        var role = Request.Headers["X-User-Role"].FirstOrDefault();
        if (role != "Coordinator")
        {
            return Unauthorized(new { error = "Only coordinators can make approval decisions." });
        }

        if (!Enum.TryParse<WorkflowState>(request.Decision, true, out var decisionState))
        {
            return BadRequest(new { error = "Invalid decision. Must be Approved, Rejected, or RevisionRequested." });
        }

        try
        {
            var run = await _orchestrator.ApplyCoordinatorDecisionAsync(id, decisionState, ct);
            return Ok(run);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record CreateWorkflowRequest(string Objective);
public record CoordinatorDecisionRequest(string Decision);
