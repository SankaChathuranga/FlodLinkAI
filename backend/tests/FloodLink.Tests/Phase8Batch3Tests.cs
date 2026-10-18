using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using FloodLink.Tests.AgentEvaluation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Phase 8 batch 3 API: stock movement history (8.03), physical confirmation of reserved stock (8.05),
/// stock-line lookup for QR labels (8.06) and duplicate-safe report re-sending (8.09).
/// Demo seed: item 1 = depot 1 Water 600 (floor 100), item 4 = depot 2 Medical 50; both depots managed by user 3.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Phase8Batch3Tests : IClassFixture<AgentSafetyApiTests.Factory>
{
    private readonly AgentSafetyApiTests.Factory _factory;
    private readonly HttpClient _coordinator;
    private readonly HttpClient _depot;

    public Phase8Batch3Tests(AgentSafetyApiTests.Factory factory)
    {
        _factory = factory;
        _factory.Llm = new FakeLlmClient();
        _factory.Mapbox = AgentSafetyApiTests.Factory.DefaultRoute;
        TestAppFactory.ResetDatabase(_factory.Services);
        _coordinator = ClientAs(Roles.Coordinator);
        _depot = ClientAs(Roles.DepotManager);
    }

    private HttpClient ClientAs(string role, int? userId = null) =>
        _factory.CreateClient().WithToken(TestAppFactory.TokenFor(role, userId));

    private AppDbContext NewDb() => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<JsonArray> MovementsAsync(string query = "")
        => (await _depot.GetFromJsonAsync<JsonObject>($"/api/inventory/movements?{query}"))!["items"]!.AsArray();

    private static string Kind(JsonNode? m) => m!["kind"]!.GetValue<string>();

    // A run that reaches PendingApproval with 40 Water reserved at depot 1 (rule-based agents, stub Mapbox).
    private async Task<Guid> PendingRunAsync(int quantity = 40)
    {
        int reportId;
        await using (var db = NewDb())
        {
            var report = new Report { ShelterId = 1, ReportedBy = TestAppFactory.DemoUserId(Roles.Volunteer), NeedType = "Water", QuantityNeeded = quantity, Status = "New" };
            db.Reports.Add(report);
            await db.SaveChangesAsync();
            reportId = report.Id;
        }
        var create = await _coordinator.PostAsJsonAsync("/api/workflows", new { objective = "batch 3", reportIds = new[] { reportId } });
        var runId = Guid.Parse((await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>());
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (DateTime.UtcNow < deadline)
        {
            await using var db = NewDb();
            var state = await db.WorkflowRuns.Where(r => r.Id == runId).Select(r => r.CurrentState).SingleAsync();
            if (state == WorkflowState.PendingApproval) return runId;
            Assert.NotEqual(WorkflowState.Failed, state);
            await Task.Delay(50);
        }
        throw new TimeoutException("Run did not reach PendingApproval.");
    }

    // ── 8.03 Stock history ────────────────────────────────────────────────────

    [Fact]
    public async Task ManualChanges_AreRecordedWithTheStockAfterwardsAndWhoDidThem()
    {
        await _depot.PutAsJsonAsync("/api/inventory/1/check-in", new { quantityReceived = 25 });
        await _depot.PutAsJsonAsync("/api/inventory/1/reserve", new { quantity = 10 });
        await _depot.PutAsJsonAsync("/api/inventory/1/release", new { quantity = 4 });

        var movements = await MovementsAsync("inventoryItemId=1");

        Assert.Equal(["ManualRelease", "ManualHold", "CheckIn"], movements.Select(Kind)); // newest first
        var checkIn = movements[2]!;
        Assert.Equal((25.0, 625.0, 0.0), (checkIn["quantity"]!.GetValue<double>(), checkIn["availableAfter"]!.GetValue<double>(), checkIn["reservedAfter"]!.GetValue<double>()));
        Assert.Equal("Kamal Perera", checkIn["userName"]!.GetValue<string>());
        Assert.Equal(("Water", "Colombo Central Depot"), (checkIn["itemName"]!.GetValue<string>(), checkIn["depotName"]!.GetValue<string>()));
        Assert.Equal(6.0, movements[0]!["reservedAfter"]!.GetValue<double>());
    }

    [Fact]
    public async Task NewStockLine_StartsWithAnOpeningMovement()
    {
        var created = await _depot.PostAsJsonAsync("/api/inventory", new { depotId = 1, itemName = "Tarpaulin", unit = "sheets", quantityAvailable = 80, reorderThreshold = 10 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();

        var opening = Assert.Single(await MovementsAsync($"inventoryItemId={id}"));
        Assert.Equal(("Opening", 80.0), (Kind(opening), opening!["quantity"]!.GetValue<double>()));
    }

    [Fact]
    public async Task PlanLifecycle_RecordsReserveCommitAndRelease_WithTheRunAndApprover()
    {
        var approved = await PendingRunAsync();
        var reserve = Assert.Single(await MovementsAsync($"workflowRunId={approved}"));
        Assert.Equal("PlanReserve", Kind(reserve));
        Assert.Null(reserve!["userId"]); // the reservation after validation is a system step

        Assert.Equal(HttpStatusCode.OK, (await _coordinator.PostAsJsonAsync($"/api/dispatches/{approved}/approve", new { })).StatusCode);
        var commit = (await MovementsAsync($"workflowRunId={approved}&kind=PlanCommit")).Single()!;
        Assert.Equal(TestAppFactory.DemoUserId(Roles.Coordinator), commit["userId"]!.GetValue<int>());
        Assert.Equal((560.0, 0.0), (commit["availableAfter"]!.GetValue<double>(), commit["reservedAfter"]!.GetValue<double>()));

        var rejected = await PendingRunAsync(quantity: 30);
        await _coordinator.PostAsJsonAsync($"/api/dispatches/{rejected}/reject", new { reason = "Road closed" });
        Assert.Equal(["PlanRelease", "PlanReserve"], (await MovementsAsync($"workflowRunId={rejected}")).Select(Kind));
    }

    [Fact]
    public async Task Movements_FilterByKind_AndRejectAnUnknownKind_AndAreForStockStaffOnly()
    {
        await _depot.PutAsJsonAsync("/api/inventory/4/check-in", new { quantityReceived = 5 });
        await _depot.PutAsJsonAsync("/api/inventory/4/reserve", new { quantity = 2 });

        Assert.Equal(["CheckIn"], (await MovementsAsync("kind=checkin&depotId=2")).Select(Kind));
        Assert.Equal(HttpStatusCode.BadRequest, (await _depot.GetAsync("/api/inventory/movements?kind=Teleport")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _coordinator.GetAsync("/api/inventory/movements")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientAs(Roles.Volunteer).GetAsync("/api/inventory/movements")).StatusCode);
    }

    // ── 8.05 Confirm reserved stock ───────────────────────────────────────────

    private async Task<int> ReservedLineAsync(Guid runId)
    {
        await using var db = NewDb();
        return await db.AllocationProposals.Where(a => a.WorkflowRunId == runId && a.Status == "Reserved").Select(a => a.Id).SingleAsync();
    }

    [Fact]
    public async Task DepotManager_ConfirmsTheShelfCount_AndTheCoordinatorSeesIt()
    {
        var runId = await PendingRunAsync();
        var line = await ReservedLineAsync(runId);

        var mine = await _depot.GetFromJsonAsync<JsonObject>("/api/allocations?status=Reserved&managedByMe=true");
        Assert.Contains(mine!["items"]!.AsArray(), a => a!["id"]!.GetValue<int>() == line);

        var confirm = await _depot.PostAsJsonAsync($"/api/allocations/{line}/confirm-stock", new { countedQuantity = 40, note = "On pallet 3." });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var body = (await confirm.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal((40.0, "Kamal Perera", "On pallet 3."),
            (body["countedQuantity"]!.GetValue<double>(), body["confirmedBy"]!.GetValue<string>(), body["confirmationNote"]!.GetValue<string>()));

        var queue = await _coordinator.GetFromJsonAsync<JsonObject>("/api/dispatches/approval-queue");
        var check = queue!["items"]!.AsArray().Single(i => i!["workflowRunId"]!.GetValue<string>() == runId.ToString())!["stockCheck"]!;
        Assert.Equal((1, 1, 0), (check["reservedLines"]!.GetValue<int>(), check["confirmedLines"]!.GetValue<int>(), check["shortfalls"]!.AsArray().Count));

        var count = (await MovementsAsync($"workflowRunId={runId}&kind=PhysicalCount")).Single()!;
        Assert.Equal(560.0 + 40, count["availableAfter"]!.GetValue<double>()); // no quantity changed
    }

    [Fact]
    public async Task ShortCount_IsReportedAsAShortfall()
    {
        var runId = await PendingRunAsync();
        var line = await ReservedLineAsync(runId);

        await _depot.PostAsJsonAsync($"/api/allocations/{line}/confirm-stock", new { countedQuantity = 25, note = "Some bottles damaged." });

        var queue = await _coordinator.GetFromJsonAsync<JsonObject>("/api/dispatches/approval-queue");
        var shortfall = queue!["items"]!.AsArray().Single(i => i!["workflowRunId"]!.GetValue<string>() == runId.ToString())!["stockCheck"]!["shortfalls"]!.AsArray().Single()!;
        Assert.Equal(("Colombo Central Depot", "Water", 40.0, 25.0),
            (shortfall["depotName"]!.GetValue<string>(), shortfall["itemName"]!.GetValue<string>(),
             shortfall["reserved"]!.GetValue<double>(), shortfall["counted"]!.GetValue<double>()));
        var count = (await MovementsAsync($"workflowRunId={runId}&kind=PhysicalCount")).Single()!;
        Assert.Contains("Counted 25 of 40 reserved.", count["note"]!.GetValue<string>());
    }

    [Fact]
    public async Task Confirmation_IsOnlyForTheDepotsManager_AndOnlyWhileReserved()
    {
        var runId = await PendingRunAsync();
        var line = await ReservedLineAsync(runId);

        var otherManager = ClientAs(Roles.DepotManager, userId: 4242);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherManager.PostAsJsonAsync($"/api/allocations/{line}/confirm-stock", new { countedQuantity = 40 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _coordinator.PostAsJsonAsync($"/api/allocations/{line}/confirm-stock", new { countedQuantity = 40 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _depot.PostAsJsonAsync($"/api/allocations/{line}/confirm-stock", new { countedQuantity = -1 })).StatusCode);

        await _coordinator.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { });
        var late = await _depot.PostAsJsonAsync($"/api/allocations/{line}/confirm-stock", new { countedQuantity = 40 });
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Equal("NOT_RESERVED", (await late.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NotFound, (await _depot.PostAsJsonAsync("/api/allocations/999999/confirm-stock", new { countedQuantity = 1 })).StatusCode);
    }

    // ── 8.06 Stock line lookup ────────────────────────────────────────────────

    [Fact]
    public async Task StockLine_IsReadById_ForAScannedLabel()
    {
        var item = await _depot.GetFromJsonAsync<JsonObject>("/api/inventory/4");
        Assert.Equal(("Medical", "Kaduwela Supply Depot"), (item!["itemName"]!.GetValue<string>(), item["depot"]!["name"]!.GetValue<string>()));
        Assert.Equal(HttpStatusCode.NotFound, (await _depot.GetAsync("/api/inventory/999999")).StatusCode);
    }

    // ── 8.09 Re-sent reports ──────────────────────────────────────────────────

    private static MultipartFormDataContent ReportForm(string? clientReference) => new()
    {
        { new StringContent("2"), "ShelterId" },
        { new StringContent("Water"), "NeedType" },
        { new StringContent("12"), "QuantityNeeded" },
        { new StringContent(clientReference ?? ""), "ClientReference" }
    };

    [Fact]
    public async Task ReSentReport_ReturnsTheSavedOne_InsteadOfADuplicate()
    {
        var volunteer = ClientAs(Roles.Volunteer);
        const string reference = "3f2c8a1e-offline-0001";

        var first = await volunteer.PostAsync("/api/reports", ReportForm(reference));
        var again = await volunteer.PostAsync("/api/reports", ReportForm(reference));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>(),
            (await again.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>());
        await using var db = NewDb();
        Assert.Equal(1, await db.Reports.CountAsync(r => r.ClientReference == reference));

        // The same reference from another reporter is a different report.
        var other = await ClientAs(Roles.Volunteer, userId: TestAppFactory.DemoUserId(Roles.Admin)).PostAsync("/api/reports", ReportForm(reference));
        Assert.Equal(HttpStatusCode.Created, other.StatusCode);
    }

    [Fact]
    public async Task ReportWithoutReference_StillWorks_AndABadReferenceIsRejected()
    {
        var volunteer = ClientAs(Roles.Volunteer);
        Assert.Equal(HttpStatusCode.Created, (await volunteer.PostAsync("/api/reports", ReportForm(null))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await volunteer.PostAsync("/api/reports", ReportForm(null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await volunteer.PostAsync("/api/reports", ReportForm("bad ref; DROP"))).StatusCode);
    }
}
