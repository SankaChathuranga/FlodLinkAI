using System.Net;
using System.Net.Http.Json;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// DB-001..DB-009: behaviour that only a real PostgreSQL server enforces — migrations,
/// foreign keys, unique indexes, NOT NULL, delete rules and transactions. The EF Core
/// in-memory provider ignores all of these, so these tests run against floodlink_test.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class DatabaseTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;

    public DatabaseTests(TestAppFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
        _client = _factory.CreateClient();
    }

    private AppDbContext NewDb() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private static string SqlState(DbUpdateException ex) =>
        Assert.IsType<PostgresException>(ex.InnerException).SqlState;

    [Fact]
    public async Task DB001_AllMigrations_ApplyToEmptyDatabase()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestAppFactory.TestConnectionString.Replace("floodlink_test", "floodlink_migration_test"))
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureDeletedAsync();

        await db.Database.MigrateAsync();

        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.True(await db.Depots.AnyAsync()); // seed data arrived through the migrations
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task DB002_InventoryItem_WithUnknownDepot_IsRejectedByForeignKey()
    {
        await using var db = NewDb();
        db.InventoryItems.Add(new InventoryItem { DepotId = 99_999, ItemName = "Water", Unit = "bottles" });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, SqlState(ex));
    }

    [Fact]
    public async Task DB003_SameItemTwiceInOneDepot_IsRejectedByUniqueIndex()
    {
        await using var db = NewDb();
        db.InventoryItems.Add(new InventoryItem { DepotId = 1, ItemName = "Water", Unit = "bottles" }); // seed already has Water at depot 1

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, SqlState(ex));
    }

    [Fact]
    public async Task DB004_DepotWithAllocationProposal_CannotBeDeleted()
    {
        await using (var db = NewDb())
        {
            var run = new WorkflowRun { Id = Guid.NewGuid(), Objective = "DB-004" };
            db.WorkflowRuns.Add(run);
            db.AllocationProposals.Add(new AllocationProposalEntity
            {
                WorkflowRunId = run.Id, DepotId = 2, ShelterId = 1, ItemName = "Water", Quantity = 10
            });
            await db.SaveChangesAsync();
        }

        await using var db2 = NewDb();
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => db2.Database.ExecuteSqlRawAsync("DELETE FROM \"Depots\" WHERE \"Id\" = 2"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);
    }

    [Fact]
    public async Task DB005_DeletingShelter_CascadesToItsReports()
    {
        await using var db = NewDb();
        var shelterId = await db.Reports.Select(r => r.ShelterId).FirstAsync();
        Assert.True(await db.Reports.AnyAsync(r => r.ShelterId == shelterId));

        await db.Shelters.Where(s => s.Id == shelterId).ExecuteDeleteAsync();

        Assert.False(await db.Reports.AnyAsync(r => r.ShelterId == shelterId));
    }

    [Fact]
    public async Task DB006_RolledBackTransaction_LeavesNoData()
    {
        await using (var db = NewDb())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            db.Depots.Add(new Depot { Name = "Rolled-back depot", Latitude = 7, Longitude = 80 });
            await db.SaveChangesAsync();
            await tx.RollbackAsync();
        }

        await using var check = NewDb();
        Assert.False(await check.Depots.AnyAsync(d => d.Name == "Rolled-back depot"));
    }

    [Fact]
    public async Task DB007_RequiredColumn_NullIsRejected()
    {
        await using var db = NewDb();
        var ex = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Depots\" (\"Name\", \"Latitude\", \"Longitude\", \"CreatedAt\") VALUES (NULL, 7, 80, now())"));
        Assert.Equal(PostgresErrorCodes.NotNullViolation, ex.SqlState);
    }

    [Fact]
    public async Task DB008_CreateDuplicateItemThroughApi_Returns409NotServerError()
    {
        var response = await _client.PostAsJsonAsync("/api/inventory",
            new { depotId = 1, itemName = "Water", unit = "bottles", quantityAvailable = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(-5, HttpStatusCode.BadRequest)]
    [InlineData(25, HttpStatusCode.OK)]
    public async Task DB009_StockCheckIn_ValidatesQuantityAndPersists(double received, HttpStatusCode expected)
    {
        double before;
        await using (var db = NewDb())
            before = (await db.InventoryItems.SingleAsync(i => i.Id == 1)).QuantityAvailable;

        var response = await _client.PutAsJsonAsync("/api/inventory/1/check-in", new { quantityReceived = received });
        Assert.Equal(expected, response.StatusCode);

        await using var after = NewDb();
        var expectedQty = expected == HttpStatusCode.OK ? before + received : before;
        Assert.Equal(expectedQty, (await after.InventoryItems.SingleAsync(i => i.Id == 1)).QuantityAvailable);
    }
}
