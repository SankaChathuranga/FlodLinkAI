using FloodLink.Contracts;

namespace FloodLink.Agents.Matching;

/// <summary>
/// Defines the public contract for the Logistics/Matching Agent (Member B — Eshini).
/// Takes the prioritized <see cref="TriagePlan"/> and matches needs against real
/// inventory, producing an <see cref="AllocationProposal"/>.
/// </summary>
/// <remarks>
/// Implementation is owned by Member B. See CONTRIBUTING.md for scope rules:
/// every agent must return <see cref="AgentResult{T}"/> — never throw for
/// expected/anticipated failures.
/// </remarks>
public interface IMatchingAgent
{
    /// <summary>
    /// Runs the Logistics/Matching Agent against the given triage plan.
    /// </summary>
    /// <param name="triagePlan">
    /// The prioritized plan output by the Triage Agent. Unmatched needs must be
    /// explicitly listed in <see cref="AllocationProposal.Unfulfillable"/> —
    /// never silently dropped.
    /// </param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <returns>
    /// An <see cref="AgentResult{AllocationProposal}"/> on success, or an error
    /// code + message on anticipated failure.
    /// </returns>
    /// <param name="revisionNotes">
    /// The coordinator's notes when the plan was sent back for revision (e.g. "use depot 2").
    /// Untrusted text: passed to the LLM as data only.
    /// </param>
    Task<AgentResult<AllocationProposal>> ExecuteAsync(
        TriagePlan triagePlan,
        string? revisionNotes = null,
        CancellationToken cancellationToken = default);
}
