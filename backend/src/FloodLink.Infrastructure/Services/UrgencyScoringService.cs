using FloodLink.Domain.Entities;
using FloodLink.Domain.Services;

namespace FloodLink.Infrastructure.Services;

/// <summary>
/// Rule-based urgency scoring implementation.
/// Combines shelter occupancy ratio, need_type severity, and time-since-last-resupply into a 0-100 score.
/// </summary>
public class UrgencyScoringService : IUrgencyScoringService
{
    public int CalculateUrgencyScore(Shelter? shelter, string needType, DateTime? lastResupplyTimeUtc = null)
    {
        double score = 0;

        // 1. Occupancy & Capacity Factor (0 to 35 points)
        if (shelter != null)
        {
            double occupancyRatio = shelter.Capacity > 0
                ? (double)shelter.CurrentOccupancy / shelter.Capacity
                : 1.0;

            if (occupancyRatio >= 1.0)
                score += 35; // Over capacity
            else if (occupancyRatio >= 0.8)
                score += 25; // High occupancy
            else if (occupancyRatio >= 0.5)
                score += 15; // Moderate occupancy
            else
                score += 5;  // Low occupancy
        }
        else
        {
            score += 15; // Default fallback
        }

        // 2. Need Type Severity Factor (0 to 40 points)
        string normalizedNeed = needType?.Trim().ToLowerInvariant() ?? string.Empty;
        switch (normalizedNeed)
        {
            case "medical":
                score += 40;
                break;
            case "water":
                score += 35;
                break;
            case "food":
                score += 25;
                break;
            case "shelter-repair":
            case "shelterrepair":
            case "shelter repair":
                score += 15;
                break;

            default:
                score += 10;
                break;
        }

        // 3. Time-Since-Last-Resupply Factor (0 to 25 points)
        if (lastResupplyTimeUtc.HasValue)
        {
            double hoursSinceResupply = (DateTime.UtcNow - lastResupplyTimeUtc.Value).TotalHours;
            if (hoursSinceResupply >= 48)
                score += 25;
            else if (hoursSinceResupply >= 24)
                score += 18;
            else if (hoursSinceResupply >= 12)
                score += 10;
            else
                score += 5;
        }
        else
        {
            // If no resupply recorded yet, default to urgent resupply need
            score += 20;
        }

        return Math.Clamp((int)Math.Round(score), 0, 100);
    }
}
