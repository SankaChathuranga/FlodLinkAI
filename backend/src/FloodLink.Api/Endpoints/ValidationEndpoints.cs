using System.Text.Json;
using FloodLink.Agents.Validation;
using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Route = FloodLink.Contracts.Route;

namespace FloodLink.Api.Endpoints;

/// <summary>
/// Validation/Safety Agent endpoints (Member D — Ijini).
/// Triggers the deterministic validation pipeline and exposes persisted results.
/// </summary>
public static class ValidationEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Body contract for POST /api/validations/run.</summary>
    public sealed record RunValidationRequest(Guid WorkflowRunId);

    /// <summary>Wires the /api/validations route group.</summary>
    public static void MapValidationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/validations");

        group.MapPost("/run", RunAsync)
            .WithName("RunValidation")
            .WithTags("Validations");

        group.MapGet("/{workflowRunId:guid}", GetResultsAsync)
            .WithName("GetValidationResults")
            .WithTags("Validations");
    }

    /// <summary>
    /// Runs all deterministic safety checks against a completed plan. On overall
    /// pass the workflow moves to PendingApproval; on fail it moves to Failed.
    /// </summary>
    private static async Task<IResult> RunAsync(
        RunValidationRequest request,
        AppDbContext db,
        IValidationAgent validationAgent,
        IWorkflowStateService workflow,
        CancellationToken ct)
    {
        var run = await db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == request.WorkflowRunId, ct);

        if (run is null)
        {
            return Results.NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {request.WorkflowRunId}." });
        }

        if (run.CurrentState != WorkflowState.Validating)
        {
            return Results.Conflict(new
            {
                error = "INVALID_STATE",
                message = $"Validation requires state Validating but the workflow is {run.CurrentState}."
            });
        }

        if (string.IsNullOrWhiteSpace(run.PlanJson))
        {
            return Results.UnprocessableEntity(new { error = "PLAN_NOT_FOUND", message = "Workflow run has no PlanJson to validate." });
        }

        PlanDocument plan;
        try
        {
            plan = JsonSerializer.Deserialize<PlanDocument>(run.PlanJson, JsonOptions)
                   ?? throw new JsonException("Plan parsed to null.");
        }
        catch (JsonException ex)
        {
            return Results.UnprocessableEntity(new { error = "INVALID_PLAN", message = $"PlanJson is not a valid plan document: {ex.Message}" });
        }

        var route = new Route
        {
            WorkflowRunId = run.Id,
            AllocationProposalId = plan.AllocationProposalId,
            DistanceKm = plan.DistanceKm,
            EtaMinutes = plan.EtaMinutes,
            Polyline = plan.Polyline
        };

        var agentResult = await validationAgent.ExecuteAsync(route, ct);
        if (!agentResult.Success)
        {
            return Results.UnprocessableEntity(new { error = agentResult.ErrorCode, message = agentResult.ErrorMessage });
        }

        foreach (var check in agentResult.Data!.Checks)
        {
            db.ValidationResults.Add(new ValidationResult
            {
                WorkflowRunId = run.Id,
                CheckName = check.CheckName,
                Passed = check.Passed,
                ViolationDetail = check.ViolationDetail
            });
        }

        db.AuditTrail.Add(new AuditTrail
        {
            EventType = "PlanValidated",
            EventDetailJson = JsonSerializer.Serialize(new
            {
                overallPassed = agentResult.Data.OverallPassed,
                checkCount = agentResult.Data.Checks.Count
            })
        });

        var targetState = agentResult.Data.OverallPassed ? WorkflowState.PendingApproval : WorkflowState.Failed;
        await workflow.TryTransitionAsync(run.Id, targetState, ct);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            workflowRunId = run.Id,
            overallPassed = agentResult.Data.OverallPassed,
            state = targetState,
            checks = agentResult.Data.Checks
        });
    }

    /// <summary>Returns all persisted validation checks for a workflow run.</summary>
    private static async Task<IResult> GetResultsAsync(
        Guid workflowRunId,
        AppDbContext db,
        CancellationToken ct)
    {
        var run = await db.WorkflowRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return Results.NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        var checks = await db.ValidationResults.AsNoTracking()
            .Where(v => v.WorkflowRunId == workflowRunId)
            .OrderBy(v => v.CreatedAt)
            .Select(v => new { v.CheckName, v.Passed, v.ViolationDetail })
            .ToListAsync(ct);

        return Results.Ok(new
        {
            workflowRunId,
            state = run.CurrentState,
            hasRunValidation = checks.Count > 0,
            overallPassed = checks.Count > 0 && checks.All(c => c.Passed),
            checks
        });
    }
}