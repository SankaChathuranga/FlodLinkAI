using FloodLink.Agents.Triage;
using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;

public class TriageAgentTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        // Seed default user
        context.Users.Add(new User
        {
            Id = 1,
            Name = "System Tester",
            Role = "Admin",
            Phone = "+94771234567",
            HashedPassword = "hashed_pass"
        });

        context.SaveChanges();
        return context;
    }

    // The agent triages an existing run; with no report list it covers every unresolved report.
    private static async Task<Guid> SeedRunAsync(AppDbContext context)
    {
        var run = new WorkflowRun { Id = Guid.NewGuid(), Objective = "Triage test" };
        context.WorkflowRuns.Add(run);
        await context.SaveChangesAsync();
        return run.Id;
    }

    [Fact]
    public async Task ExecuteAsync_OvercrowdedShelterCriticalMedicalNeed_ReturnsScoreAbove80AndRankedFirst()
    {
        // Arrange
        using var context = CreateDbContext();
        var scoringService = new UrgencyScoringService();
        var agent = new TriageAgent(context, scoringService);

        var shelter = new Shelter
        {
            Id = 10,
            Name = "Overcrowded Relief Hub",
            Capacity = 100,
            CurrentOccupancy = 130, // 130% capacity -> 35 pts
            Status = "Active",
            Latitude = 6.9,
            Longitude = 79.8
        };
        context.Shelters.Add(shelter);

        // Report 1: Medical need at overcrowded shelter (Medical = 40 pts, Overcapacity = 35 pts, default resupply = 20 pts -> 95 pts)
        var criticalReport = new Report
        {
            Id = 101,
            ShelterId = 10,
            ReportedBy = 1,
            NeedType = "Medical",
            QuantityNeeded = 50,
            Status = "New",
            CreatedAt = DateTime.UtcNow
        };

        // Report 2: Other need at normal shelter
        var normalShelter = new Shelter
        {
            Id = 11,
            Name = "Normal Shelter",
            Capacity = 100,
            CurrentOccupancy = 20, // 20% -> 5 pts
            Status = "Active",
            Latitude = 6.9,
            Longitude = 79.8
        };
        context.Shelters.Add(normalShelter);

        var lowReport = new Report
        {
            Id = 102,
            ShelterId = 11,
            ReportedBy = 1,
            NeedType = "Other", // 10 pts
            QuantityNeeded = 5,
            Status = "New",
            CreatedAt = DateTime.UtcNow
        };

        context.Reports.AddRange(criticalReport, lowReport);
        await context.SaveChangesAsync();

        Guid workflowRunId = await SeedRunAsync(context);

        // Act
        var result = await agent.ExecuteAsync(workflowRunId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(workflowRunId, result.Data.WorkflowRunId);
        Assert.Equal(2, result.Data.PriorityItems.Count);

        var topItem = result.Data.PriorityItems.First();
        Assert.Equal(101, topItem.ReportId);
        Assert.Equal("Medical", topItem.NeedType);
        Assert.True(topItem.PriorityScore > 80, $"Expected score > 80, but got {topItem.PriorityScore}");
        Assert.Contains("Critical Priority", topItem.Justification);
    }

    [Fact]
    public async Task ExecuteAsync_MultiReportRanking_OrdersDescendingByPriorityScore()
    {
        // Arrange
        using var context = CreateDbContext();
        var scoringService = new UrgencyScoringService();
        var agent = new TriageAgent(context, scoringService);

        var shelter = new Shelter
        {
            Id = 20,
            Name = "Central Shelter",
            Capacity = 100,
            CurrentOccupancy = 80,
            Status = "Active",
            Latitude = 6.9,
            Longitude = 79.8
        };
        context.Shelters.Add(shelter);

        // Add 3 reports with varying need types (Medical=40, Water=35, Food=25)
        var foodReport = new Report { Id = 201, ShelterId = 20, ReportedBy = 1, NeedType = "Food", QuantityNeeded = 100, Status = "New", CreatedAt = DateTime.UtcNow };
        var medicalReport = new Report { Id = 202, ShelterId = 20, ReportedBy = 1, NeedType = "Medical", QuantityNeeded = 10, Status = "New", CreatedAt = DateTime.UtcNow };
        var waterReport = new Report { Id = 203, ShelterId = 20, ReportedBy = 1, NeedType = "Water", QuantityNeeded = 50, Status = "New", CreatedAt = DateTime.UtcNow };

        context.Reports.AddRange(foodReport, medicalReport, waterReport);
        await context.SaveChangesAsync();

        Guid workflowRunId = await SeedRunAsync(context);

        // Act
        var result = await agent.ExecuteAsync(workflowRunId);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        var items = result.Data.PriorityItems;
        Assert.Equal(3, items.Count);

        // Verify descending order
        for (int i = 0; i < items.Count - 1; i++)
        {
            Assert.True(items[i].PriorityScore >= items[i + 1].PriorityScore,
                $"Item at index {i} ({items[i].PriorityScore}) should be >= item at index {i + 1} ({items[i + 1].PriorityScore})");
        }

        Assert.Equal(202, items[0].ReportId); // Medical (highest)
        Assert.Equal(203, items[1].ReportId); // Water (second)
        Assert.Equal(201, items[2].ReportId); // Food (third)
    }

    [Fact]
    public async Task ExecuteAsync_EdgeCaseMissingFields_HandlesNullShelterCapacityAndNoResupplyGracefully()
    {
        // Arrange
        using var context = CreateDbContext();
        var scoringService = new UrgencyScoringService();
        var agent = new TriageAgent(context, scoringService);

        // Shelter with 0 capacity (or missing fields scenario)
        var edgeShelter = new Shelter
        {
            Id = 30,
            Name = "Temporary Setup",
            Capacity = 0, // Edge case: zero capacity
            CurrentOccupancy = 15,
            Status = "Active",
            Latitude = 0.0,
            Longitude = 0.0
        };
        context.Shelters.Add(edgeShelter);

        // Report without prior resupply records
        var report = new Report
        {
            Id = 301,
            ShelterId = 30,
            ReportedBy = 1,
            NeedType = "Shelter-Repair",
            QuantityNeeded = 3,
            Status = "New",
            CreatedAt = DateTime.UtcNow
        };
        context.Reports.Add(report);
        await context.SaveChangesAsync();

        Guid workflowRunId = await SeedRunAsync(context);

        // Act
        var result = await agent.ExecuteAsync(workflowRunId);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.PriorityItems);

        var item = result.Data.PriorityItems[0];
        Assert.Equal(301, item.ReportId);
        Assert.NotNull(item.Justification);
        Assert.NotEmpty(item.Justification);
        Assert.True(item.PriorityScore >= 0 && item.PriorityScore <= 100);

        // Verify TriagePlanEntity persisted in DB
        var persistedPlan = await context.TriagePlans.FirstOrDefaultAsync(p => p.CreatedByAgentRunId == workflowRunId);
        Assert.NotNull(persistedPlan);
        Assert.Contains("301", persistedPlan.PlanSummaryJson);
    }

    [Fact]
    public async Task ExecuteAsync_ScopesToTheRunsReports_AndRecordsToolCalls()
    {
        using var context = CreateDbContext();
        var agent = new TriageAgent(context, new UrgencyScoringService());
        var shelter = new Shelter { Id = 50, Name = "Scoped", Capacity = 100, CurrentOccupancy = 50, Status = "Active" };
        context.Shelters.Add(shelter);
        context.Reports.AddRange(
            new Report { Id = 501, ShelterId = 50, ReportedBy = 1, NeedType = "Water", QuantityNeeded = 10, Status = "New" },
            new Report { Id = 502, ShelterId = 50, ReportedBy = 1, NeedType = "Food", QuantityNeeded = 10, Status = "New" });
        var run = new WorkflowRun { Id = Guid.NewGuid(), Objective = "Scoped", ReportIds = [502] };
        context.WorkflowRuns.Add(run);
        await context.SaveChangesAsync();

        var result = await agent.ExecuteAsync(run.Id);

        Assert.True(result.Success);
        Assert.Equal(502, Assert.Single(result.Data!.PriorityItems).ReportId);
        Assert.Contains(result.ToolCalls, call => call.Tool == "db.reports.read");
        Assert.Null(run.PlanJson); // the orchestrator, not the agent, writes the plan
    }

    [Fact]
    public async Task ExecuteAsync_UnknownRun_Fails()
    {
        using var context = CreateDbContext();
        var agent = new TriageAgent(context, new UrgencyScoringService());

        var result = await agent.ExecuteAsync(Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal("WORKFLOW_NOT_FOUND", result.ErrorCode);
    }
}
