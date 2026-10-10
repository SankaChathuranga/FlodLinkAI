using FloodLink.Agents.Matching;
using FloodLink.Agents.Triage;
using FloodLink.Agents.Validation;
using System.Text.Json;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Route = FloodLink.Contracts.Route;

namespace FloodLink.Api;

// Adapters bridging the orchestration-facing invoker interfaces (FloodLink.Domain)
// to each member's agent implementation. Domain can't reference the Agents.* projects,
// so the Api composition root owns the glue.

/// <summary>Adapts Member A's <see cref="ITriageAgent"/> to <see cref="ITriageAgentInvoker"/>.</summary>
public sealed class TriageAgentInvoker(ITriageAgent agent) : ITriageAgentInvoker
{
    public Task<AgentResult<TriagePlan>> ExecuteAsync(Guid workflowRunId, CancellationToken ct = default)
        => agent.ExecuteAsync(workflowRunId, ct);
}

/// <summary>Adapts Member B's <see cref="IMatchingAgent"/> to <see cref="IMatchingAgentInvoker"/>.</summary>
public sealed class MatchingAgentInvoker(IMatchingAgent agent) : IMatchingAgentInvoker
{
    public Task<AgentResult<AllocationProposal>> ExecuteAsync(TriagePlan plan, CancellationToken ct = default)
        => agent.ExecuteAsync(plan, ct);
}

/// <summary>
/// Adapts Member D's <see cref="IValidationAgent"/> to <see cref="IValidationAgentInvoker"/>,
/// and records the run's latest check results and a <c>PlanValidated</c> audit event so the
/// approval queue can show them. The orchestrator saves them with the step.
/// </summary>
public sealed class ValidationAgentInvoker(IValidationAgent agent, AppDbContext db) : IValidationAgentInvoker
{
    public async Task<AgentResult<ValidationResults>> ExecuteAsync(Route route, CancellationToken ct = default)
    {
        var result = await agent.ExecuteAsync(route, ct);
        if (!result.Success || result.Data is null)
            return result;

        // Keep only the latest attempt's checks (a retry or revision validates again).
        var previous = await db.ValidationResults.Where(v => v.WorkflowRunId == route.WorkflowRunId).ToListAsync(ct);
        db.ValidationResults.RemoveRange(previous);
        db.ValidationResults.AddRange(result.Data.Checks.Select(check => new ValidationResult
        {
            WorkflowRunId = route.WorkflowRunId,
            CheckName = check.CheckName,
            Passed = check.Passed,
            ViolationDetail = check.ViolationDetail
        }));
        db.AuditTrail.Add(new AuditTrail
        {
            EventType = "PlanValidated",
            EventDetailJson = JsonSerializer.Serialize(new
            {
                workflowRunId = route.WorkflowRunId,
                overallPassed = result.Data.OverallPassed,
                checkCount = result.Data.Checks.Count
            })
        });

        return result;
    }
}
