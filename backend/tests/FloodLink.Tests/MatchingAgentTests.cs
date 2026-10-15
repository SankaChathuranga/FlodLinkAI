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

    private static TriagePriorityItem Need(int shelterId, string needType, double quantity, double score = 50, int reportId = 1) => new()
    {
        ReportId = reportId,
        ShelterId = shelterId,
        NeedType = needType,
        Quantity = quantity,
        PriorityScore = score,
        Justification = "Test need"
    };

    // Needs are passed in the order Triage ranks them (highest priority first).
    private static TriagePlan PlanOf(params TriagePriorityItem[] needs) => new()
    {
        WorkflowRunId = Guid.NewGuid(),
        PriorityItems = needs
    };

    private static InventoryItem Stock(int id, int depotId, string itemName, double quantity, int reserved = 0) => new()
    {
        Id = id, DepotId = depotId, ItemName = itemName, Unit = "units", QuantityAvailable = quantity, QuantityReserved = reserved
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

    [Fact]
    public async Task ExecuteAsync_RerunForSameWorkflow_ReplacesPreviousRows_AndLeavesOtherRunsAlone()
    {
        await using var context = CreateContext(new InventoryItem
        {
            Id = 1, DepotId = 1, ItemName = "Water", Unit = "bottles", QuantityAvailable = 100
        });
        var agent = new MatchingAgent(context);
        var otherRun = await agent.ExecuteAsync(Plan(10));
        var plan = Plan(60);

        var first = await agent.ExecuteAsync(plan);
        var second = await agent.ExecuteAsync(plan);

        Assert.True(first.Success && second.Success);
        var rows = context.AllocationProposals.Where(row => row.WorkflowRunId == plan.WorkflowRunId).ToList();
        Assert.Equal(second.Data!.AllocationProposalId, Assert.Single(rows).Id);
        Assert.NotEqual(first.Data!.AllocationProposalId, second.Data.AllocationProposalId);
        Assert.Single(context.AllocationProposals, row => row.WorkflowRunId == otherRun.Data!.WorkflowRunId);
    }

    // ── Edge cases (Gap 4) ────────────────────────────────────────────────────

    // M5: the in-memory stock copy is shared across needs in one plan.
    [Fact]
    public async Task ExecuteAsync_TwoNeedsSameItem_DoesNotDoubleCountStock()
    {
        await using var context = CreateContext(Stock(1, 1, "Water", 50));
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf(Need(1, "Water", 30, 90), Need(2, "Water", 30, 80)));

        Assert.True(result.Success);
        Assert.Equal(30, result.Data!.Allocations.Single(a => a.ShelterId == 1).Quantity);
        Assert.Equal(20, result.Data.Allocations.Single(a => a.ShelterId == 2).Quantity);
        Assert.Equal(2, Assert.Single(result.Data.Unfulfillable).ShelterId);
    }

    // M2 + D7 naming rule: only an exact (case-insensitive) name match counts; no synonyms or substrings.
    [Fact]
    public async Task ExecuteAsync_ItemNameNotEqualToNeedType_IsNotMatched()
    {
        await using var context = CreateContext(
            Stock(1, 1, "Bottled water", 100), Stock(2, 1, "First aid kit", 100), Stock(3, 2, "Food", 10));
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf(Need(1, "Water", 20, 90), Need(1, "Medical", 5, 80), Need(1, "Food", 10, 70)));

        Assert.True(result.Success);
        Assert.Equal("Food", Assert.Single(result.Data!.Allocations).ItemName);
        Assert.Equal(["Water", "Medical"], result.Data.Unfulfillable.Select(u => u.ItemName));
        Assert.All(result.Data.Unfulfillable, u => Assert.Equal("No stock is available at any depot.", u.Reason));
    }

    // M1: when stock is short, the top-ranked need is filled before lower-ranked ones.
    [Fact]
    public async Task ExecuteAsync_HigherPriorityNeedServedFirst()
    {
        await using var context = CreateContext(Stock(1, 1, "Medical", 10));
        var agent = new MatchingAgent(context);

        // Shelter 7 is ranked first despite the higher shelter and report ids.
        var result = await agent.ExecuteAsync(PlanOf(
            Need(7, "Medical", 10, score: 95, reportId: 9),
            Need(3, "Medical", 10, score: 40, reportId: 2)));

        Assert.True(result.Success);
        var allocation = Assert.Single(result.Data!.Allocations);
        Assert.Equal(7, allocation.ShelterId);
        Assert.Equal(10, allocation.Quantity);
        Assert.Equal(3, Assert.Single(result.Data.Unfulfillable).ShelterId);
    }

    // M3 + deterministic order: depot 1 is used before depot 2 even though depot 2's row has the lower id.
    [Fact]
    public async Task ExecuteAsync_NeedSplitAcrossDepots_InDepotIdOrder()
    {
        await using var context = CreateContext(Stock(1, 2, "Food", 10), Stock(2, 1, "Food", 10));
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf(Need(1, "Food", 15)));

        Assert.True(result.Success);
        Assert.Collection(result.Data!.Allocations,
            first => { Assert.Equal(1, first.DepotId); Assert.Equal(10, first.Quantity); },
            second => { Assert.Equal(2, second.DepotId); Assert.Equal(5, second.Quantity); });
        Assert.Empty(result.Data.Unfulfillable);
    }

    // M2: item name vs need type is case-insensitive.
    [Fact]
    public async Task ExecuteAsync_ItemNameDifferentCase_StillMatches()
    {
        await using var context = CreateContext(Stock(1, 1, "WATER", 25));
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf(Need(1, "water", 25)));

        Assert.True(result.Success);
        Assert.Equal(25, Assert.Single(result.Data!.Allocations).Quantity);
        Assert.Empty(result.Data.Unfulfillable);
    }

    // M3/M8: a depot with no stock never produces a zero-quantity allocation.
    [Fact]
    public async Task ExecuteAsync_DepotWithZeroStock_IsSkipped()
    {
        await using var context = CreateContext(Stock(1, 1, "Water", 0), Stock(2, 2, "Water", 30));
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf(Need(1, "Water", 20)));

        Assert.True(result.Success);
        var allocation = Assert.Single(result.Data!.Allocations);
        Assert.Equal(2, allocation.DepotId);
        Assert.Equal(20, allocation.Quantity);
        Assert.DoesNotContain(context.AllocationProposals, row => row.Quantity <= 0);
    }

    // Determinism (ADR-004, D6): identical stock and plan give identical output.
    [Fact]
    public async Task ExecuteAsync_SameInput_SameOutput()
    {
        static InventoryItem[] SeedStock() =>
        [
            Stock(1, 2, "Water", 15), Stock(2, 1, "Water", 10), Stock(3, 1, "Food", 5), Stock(4, 2, "Medical", 8)
        ];
        TriagePriorityItem[] needs =
        [
            Need(1, "Water", 20, 90), Need(2, "Food", 10, 70), Need(3, "Water", 10, 60), Need(4, "Blankets", 5, 50)
        ];

        await using var firstContext = CreateContext(SeedStock());
        await using var secondContext = CreateContext(SeedStock());
        var first = await new MatchingAgent(firstContext).ExecuteAsync(PlanOf(needs));
        var second = await new MatchingAgent(secondContext).ExecuteAsync(PlanOf(needs));

        Assert.True(first.Success && second.Success);
        Assert.Equal(first.Data!.Allocations, second.Data!.Allocations);
        Assert.Equal(first.Data.Unfulfillable, second.Data.Unfulfillable);
    }

    // M6: Matching never changes, reserves or decrements real inventory.
    [Fact]
    public async Task ExecuteAsync_DoesNotChangeInventory()
    {
        await using var context = CreateContext(Stock(1, 1, "Water", 40, reserved: 5), Stock(2, 2, "Water", 10));
        var before = context.InventoryItems.AsNoTracking().OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.QuantityAvailable, i.QuantityReserved, i.UpdatedAt }).ToList();
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf(Need(1, "Water", 100)));

        Assert.True(result.Success);
        var after = context.InventoryItems.AsNoTracking().OrderBy(i => i.Id)
            .Select(i => new { i.Id, i.QuantityAvailable, i.QuantityReserved, i.UpdatedAt }).ToList();
        Assert.Equal(before, after);
        Assert.Empty(context.StockReservations);
    }

    // M4: a partly covered need is listed with a partial-allocation reason.
    [Fact]
    public async Task ExecuteAsync_PartialStock_ReasonIsPartialAllocation()
    {
        await using var context = CreateContext(Stock(1, 1, "Food", 40));
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf(Need(5, "Food", 100)));

        Assert.True(result.Success);
        var gap = Assert.Single(result.Data!.Unfulfillable);
        Assert.Equal(5, gap.ShelterId);
        Assert.Equal("Food", gap.ItemName);
        Assert.Equal("Only 40 of 100 could be allocated.", gap.Reason);
    }

    // M4: a need that gets nothing is listed with the no-stock reason.
    // "exhausted" = an earlier need from the same shelter used all of that item first.
    [Theory]
    [InlineData("never-stocked")]
    [InlineData("exhausted")]
    public async Task ExecuteAsync_NoStock_ReasonIsNoStock(string scenario)
    {
        await using var context = CreateContext(Stock(1, 1, "Water", 50));
        var agent = new MatchingAgent(context);
        var plan = scenario == "never-stocked"
            ? PlanOf(Need(1, "Water", 50, 90), Need(1, "Medical", 30, 80))
            : PlanOf(Need(1, "Water", 50, 90, reportId: 1), Need(1, "Water", 30, 80, reportId: 2));

        var result = await agent.ExecuteAsync(plan);

        Assert.True(result.Success);
        var gap = Assert.Single(result.Data!.Unfulfillable);
        Assert.Equal(1, gap.ShelterId);
        Assert.Equal("No stock is available at any depot.", gap.Reason);
    }

    // M14: an empty plan is an expected failure value, not an exception.
    [Fact]
    public async Task ExecuteAsync_EmptyTriagePlan_ReturnsTypedFailure()
    {
        await using var context = CreateContext(Stock(1, 1, "Water", 50));
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf());

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("EMPTY_TRIAGE_PLAN", result.ErrorCode);
        Assert.Empty(context.AllocationProposals);
    }

    // Safety: for every depot and item, the total allocated never exceeds that depot's stock.
    [Fact]
    public async Task ExecuteAsync_TotalAllocatedNeverExceedsStock()
    {
        var inventory = new[]
        {
            Stock(1, 1, "Water", 30), Stock(2, 2, "water", 25), Stock(3, 1, "Food", 12),
            Stock(4, 2, "Food", 0), Stock(5, 2, "Medical", 7), Stock(6, 1, "Water", 5)
        };
        await using var context = CreateContext(inventory);
        var agent = new MatchingAgent(context);

        var result = await agent.ExecuteAsync(PlanOf(
            Need(1, "Water", 40, 95), Need(2, "Food", 10, 90), Need(3, "WATER", 40, 85),
            Need(4, "Medical", 10, 80), Need(5, "Food", 10, 70), Need(6, "Water", 5, 60)));

        Assert.True(result.Success);
        Assert.All(result.Data!.Allocations, a => Assert.True(a.Quantity > 0));
        var allocatedByDepotItem = result.Data.Allocations
            .GroupBy(a => (a.DepotId, Item: a.ItemName.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.Sum(a => a.Quantity));
        var stockByDepotItem = inventory
            .GroupBy(i => (i.DepotId, Item: i.ItemName.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.Sum(i => i.QuantityAvailable));
        Assert.All(allocatedByDepotItem, pair => Assert.True(pair.Value <= stockByDepotItem[pair.Key],
            $"Depot {pair.Key.DepotId} '{pair.Key.Item}': allocated {pair.Value}, stock {stockByDepotItem[pair.Key]}"));
        // Every unit asked for is either allocated or reported as unfulfillable.
        Assert.Equal(6, result.Data.Allocations.Select(a => a.ShelterId)
            .Union(result.Data.Unfulfillable.Select(u => u.ShelterId)).Distinct().Count());
    }
}
