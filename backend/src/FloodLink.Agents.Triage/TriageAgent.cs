using FloodLink.Contracts;

namespace FloodLink.Agents.Triage;

/// <summary>
/// Stub implementation of <see cref="ITriageAgent"/>.
/// Owned by Member A (Sharani) — do NOT implement logic here yet.
/// Replace <see cref="NotImplementedException"/> with real logic in your own session.
/// </summary>
public sealed class TriageAgent : ITriageAgent
{
    /// <inheritdoc />
    public Task<AgentResult<TriagePlan>> ExecuteAsync(
        Guid workflowRunId,
        CancellationToken cancellationToken = default)
    {
        // STUB — Member A: implement triage/urgency scoring logic here (Week 5).
        throw new NotImplementedException(
            "TriageAgent.ExecuteAsync is not yet implemented. " +
            "Member A (Sharani) will implement this in Week 5.");
    }
}
