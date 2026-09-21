using FloodLink.Contracts;

namespace FloodLink.Agents.Routing;

/// <summary>
/// Stub implementation of <see cref="IRoutingAgent"/>.
/// Owned by Member C (Sanka) — this is your agent. Replace <see cref="NotImplementedException"/>
/// with real maps-API integration in a later session (Week 5).
/// </summary>
public sealed class RoutingAgent : IRoutingAgent
{
    /// <inheritdoc />
    public Task<AgentResult<Route>> ExecuteAsync(
        AllocationProposal allocationProposal,
        CancellationToken cancellationToken = default)
    {
        // STUB — Member C: implement OpenRouteService / Mapbox Directions API call here (Week 5).
        // Remember to handle: timeout, retry-limit, implausible coordinates, and API errors
        // via AgentResult.Fail(...) — do NOT throw for these anticipated failures.
        throw new NotImplementedException(
            "RoutingAgent.ExecuteAsync is not yet implemented. " +
            "Member C (Sanka) will implement this in Week 5.");
    }
}
