using FloodLink.Contracts;

namespace FloodLink.Agents.Matching;

/// <summary>
/// Stub implementation of <see cref="IMatchingAgent"/>.
/// Owned by Member B (Eshini) — do NOT implement logic here yet.
/// Replace <see cref="NotImplementedException"/> with real matching logic in your own session.
/// </summary>
public sealed class MatchingAgent : IMatchingAgent
{
    /// <inheritdoc />
    public Task<AgentResult<AllocationProposal>> ExecuteAsync(
        TriagePlan triagePlan,
        CancellationToken cancellationToken = default)
    {
        // STUB — Member B: implement inventory matching / allocation logic here (Week 5).
        throw new NotImplementedException(
            "MatchingAgent.ExecuteAsync is not yet implemented. " +
            "Member B (Eshini) will implement this in Week 5.");
    }
}
