using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Phase 5 (backend quality): search / sort / paging on the report, inventory, depot and audit
/// lists, depot editing with manager assignment, automatic UpdatedAt, and DTO validation errors.
/// Runs against the seeded floodlink_test database (5 reports, 2 depots, 4 stock lines).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BackendQualityTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _coordinator;
    private readonly HttpClient _depotManager;

    public BackendQualityTests(TestAppFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
        _coordinator = _factory.CreateCoordinatorClient();
        _depotManager = _factory.CreateClientAs(Roles.DepotManager);
    }

    private AppDbContext NewDb() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    private static List<int> Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();

    // ── 5.01 Reports ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Reports_ArePagedWithTotals()
    {
        var page = await JsonAsync(await _coordinator.GetAsync("/api/reports?sort=oldest&page=2&pageSize=2"));

        Assert.Equal(5, page.GetProperty("totalItems").GetInt32());
        Assert.Equal(3, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(2, page.GetProperty("page").GetInt32());
        Assert.Equal([3, 4], Ids(page)); // seed reports share CreatedAt, so oldest falls back to id order
    }

    [Fact]
    public async Task Reports_SearchMatchesShelterNameNeedTypeAndId()
    {
        Assert.Equal([3, 4], (Ids(await JsonAsync(await _coordinator.GetAsync("/api/reports?search=kaduwela&sort=oldest")))));
        Assert.Equal([3], Ids(await JsonAsync(await _coordinator.GetAsync("/api/reports?search=MEDICAL"))));
        Assert.Equal([5], Ids(await JsonAsync(await _coordinator.GetAsync("/api/reports?search=%23R-5"))));
    }

    [Fact]
    public async Task Reports_SortByQuantity_AndFiltersStillApply()
    {
        var byQuantity = Ids(await JsonAsync(await _coordinator.GetAsync("/api/reports?sort=quantity_desc")));
        Assert.Equal([1, 2, 5, 3, 4], byQuantity);

        var newOnly = Ids(await JsonAsync(await _coordinator.GetAsync("/api/reports?status=New&shelterId=1")));
        Assert.Equal([1], newOnly);
    }

    // ── 5.02 Inventory ────────────────────────────────────────────────────────

    [Fact]
    public async Task Inventory_SearchSortAndPage()
    {
        Assert.Equal([3, 4], Ids(await JsonAsync(await _depotManager.GetAsync("/api/inventory?search=kaduwela&sort=item"))).OrderBy(i => i));
        Assert.Equal([4], Ids(await JsonAsync(await _depotManager.GetAsync("/api/inventory?search=medical"))));

        var page = await JsonAsync(await _depotManager.GetAsync("/api/inventory?sort=quantity_desc&pageSize=3"));
        Assert.Equal([1, 3, 2], Ids(page));
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task Inventory_LowStock_KeepsLinesAtOrBelowTheReorderThreshold()
    {
        Assert.Empty(Ids(await JsonAsync(await _depotManager.GetAsync("/api/inventory?lowStock=true"))));

        // Medical at depot 2: 50 available, threshold 10. Holding 45 leaves 5 free.
        var reserve = await _depotManager.PutAsJsonAsync("/api/inventory/4/reserve", new { quantity = 45 });
        Assert.Equal(HttpStatusCode.OK, reserve.StatusCode);

        Assert.Equal([4], Ids(await JsonAsync(await _depotManager.GetAsync("/api/inventory?lowStock=true"))));
    }

    [Fact]
    public async Task Inventory_CheckIn_GoesThroughTheStockServiceAndStampsUpdatedAt()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var response = await _depotManager.PutAsJsonAsync("/api/inventory/2/check-in", new { quantityReceived = 25 });
        var item = await JsonAsync(response);

        Assert.Equal(275, item.GetProperty("quantityAvailable").GetDouble());
        Assert.True(item.GetProperty("updatedAt").GetDateTime() >= before);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _depotManager.PutAsJsonAsync("/api/inventory/999999/check-in", new { quantityReceived = 1 })).StatusCode);
    }

    // ── 5.03 Depots ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Depots_ArePagedAndShowTheirManager()
    {
        var page = await JsonAsync(await _coordinator.GetAsync("/api/depots?pageSize=1"));
        Assert.Equal(2, page.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());

        var depot = page.GetProperty("items")[0];
        Assert.Equal("Colombo Central Depot", depot.GetProperty("name").GetString());
        Assert.Equal("Kamal Perera", depot.GetProperty("managerName").GetString());
        Assert.Equal(2, depot.GetProperty("itemCount").GetInt32());

        Assert.Equal([2], Ids(await JsonAsync(await _coordinator.GetAsync("/api/depots?search=kaduwela"))));
    }

    [Fact]
    public async Task UpdateDepot_EditsAndReassignsManager()
    {
        var response = await _depotManager.PutAsJsonAsync("/api/depots/1",
            new { name = "Colombo Main Depot", latitude = 6.94, longitude = 79.85, managerId = (int?)null });
        var depot = await JsonAsync(response);

        Assert.Equal("Colombo Main Depot", depot.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, depot.GetProperty("managerId").ValueKind);
        Assert.True(depot.GetProperty("updatedAt").GetDateTime() > depot.GetProperty("createdAt").GetDateTime());

        var assign = await _depotManager.PutAsJsonAsync("/api/depots/1",
            new { name = "Colombo Main Depot", latitude = 6.94, longitude = 79.85, managerId = TestAppFactory.DemoUserId(Roles.DepotManager) });
        Assert.Equal("Kamal Perera", (await JsonAsync(assign)).GetProperty("managerName").GetString());
    }

    [Fact]
    public async Task UpdateDepot_RejectsANonManagerAccount_AndUnknownDepot()
    {
        var coordinatorAsManager = await _depotManager.PutAsJsonAsync("/api/depots/1",
            new { name = "X", latitude = 6.9, longitude = 79.8, managerId = TestAppFactory.DemoUserId(Roles.Coordinator) });
        Assert.Equal(HttpStatusCode.BadRequest, coordinatorAsManager.StatusCode);

        var missing = await _depotManager.PutAsJsonAsync("/api/depots/999999",
            new { name = "X", latitude = 6.9, longitude = 79.8 });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // ── 5.04 Audit ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Audit_IsPagedAndFiltersByEventTypeActorAndRun()
    {
        var runId = Guid.NewGuid();
        var coordinatorId = TestAppFactory.DemoUserId(Roles.Coordinator);
        await using (var db = NewDb())
        {
            db.WorkflowRuns.Add(new WorkflowRun { Id = runId, Objective = "audit paging", CurrentState = WorkflowState.Approved });
            var dispatch = new Dispatch { Id = Guid.NewGuid(), WorkflowRunId = runId, Decision = DispatchDecision.Approved, ApprovedById = coordinatorId };
            db.Dispatches.Add(dispatch);
            db.AuditTrail.AddRange(
                new AuditTrail { DispatchId = dispatch.Id, EventType = "DispatchApproved", ActorId = coordinatorId, CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
                new AuditTrail { DispatchId = dispatch.Id, EventType = "DeliveryConfirmed", ActorId = TestAppFactory.DemoUserId(Roles.Volunteer), CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
                new AuditTrail { EventType = "PlanValidated", CreatedAt = DateTime.UtcNow.AddMinutes(-3) });
            await db.SaveChangesAsync();
        }

        var all = await JsonAsync(await _coordinator.GetAsync("/api/audit?pageSize=2"));
        Assert.Equal(3, all.GetProperty("totalItems").GetInt32());
        Assert.Equal(2, all.GetProperty("totalPages").GetInt32());
        Assert.Equal("DeliveryConfirmed", all.GetProperty("items")[0].GetProperty("eventType").GetString()); // newest first

        var approved = (await JsonAsync(await _coordinator.GetAsync("/api/audit?eventType=DispatchApproved"))).GetProperty("items");
        Assert.Equal(1, approved.GetArrayLength());
        Assert.Equal("Jane Smith", approved[0].GetProperty("actorName").GetString());
        Assert.Equal(runId, approved[0].GetProperty("workflowRunId").GetGuid());

        var byActor = await JsonAsync(await _coordinator.GetAsync($"/api/audit?actorId={coordinatorId}"));
        Assert.Equal(1, byActor.GetProperty("totalItems").GetInt32());
        var system = await JsonAsync(await _coordinator.GetAsync("/api/audit?actorId=0"));
        Assert.Equal("PlanValidated", system.GetProperty("items")[0].GetProperty("eventType").GetString());
        var byRun = await JsonAsync(await _coordinator.GetAsync($"/api/audit?workflowRunId={runId}"));
        Assert.Equal(2, byRun.GetProperty("totalItems").GetInt32());

        var types = await JsonAsync(await _coordinator.GetAsync("/api/audit/event-types"));
        Assert.Equal(["DeliveryConfirmed", "DispatchApproved", "PlanValidated"], types.EnumerateArray().Select(t => t.GetString()));
    }

    // ── 5.06 UpdatedAt ────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveChanges_StampsUpdatedAtOnModifiedRowsOnly()
    {
        var seedDate = new DateTime(2026, 9, 25, 0, 0, 0, DateTimeKind.Utc);
        await using (var db = NewDb())
        {
            var report = await db.Reports.SingleAsync(r => r.Id == 1);
            Assert.Equal(seedDate, report.UpdatedAt);
            report.Status = "Triaged";
            await db.SaveChangesAsync();
        }

        await using (var db = NewDb())
        {
            Assert.True((await db.Reports.SingleAsync(r => r.Id == 1)).UpdatedAt > DateTime.UtcNow.AddMinutes(-1));
            Assert.Equal(seedDate, (await db.Reports.SingleAsync(r => r.Id == 2)).UpdatedAt);
        }
    }

    // ── 5.09 Validation ───────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidInput_Returns400WithFieldErrors()
    {
        await AssertFieldErrorAsync(
            await _depotManager.PostAsJsonAsync("/api/depots", new { name = "Bad", latitude = 95, longitude = 79.8 }), "Latitude");
        await AssertFieldErrorAsync(
            await _depotManager.PutAsJsonAsync("/api/inventory/1/check-in", new { quantityReceived = 0 }), "QuantityReceived");
        await AssertFieldErrorAsync(
            await _coordinator.PostAsJsonAsync($"/api/dispatches/{Guid.NewGuid()}/reject", new { reason = new string('x', 2001) }), "Reason");
        await AssertFieldErrorAsync(
            await _coordinator.PostAsJsonAsync("/api/workflows", new { objective = "", reportIds = new[] { 1 } }), "Objective");
        await AssertFieldErrorAsync(
            await _coordinator.PostAsJsonAsync("/api/shelters",
                new { name = "S", latitude = 6.9, longitude = 79.8, capacity = -1, currentOccupancy = 0, status = "Active" }), "Capacity");

        using var form = new MultipartFormDataContent
        {
            { new StringContent("1"), "ShelterId" },
            { new StringContent("Water"), "NeedType" },
            { new StringContent("0"), "QuantityNeeded" },
            { new StringContent("123"), "GpsLat" },
            { new StringContent(new string('n', 501)), "Notes" }
        };
        var report = await _factory.CreateClientAs(Roles.Volunteer).PostAsync("/api/reports", form);
        await AssertFieldErrorAsync(report, "QuantityNeeded", "GpsLat", "Notes");
    }

    private static async Task AssertFieldErrorAsync(HttpResponseMessage response, params string[] fields)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");
        var keys = errors.EnumerateObject().Select(e => e.Name.TrimStart('$', '.')).ToList();
        foreach (var field in fields)
            Assert.Contains(keys, k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase));
    }
}
