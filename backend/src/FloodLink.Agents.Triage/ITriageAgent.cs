using FloodLink.Contracts;

namespace FloodLink.Agents.Triage;

/// <summary>
/// Defines the public contract for the Triage/Planner Agent (Member A — Sharani).
/// Reads field reports and shelter context, scores urgency, and produces a
/// prioritized <see cref="TriagePlan"/> for the Logistics/Matching Agent.
/// </summary>
/// <remarks>
/// Implementation is owned by Member A. See CONTRIBUTING.md for scope rules:
/// every agent must return <see cref="AgentResult{T}"/> — never throw for
/// expected/anticipated failures.
/// </remarks>
public interface ITriageAgent
{
    /// <summary>
    /// Runs the Triage/Planner Agent for the given workflow run.
    /// </summary>
    /// <param name="workflowRunId">
    /// The workflow run this triage is part of. Used to load the relevant
    /// reports and shelter context from the database.
    /// </param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <returns>
    /// An <see cref="AgentResult{TriagePlan}"/> containing the ranked plan on
    /// success, or an error code + message on anticipated failure.
    /// </returns>
    Task<AgentResult<TriagePlan>> ExecuteAsync(
        Guid workflowRunId,
        CancellationToken cancellationToken = default);
}
