using FloodLink.Contracts;

namespace FloodLink.Agents.Validation;

/// <summary>
/// Stub implementation of <see cref="IValidationAgent"/>.
/// Owned by Member D (Ijini) — do NOT implement logic here yet.
/// Replace <see cref="NotImplementedException"/> with real deterministic validation
/// rules in your own session (Week 5). Remember: no LLM call — rule-based code only.
/// </summary>
public sealed class ValidationAgent : IValidationAgent
{
    /// <inheritdoc />
    public Task<AgentResult<ValidationResults>> ExecuteAsync(
        Route route,
        CancellationToken cancellationToken = default)
    {
        // STUB — Member D: implement deterministic safety checks here (Week 5).
        // Checks to implement (from spec Section 3.3): stock availability,
        // vehicle/truck capacity, reserve-minimum thresholds, coordinate sanity.
        // Each check must produce a ValidationCheck record with CheckName + Passed + ViolationDetail.
        throw new NotImplementedException(
            "ValidationAgent.ExecuteAsync is not yet implemented. " +
            "Member D (Ijini) will implement this in Week 5.");
    }
}
