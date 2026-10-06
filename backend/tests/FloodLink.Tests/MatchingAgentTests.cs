using FloodLink.Agents.Matching;
using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;

public class MatchingAgentTests
{
    private static AppDbContext CreateContext(params InventoryItem[] inventory)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        context.Depots.AddRange(
            new Depot { Id = 1, Name = "Depot One", Latitude = 6.9, Longitude = 79.8 },
            new Depot { Id = 2, Name = "Depot Two", Latitude = 7.0, Longitude = 79.9 });
        context.InventoryItems.AddRange(inventory);
        context.SaveChanges();
        return context;
    }

    private static TriagePlan Plan(double quantity, string needType = "Water") => new()
    {
        WorkflowRunId = Guid.NewGuid(),
        PriorityItems = [new()
        {
            ReportId = 1,
            ShelterId = 1,
            NeedType = needType,
            Quantity = quantity,
            PriorityScore = 90,
            Justification = "High priority"
        }]
    };

    [Fact]
    public async Task ExecuteAsync_MatchesStockAndPersistsProposal()
    {
        await using var context = CreateContext(new InventoryItem
        {
            Id = 1, DepotId = 1, ItemName = "Water", Unit = "bottles", QuantityAvailable = 100
        });
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(Plan(60));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(60, result.Data.Allocations.Single().Quantity);
        Assert.True(result.Data.AllocationProposalId > 0);
        Assert.Empty(result.Data.Unfulfillable);
        Assert.Single(context.AllocationProposals);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoStockMatches_ReturnsSafeFailure()
    {
        await using var context = CreateContext(new InventoryItem
        {
            Id = 1, DepotId = 1, ItemName = "Food", Unit = "packs", QuantityAvailable = 100
        });
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(Plan(60));

        Assert.False(result.Success);
        Assert.Equal("NO_STOCK_AVAILABLE", result.ErrorCode);
        Assert.Empty(context.AllocationProposals);
    }

    [Fact]
    public async Task ExecuteAsync_WhenStockIsPartial_ReturnsAllocationAndUnfulfillableNeed()
    {
        await using var context = CreateContext(new InventoryItem
        {
            Id = 1, DepotId = 1, ItemName = "Water", Unit = "bottles", QuantityAvailable = 40
        });
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(Plan(100));

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(40, result.Data.Allocations.Single().Quantity);
        Assert.Single(result.Data.Unfulfillable);
        Assert.Contains("40 of 100", result.Data.Unfulfillable[0].Reason);
    }
}
