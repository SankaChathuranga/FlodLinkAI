using System.Text.Json;
using FloodLink.Contracts;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Validation;

/// <summary>
/// Deterministic rule-based implementation of <see cref="IValidationAgent"/>.
/// Owned by Member D (Ijini). No LLM call — pure <see cref="SafetyRules"/> checks
/// against the accumulated plan stored in the workflow run.
/// </summary>
public sealed class ValidationAgent : IValidationAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AppDbContext _db;

    public ValidationAgent(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<AgentResult<ValidationResults>> ExecuteAsync(
        Route route,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.WorkflowRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == route.WorkflowRunId, cancellationToken);

        if (run is null)
        {
            return AgentResult<ValidationResults>.Fail(
                "WORKFLOW_NOT_FOUND",
                $"No workflow run with id {route.WorkflowRunId}.");
        }

        if (string.IsNullOrWhiteSpace(run.PlanJson))
        {
            return AgentResult<ValidationResults>.Fail(
                "PLAN_NOT_FOUND",
                $"Workflow run {route.WorkflowRunId} has no PlanJson to validate.");
        }

        PlanDocument plan;
        try
        {
            plan = JsonSerializer.Deserialize<PlanDocument>(run.PlanJson, JsonOptions) ?? throw new JsonException("Plan parsed to null.");
        }
        catch (JsonException ex)
        {
            return AgentResult<ValidationResults>.Fail(
                "INVALID_PLAN",
                $"Workflow run {route.WorkflowRunId} PlanJson is not a valid plan document: {ex.Message}");
        }

        if (plan.WorkflowRunId != route.WorkflowRunId)
        {
            return AgentResult<ValidationResults>.Fail(
                "INVALID_PLAN",
                $"Plan document targets workflow {plan.WorkflowRunId}, expected {route.WorkflowRunId}.");
        }

        var checks = SafetyRules.RunAll(plan, route);
        var overallPassed = checks.All(c => c.Passed);

        return AgentResult<ValidationResults>.Ok(new ValidationResults
        {
            WorkflowRunId = route.WorkflowRunId,
            Checks = checks,
            OverallPassed = overallPassed
        });
    }
}