using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Stock lifecycle against real PostgreSQL: reserve is all-or-nothing, commit and release keep
/// available/reserved consistent, and concurrent writes to the same stock row conflict (xmin)
/// instead of over-allocating.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class StockReservationServiceTests
{
    private static readonly DbContextOptions<AppDbContext> Options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(TestAppFactory.TestConnectionString)
        .Options;

    private readonly Guid _runId = Guid.NewGuid();
    private readonly int _waterId;
    private readonly int _foodId;

    public StockReservationServiceTests()
    {
        using var db = new AppDbContext(Options);
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
        db.InventoryItems.RemoveRange(db.InventoryItems);
        db.SaveChanges();

        var depot = new Depot { Name = "Stock Test Depot", Latitude = 6.9, Longitude = 79.9 };
        var shelter = new Shelter { Name = "Stock Test Shelter", Capacity = 100, Status = "Active", Latitude = 7.0, Longitude = 80.0 };
        db.AddRange(depot, shelter, new WorkflowRun { Id = _runId, Objective = "Stock test" });
        db.SaveChanges();

        var water = new InventoryItem { DepotId = depot.Id, ItemName = "Water", Unit = "bottles", QuantityAvailable = 100 };
        var food = new InventoryItem { DepotId = depot.Id, ItemName = "Food", Unit = "packs", QuantityAvailable = 10 };
        db.InventoryItems.AddRange(water, food);
        db.AllocationProposals.AddRange(
            new AllocationProposalEntity { WorkflowRunId = _runId, DepotId = depot.Id, ShelterId = shelter.Id, ItemName = "Water", Quantity = 60 },
            new AllocationProposalEntity { WorkflowRunId = _runId, DepotId = depot.Id, ShelterId = shelter.Id, ItemName = "Food", Quantity = 5 });
        db.SaveChanges();
        _waterId = water.Id;
        _foodId = food.Id;
    }

    private static AppDbContext NewDb() => new(Options);

    [Fact]
    public async Task Reserve_HoldsStock_ThenCommit_TakesItOutOfTheDepot()
    {
        await using (var db = NewDb())
        {
            var result = await new StockReservationService(db).ReserveForRunAsync(_runId);
            Assert.True(result.Succeeded);
        }

        await using (var db = NewDb())
        {
            var water = await db.InventoryItems.SingleAsync(i => i.Id == _waterId);
            Assert.Equal(100, water.QuantityAvailable);
            Assert.Equal(60, water.QuantityReserved);
            Assert.All(db.AllocationProposals, p => Assert.Equal("Reserved", p.Status));

            var commit = await new StockReservationService(db).StageCommitAsync(_runId);
            Assert.True(commit.Succeeded);
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb())
        {
            var water = await db.InventoryItems.SingleAsync(i => i.Id == _waterId);
            Assert.Equal(40, water.QuantityAvailable);
            Assert.Equal(0, water.QuantityReserved);
            Assert.All(db.AllocationProposals, p => Assert.Equal("Committed", p.Status));
        }
    }

    [Fact]
    public async Task Reserve_IsAllOrNothing_WhenOneLineIsShort()
    {
        await using (var db = NewDb())
        {
            // Someone else already holds 8 of the 10 food packs, so the 5 requested don't fit.
            var food = await db.InventoryItems.SingleAsync(i => i.Id == _foodId);
            food.QuantityReserved = 8;
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb())
        {
            var result = await new StockReservationService(db).ReserveForRunAsync(_runId);
            Assert.False(result.Succeeded);
            Assert.Equal("INSUFFICIENT_STOCK", result.ErrorCode);
            await db.SaveChangesAsync(); // nothing pending: the water line was not reserved either
        }

        await using (var db = NewDb())
        {
            Assert.Equal(0, (await db.InventoryItems.SingleAsync(i => i.Id == _waterId)).QuantityReserved);
            Assert.All(db.AllocationProposals, p => Assert.Equal("Proposed", p.Status));
        }
    }

    [Fact]
    public async Task Release_ReturnsReservedStock()
    {
        await using (var db = NewDb())
            await new StockReservationService(db).ReserveForRunAsync(_runId);

        await using (var db = NewDb())
        {
            var result = await new StockReservationService(db).ReleaseForRunAsync(_runId);
            Assert.True(result.Succeeded);
        }

        await using (var db = NewDb())
        {
            Assert.Equal(0, (await db.InventoryItems.SingleAsync(i => i.Id == _waterId)).QuantityReserved);
            Assert.All(db.AllocationProposals, p => Assert.Equal("Released", p.Status));
        }
    }

    [Fact]
    public async Task ConcurrentWrite_ToTheSameStock_IsRejected_NotOverwritten()
    {
        await using var first = NewDb();
        await using var second = NewDb();

        // The first request has read the stock row...
        await first.InventoryItems.ToListAsync();

        // ...when a second request changes the same row and saves first.
        var water = await second.InventoryItems.SingleAsync(i => i.Id == _waterId);
        water.QuantityAvailable = 10;
        await second.SaveChangesAsync();

        // The first request's reservation is based on stale stock: it must conflict.
        var result = await new StockReservationService(first).ReserveForRunAsync(_runId);

        Assert.False(result.Succeeded);
        Assert.Equal("STOCK_CONFLICT", result.ErrorCode);
        await using var check = NewDb();
        var stored = await check.InventoryItems.SingleAsync(i => i.Id == _waterId);
        Assert.Equal(10, stored.QuantityAvailable);
        Assert.Equal(0, stored.QuantityReserved);
    }

    [Fact]
    public async Task ManualReserve_RefusesMoreThanFreeStock()
    {
        await using var db = NewDb();
        var service = new StockReservationService(db);

        var (ok, item) = await service.ReserveItemAsync(_waterId, 70);
        var (tooMuch, _) = await service.ReserveItemAsync(_waterId, 40);

        Assert.True(ok.Succeeded);
        Assert.Equal(70, item!.QuantityReserved);
        Assert.False(tooMuch.Succeeded);
        Assert.Equal("INSUFFICIENT_STOCK", tooMuch.ErrorCode);
    }
}
