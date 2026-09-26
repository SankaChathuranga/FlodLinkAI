using FloodLink.Domain.Entities;

namespace FloodLink.Domain.Services;

/// <summary>
/// Service interface for rule-based auto-urgency scoring.
/// </summary>
public interface IUrgencyScoringService
{
    /// <summary>
    /// Calculates a rule-based urgency score (0-100) for a field report.
    /// Combines people-count/capacity ratio, need_type severity, and time-since-last-resupply.
    /// </summary>
    int CalculateUrgencyScore(Shelter? shelter, string needType, DateTime? lastResupplyTimeUtc = null);
}
