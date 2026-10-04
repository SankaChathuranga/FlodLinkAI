using FloodLink.Contracts;

namespace FloodLink.Agents.Routing;

/// <summary>
/// Defines the public contract for the Route/ETA Agent (Member C — Sanka).
/// Takes an <see cref="AllocationProposal"/> and calls the external maps/routing API
/// to compute delivery distance and ETA, producing a <see cref="Contracts.Route"/>.
/// </summary>
/// <remarks>
/// Implementation is owned by Member C. See CONTRIBUTING.md for scope rules:
/// every agent must return <see cref="AgentResult{T}"/> — never throw for
/// expected/anticipated failures. The implementation should handle maps-API timeouts,
/// invalid coordinates, and rate-limit errors via the AgentResult failure path.
/// </remarks>
public interface IRoutingAgent
{
    /// <summary>
    /// Runs the Route/ETA Agent for the given allocation proposal.
    /// </summary>
    /// <param name="allocationProposal">
    /// The matched allocation output by the Logistics Agent. Contains the depot and
    /// shelter coordinates used to call the external routing API.
    /// </param>
    /// <param name="cancellationToken">Propagated cancellation token.</param>
    /// <returns>
    /// An <see cref="AgentResult{Route}"/> containing distance/ETA/polyline on success,
    /// or an error code + message on anticipated failure (e.g. maps API timeout).
    /// </returns>
    Task<AgentResult<Route>> ExecuteAsync(
        AllocationProposal allocationProposal,
        CancellationToken cancellationToken = default);
}
