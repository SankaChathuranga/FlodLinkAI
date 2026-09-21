using FloodLink.Contracts;

namespace FloodLink.Agents.Validation;

/// <summary>
/// Defines the public contract for the Validation/Safety Agent (Member D — Ijini).
/// Performs deterministic, rule-based checks (stock availability, vehicle capacity,
/// reserve thresholds, coordinate sanity) on the complete plan before it reaches
/// the human coordinator for approval.
/// </summary>
/// <remarks>
/// <para>
/// Implementation is owned by Member D. This agent is intentionally deterministic —
/// no LLM call — per the spec requirement that LLM-as-judge is not the sole evaluation
/// method. See CONTRIBUTING.md for scope rules.
/// </para>
/// <para>
/// Every agent must return <see cref="AgentResult{T}"/> — never throw for
/// expected/anticipated failures.
/// </para>
/// </remarks>
public interface IValidationAgent
{
    /// <summary>
    /// Runs all deterministic safety checks against the complete plan.
    /// </summary>
    /// <param name="route">
    /// The route output by the Route/ETA Agent, which carries the workflowRunId and
    /// allocationProposalId needed to load the full plan for validation.
    /// </param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <returns>
    /// An <see cref="AgentResult{ValidationResults}"/> containing all check results
    /// and an overall pass/fail status. On <see cref="ValidationResults.OverallPassed"/>
    /// = true the workflow transitions to <c>PendingApproval</c>; on false it transitions
    /// to <c>Failed</c>.
    /// </returns>
    Task<AgentResult<ValidationResults>> ExecuteAsync(
        Route route,
        CancellationToken cancellationToken = default);
}
