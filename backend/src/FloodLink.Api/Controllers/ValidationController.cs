using System.Text.Json;
using FloodLink.Agents.Validation;
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
/// Validation/Safety Agent endpoints (Member D — Ijini).
/// Triggers the deterministic validation pipeline and exposes persisted results.
/// Coordinator-only.
/// </summary>
[ApiController]
[Route("api/validations")]
[Authorize(Roles = "Coordinator")]
public sealed class ValidationController : ControllerBase
{
    /// <summary>Body contract for POST /api/validations/run.</summary>
    public sealed record RunValidationRequest(Guid WorkflowRunId);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AppDbContext _db;
    private readonly IValidationAgent _validationAgent;
    private readonly IStockReservationService _stock;

    public ValidationController(AppDbContext db, IValidationAgent validationAgent, IStockReservationService stock)
    {
        _db = db;
        _validationAgent = validationAgent;
        _stock = stock;
    }

    /// <summary>
    /// Runs all deterministic safety checks against a completed plan. On overall pass the plan's
    /// stock is reserved and the workflow moves to PendingApproval; on fail (or if the stock can
    /// no longer be reserved) it moves to Failed with a recorded reason.
    /// </summary>
    [HttpPost("run")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RunAsync([FromBody] RunValidationRequest request, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == request.WorkflowRunId, ct);

        if (run is null)
        {
            return NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {request.WorkflowRunId}." });
        }

        if (run.CurrentState != WorkflowState.Validating)
        {
            return Conflict(new
            {
                error = "INVALID_STATE",
                message = $"Validation requires state Validating but the workflow is {run.CurrentState}."
            });
        }

        if (string.IsNullOrWhiteSpace(run.PlanJson))
        {
            return UnprocessableEntity(new { error = "PLAN_NOT_FOUND", message = "Workflow run has no PlanJson to validate." });
        }

        PlanDocument plan;
        try
        {
            plan = JsonSerializer.Deserialize<PlanDocument>(run.PlanJson, JsonOptions)
                   ?? throw new JsonException("Plan parsed to null.");
        }
        catch (JsonException ex)
        {
            return UnprocessableEntity(new { error = "INVALID_PLAN", message = $"PlanJson is not a valid plan document: {ex.Message}" });
        }

        var route = new Route
        {
            WorkflowRunId = run.Id,
            AllocationProposalId = plan.AllocationProposalId,
            DistanceKm = plan.DistanceKm,
            EtaMinutes = plan.EtaMinutes,
            Polyline = plan.Polyline
        };

        var agentResult = await _validationAgent.ExecuteAsync(route, ct);
        if (!agentResult.Success)
        {
            return UnprocessableEntity(new { error = agentResult.ErrorCode, message = agentResult.ErrorMessage });
        }

        foreach (var check in agentResult.Data!.Checks)
        {
            _db.ValidationResults.Add(new ValidationResult
            {
                WorkflowRunId = run.Id,
                CheckName = check.CheckName,
                Passed = check.Passed,
                ViolationDetail = check.ViolationDetail
            });
        }

        _db.AuditTrail.Add(new AuditTrail
        {
            EventType = "PlanValidated",
            EventDetailJson = JsonSerializer.Serialize(new
            {
                overallPassed = agentResult.Data.OverallPassed,
                checkCount = agentResult.Data.Checks.Count
            })
        });

        var targetState = agentResult.Data.OverallPassed ? WorkflowState.PendingApproval : WorkflowState.Failed;
        string? failureReason = agentResult.Data.OverallPassed
            ? null
            : "VALIDATION_FAILED: " + string.Join("; ", agentResult.Data.Checks
                .Where(c => !c.Passed).Select(c => $"{c.CheckName}: {c.ViolationDetail}"));

        if (agentResult.Data.OverallPassed)
        {
            var reservation = await _stock.ReserveForRunAsync(run.Id, ct);
            if (!reservation.Succeeded)
            {
                targetState = WorkflowState.Failed;
                failureReason = $"{reservation.ErrorCode}: {reservation.ErrorMessage}";
            }
        }

        WorkflowEngine.TryTransition(run, targetState);
        if (targetState == WorkflowState.Failed)
            run.FailureReason = failureReason;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            workflowRunId = run.Id,
            overallPassed = agentResult.Data.OverallPassed,
            state = targetState,
            checks = agentResult.Data.Checks
        });
    }

    /// <summary>Returns all persisted validation checks for a workflow run.</summary>
    [HttpGet("{workflowRunId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResultsAsync(Guid workflowRunId, CancellationToken ct)
    {
        var run = await _db.WorkflowRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        var checks = await _db.ValidationResults.AsNoTracking()
            .Where(v => v.WorkflowRunId == workflowRunId)
            .OrderBy(v => v.CreatedAt)
            .Select(v => new { v.CheckName, v.Passed, v.ViolationDetail })
            .ToListAsync(ct);

        return Ok(new
        {
            workflowRunId,
            state = run.CurrentState,
            hasRunValidation = checks.Count > 0,
            overallPassed = checks.Count > 0 && checks.All(c => c.Passed),
            checks
        });
    }
}