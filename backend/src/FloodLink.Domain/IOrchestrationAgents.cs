using FloodLink.Contracts;

namespace FloodLink.Domain;

// Thin orchestration-facing interfaces so the Domain layer can call agents
// without depending on the individual FloodLink.Agents.* projects.
// Each FloodLink.Agents.* project implements the matching interface from its own namespace;
// the DI container wires them together at startup.

/// <summary>
/// Orchestration-facing contract for the Planner step (Member C): turns the run's objective and a
/// summary of its reports into an <see cref="ExecutionPlan"/>. Never fails for an LLM problem —
/// it returns the default plan instead.
/// </summary>
public interface IPlannerInvoker
{
    Task<AgentResult<ExecutionPlan>> ExecuteAsync(Guid workflowRunId, CancellationToken ct = default);
}

/// <summary>Orchestration-facing contract for the Triage/Planner Agent (Member A).</summary>
public interface ITriageAgentInvoker
{
    Task<AgentResult<TriagePlan>> ExecuteAsync(Guid workflowRunId, CancellationToken ct = default);
}

/// <summary>Orchestration-facing contract for the Logistics/Matching Agent (Member B).</summary>
public interface IMatchingAgentInvoker
{
    /// <param name="revisionNotes">The coordinator's notes when the run was sent back for revision; untrusted text.</param>
    Task<AgentResult<AllocationProposal>> ExecuteAsync(TriagePlan plan, string? revisionNotes = null, CancellationToken ct = default);
}

/// <summary>Orchestration-facing contract for the Route/ETA Agent (Member C).</summary>
public interface IRoutingAgentInvoker
{
    Task<AgentResult<Route>> ExecuteAsync(AllocationProposal proposal, CancellationToken ct = default);
}

/// <summary>Orchestration-facing contract for the Validation/Safety Agent (Member D).</summary>
public interface IValidationAgentInvoker
{
    Task<AgentResult<ValidationResults>> ExecuteAsync(Route route, CancellationToken ct = default);
}
