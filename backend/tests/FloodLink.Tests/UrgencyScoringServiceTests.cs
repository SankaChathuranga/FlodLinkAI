using FloodLink.Domain.Entities;
using FloodLink.Infrastructure.Services;
using Xunit;

namespace FloodLink.Tests;

public class UrgencyScoringServiceTests
{
    private readonly UrgencyScoringService _service;

    public UrgencyScoringServiceTests()
    {
        _service = new UrgencyScoringService();
    }

    [Fact]
    public void CalculateUrgencyScore_OverCapacityMedicalLongResupply_Returns100()
    {
        var shelter = new Shelter
        {
            Capacity = 100,
            CurrentOccupancy = 120
        };

        DateTime lastResupply = DateTime.UtcNow.AddHours(-50);
        int score = _service.CalculateUrgencyScore(shelter, "Medical", lastResupply);

        // 35 (over capacity) + 40 (medical) + 25 (>48h) = 100
        Assert.Equal(100, score);
    }

    [Fact]
    public void CalculateUrgencyScore_LowOccupancyOtherRecentResupply_ReturnsLowScore()
    {
        var shelter = new Shelter
        {
            Capacity = 100,
            CurrentOccupancy = 10
        };

        DateTime lastResupply = DateTime.UtcNow.AddHours(-2);
        int score = _service.CalculateUrgencyScore(shelter, "Other", lastResupply);

        // 5 (low occupancy) + 10 (other) + 5 (<12h) = 20
        Assert.Equal(20, score);
    }

    [Fact]
    public void CalculateUrgencyScore_NoResupplyDate_UsesDefaultResupplyScore()
    {
        var shelter = new Shelter
        {
            Capacity = 100,
            CurrentOccupancy = 85
        };

        int score = _service.CalculateUrgencyScore(shelter, "Water", lastResupplyTimeUtc: null);

        // 25 (high occupancy) + 35 (water) + 20 (default resupply) = 80
        Assert.Equal(80, score);
    }

    [Fact]
    public void CalculateUrgencyScore_ClampsScoreBetween0And100()
    {
        var shelter = new Shelter
        {
            Capacity = 50,
            CurrentOccupancy = 200
        };

        int score = _service.CalculateUrgencyScore(shelter, "Medical", DateTime.UtcNow.AddDays(-5));
        Assert.True(score <= 100);
        Assert.True(score >= 0);
    }
}
