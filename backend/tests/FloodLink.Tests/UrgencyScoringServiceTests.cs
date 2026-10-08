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

    [Fact]
    public void CalculateUrgencyScore_ZeroPeopleOccupancy_ReturnsLowOccupancyScore()
    {
        var shelter = new Shelter
        {
            Capacity = 100,
            CurrentOccupancy = 0
        };

        DateTime recentResupply = DateTime.UtcNow.AddHours(-1);
        int score = _service.CalculateUrgencyScore(shelter, "Food", recentResupply);

        // 5 (low occupancy, 0%) + 25 (food) + 5 (<12h resupply) = 35
        Assert.Equal(35, score);
    }

    [Fact]
    public void CalculateUrgencyScore_ZeroCapacityShelter_HandlesDivisionByZeroGracefully()
    {
        var shelter = new Shelter
        {
            Capacity = 0,
            CurrentOccupancy = 5
        };

        DateTime resupplyTime = DateTime.UtcNow.AddHours(-30);
        int score = _service.CalculateUrgencyScore(shelter, "Water", resupplyTime);

        // 35 (capacity 0 defaults ratio to 1.0 -> 35 pts) + 35 (water) + 18 (24-48h resupply) = 88
        Assert.Equal(88, score);
    }

    [Fact]
    public void CalculateUrgencyScore_NullShelter_ReturnsDefaultShelterFallbackScore()
    {
        DateTime recentResupply = DateTime.UtcNow.AddHours(-5);
        int score = _service.CalculateUrgencyScore(shelter: null, needType: "Medical", lastResupplyTimeUtc: recentResupply);

        // 15 (default shelter fallback) + 40 (medical) + 5 (<12h resupply) = 60
        Assert.Equal(60, score);
    }

    [Fact]
    public void CalculateUrgencyScore_NullOrWhitespaceNeedType_ReturnsDefaultNeedTypeScore()
    {
        var shelter = new Shelter { Capacity = 100, CurrentOccupancy = 50 };
        DateTime resupplyTime = DateTime.UtcNow.AddHours(-20);

        int nullNeedScore = _service.CalculateUrgencyScore(shelter, needType: null!, resupplyTime);
        int emptyNeedScore = _service.CalculateUrgencyScore(shelter, needType: "   ", resupplyTime);

        // 15 (50% occupancy ratio) + 10 (default need) + 10 (12-24h resupply) = 35
        Assert.Equal(35, nullNeedScore);
        Assert.Equal(35, emptyNeedScore);
    }

    [Fact]
    public void CalculateUrgencyScore_MixedCaseAndPaddedNeedType_ParsesNeedTypeCorrectly()
    {
        var shelter = new Shelter { Capacity = 100, CurrentOccupancy = 50 };
        DateTime resupplyTime = DateTime.UtcNow.AddHours(-20);

        int medicalScore = _service.CalculateUrgencyScore(shelter, "  mEdIcAl  ", resupplyTime);
        int repairScore = _service.CalculateUrgencyScore(shelter, "SHELTER-REPAIR", resupplyTime);

        // 15 + 40 + 10 = 65
        Assert.Equal(65, medicalScore);

        // 15 + 15 + 10 = 40
        Assert.Equal(40, repairScore);
    }

    [Fact]
    public void CalculateUrgencyScore_ResupplyTimeIntervals_CalculatesCorrectTimeFactor()
    {
        var shelter = new Shelter { Capacity = 100, CurrentOccupancy = 10 }; // 5 pts

        // < 12h -> +5 pts
        int recentScore = _service.CalculateUrgencyScore(shelter, "Other", DateTime.UtcNow.AddHours(-6));
        Assert.Equal(20, recentScore); // 5 + 10 + 5 = 20

        // 12-24h -> +10 pts
        int mid1Score = _service.CalculateUrgencyScore(shelter, "Other", DateTime.UtcNow.AddHours(-15));
        Assert.Equal(25, mid1Score); // 5 + 10 + 10 = 25

        // 24-48h -> +18 pts
        int mid2Score = _service.CalculateUrgencyScore(shelter, "Other", DateTime.UtcNow.AddHours(-30));
        Assert.Equal(33, mid2Score); // 5 + 10 + 18 = 33

        // > 48h -> +25 pts
        int oldScore = _service.CalculateUrgencyScore(shelter, "Other", DateTime.UtcNow.AddHours(-60));
        Assert.Equal(40, oldScore); // 5 + 10 + 25 = 40
    }

    [Fact]
    public void CalculateUrgencyScore_ShelterRepairWithSpace_ScoresAsShelterRepair()
    {
        var shelter = new Shelter
        {
            Capacity = 100,
            CurrentOccupancy = 50
        };

        DateTime lastResupply = DateTime.UtcNow.AddHours(-10);
        int score = _service.CalculateUrgencyScore(shelter, "Shelter Repair", lastResupply);

        Assert.Equal(35, score);
    }
}

