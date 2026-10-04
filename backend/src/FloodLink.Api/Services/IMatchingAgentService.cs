using FloodLink.Contracts.Agents;

namespace FloodLink.Api.Services;

public interface IMatchingAgentService
{
    Task<MatchingResult> MatchAsync(
        TriagePlan triagePlan);
}