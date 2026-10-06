using FloodLink.Agents.Matching;
using FloodLink.Agents.Triage;
using FloodLink.Agents.Validation;
using FloodLink.Contracts;
using FloodLink.Domain;
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

/// <summary>Adapts Member D's <see cref="IValidationAgent"/> to <see cref="IValidationAgentInvoker"/>.</summary>
public sealed class ValidationAgentInvoker(IValidationAgent agent) : IValidationAgentInvoker
{
    public Task<AgentResult<ValidationResults>> ExecuteAsync(Route route, CancellationToken ct = default)
        => agent.ExecuteAsync(route, ct);
}
