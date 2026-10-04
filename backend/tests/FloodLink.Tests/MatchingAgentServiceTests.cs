using FloodLink.Api.Services;
using FloodLink.Contracts.Agents;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloodLink.Tests;

public class MatchingAgentServiceTests
{
    [Fact]
    public async Task MatchAsync_FullMatch_UsesDepotWithEnoughStockAndPersistsProposal()
    {
        await using var context = CreateContext();
        var depot = AddDepot(context, 1, 6m, 80m);
        AddInventory(context, 1, depot, "Water", 10m, 2m);
        await context.SaveChangesAsync();

        var result = await CreateService(context).MatchAsync(Plan(Need(12, "Water", 5m, 10)));

        var proposal = Assert.Single(result.Proposals);
        Assert.Equal(5m, proposal.Quantity);
        Assert.Equal("Proposed", proposal.Status);
        Assert.Empty(result.UnfulfillableNeeds);
        Assert.Equal("Proposed", Assert.Single(context.AllocationProposals).Status);
    }

    [Fact]
    public async Task MatchAsync_PartialMatch_AllocatesLargestAvailableAmount()
    {
        await using var context = CreateContext();
        var firstDepot = AddDepot(context, 1, 6m, 80m);
        var secondDepot = AddDepot(context, 2, 7m, 81m);
        AddInventory(context, 1, firstDepot, "Food", 3m, 0m);
        AddInventory(context, 2, secondDepot, "Food", 7m, 0m);
        await context.SaveChangesAsync();

        var result = await CreateService(context).MatchAsync(Plan(Need(12, "Food", 10m, 10)));

        var proposal = Assert.Single(result.Proposals);
        Assert.Equal(7m, proposal.Quantity);
        Assert.Equal(2, proposal.DepotId);
        Assert.Equal("Partial", proposal.Status);
        Assert.Equal(3m, Assert.Single(result.UnfulfillableNeeds).UnfulfilledQuantity);
    }

    [Fact]
    public async Task MatchAsync_ZeroStock_CreatesNoProposalAndReportsUnfulfilledNeed()
    {
        await using var context = CreateContext();
        var depot = AddDepot(context, 1, 6m, 80m);
        AddInventory(context, 1, depot, "Medical", 0m, 0m);
        await context.SaveChangesAsync();

        var result = await CreateService(context).MatchAsync(Plan(Need(12, "Medical", 4m, 10)));

        Assert.Empty(result.Proposals);
        Assert.Single(result.UnfulfillableNeeds);
        Assert.Empty(context.AllocationProposals);
    }

    [Fact]
    public async Task MatchAsync_PriorityTieBreak_HigherPriorityGetsLimitedStockFirst()
    {
        await using var context = CreateContext();
        var depot = AddDepot(context, 1, 6m, 80m);
        AddInventory(context, 1, depot, "Water", 5m, 0m);
        await context.SaveChangesAsync();

        var plan = Plan(
            Need(12, "Water", 5m, 20),
            Need(13, "Water", 5m, 10));

        var result = await CreateService(context).MatchAsync(plan);

        var proposal = Assert.Single(result.Proposals);
        Assert.Equal(12, proposal.ShelterId);
        Assert.Equal(5m, proposal.Quantity);
        Assert.Contains(result.UnfulfillableNeeds, need => need.ShelterId == 13);
    }

    private static MatchingAgentService CreateService(AppDbContext context) =>
        new(context, NullLogger<MatchingAgentService>.Instance);

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Depot AddDepot(AppDbContext context, int id, decimal latitude, decimal longitude)
    {
        var depot = new Depot { Id = id, Name = $"Depot {id}", Latitude = latitude, Longitude = longitude };
        context.Depots.Add(depot);
        return depot;
    }

    private static void AddInventory(AppDbContext context, int id, Depot depot, string itemName, decimal available, decimal reserved) =>
        context.InventoryItems.Add(new InventoryItem
        {
            Id = id,
            DepotId = depot.Id,
            Depot = depot,
            ItemName = itemName,
            QuantityAvailable = available,
            QuantityReserved = reserved,
            Unit = "unit"
        });

    private static TriagePlan Plan(params TriageNeed[] needs) => new() { Needs = needs.ToList() };

    private static TriageNeed Need(int shelterId, string itemName, decimal quantity, int priority) => new()
    {
        ShelterId = shelterId,
        ItemName = itemName,
        QuantityRequired = quantity,
        Priority = priority
    };
}
