using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Mapbox;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// E2E-001 / E2E-002: one field report travels through the real API host, the real
/// Triage, Matching, Route/ETA and Validation agents and a real PostgreSQL database,
/// up to coordinator approval, dispatch and audit. Only Mapbox is replaced by a
/// deterministic stub so the run is repeatable.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class EndToEndWorkflowTests : IClassFixture<EndToEndWorkflowTests.Factory>
{
    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", TestAppFactory.TestConnectionString);
            builder.UseSetting("Jwt:SigningKey", "test-only-signing-key-at-least-32-bytes-long!!");
            builder.UseSetting("Mapbox:ApiKey", "stub");
            builder.ConfigureTestServices(services => services.AddScoped<IMapboxClient, StubMapbox>());
        }

        public void ResetDatabase()
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
        }
    }

    /// <summary>Returns a fixed 12.5 km / 25 minute route for any coordinates.</summary>
    private sealed class StubMapbox : IMapboxClient
    {
        public Task<MapboxRouteResult?> GetRouteAsync(double originLng, double originLat,
            double destLng, double destLat, CancellationToken cancellationToken = default)
            => Task.FromResult<MapboxRouteResult?>(new MapboxRouteResult
            {
                DistanceMeters = 12_500,
                DurationSeconds = 1_500,
                EncodedPolyline = "_p~iF~ps|U_ulLnnqC"
            });
    }

    private readonly Factory _factory;
    private readonly HttpClient _client;

    public EndToEndWorkflowTests(Factory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
        _client = _factory.CreateClient();
    }

    private AppDbContext NewDb() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<(int shelterId, int userId, int itemId)> SeedAsync(double stock)
    {
        await using var db = NewDb();
        // Remove the demo seed (HasData) reports and stock so this test controls every input.
        await db.Reports.ExecuteDeleteAsync();
        await db.InventoryItems.ExecuteDeleteAsync();

        var user = new User { Name = "E2E Volunteer", Role = "Volunteer", Phone = "+94770000000", HashedPassword = "x" };
        var shelter = new Shelter
        {
            Name = "E2E Relief Camp", Capacity = 200, CurrentOccupancy = 190, Status = "Active",
            Latitude = 6.9271, Longitude = 79.8612
        };
        var depot = new Depot { Name = "E2E Depot", Latitude = 6.9000, Longitude = 79.9000 };
        db.AddRange(user, shelter, depot);
        await db.SaveChangesAsync();

        var item = new InventoryItem { DepotId = depot.Id, ItemName = "Water", Unit = "bottles", QuantityAvailable = stock };
        db.InventoryItems.Add(item);
        await db.SaveChangesAsync();
        return (shelter.Id, user.Id, item.Id);
    }

    private async Task<HttpResponseMessage> SubmitReportAsync(int shelterId, int userId, int quantity)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(shelterId.ToString()), "ShelterId" },
            { new StringContent(userId.ToString()), "ReportedBy" },
            { new StringContent("Water"), "NeedType" },
            { new StringContent(quantity.ToString()), "QuantityNeeded" }
        };
        return await _client.PostAsync("/api/reports", form);
    }

    private async Task<int> SubmitReportIdAsync(int shelterId, int userId, int quantity)
    {
        var response = await SubmitReportAsync(shelterId, userId, quantity);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();
    }

    private async Task<Guid> StartWorkflowAsync(string objective, params int[] reportIds)
    {
        var create = await _client.PostAsJsonAsync("/api/workflows", new { objective, reportIds });
        Assert.Equal(HttpStatusCode.Accepted, create.StatusCode);
        return Guid.Parse((await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>());
    }

    // The background runner advances the run; poll the status endpoint until it stops moving.
    private async Task<JsonObject> WaitForStatusAsync(Guid runId, params string[] states)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);
        JsonObject? status = null;
        while (DateTime.UtcNow < deadline)
        {
            status = await _client.GetFromJsonAsync<JsonObject>($"/api/workflows/{runId}/status");
            if (states.Contains(status!["state"]!.GetValue<string>()))
                return status;
            await Task.Delay(50);
        }

        throw new TimeoutException($"Run {runId} did not reach {string.Join("/", states)}; last status: {status}");
    }

    [Fact]
    public async Task E2E001_ReportToApprovedDispatch_PersistsEveryStage()
    {
        var (shelterId, userId, itemId) = await SeedAsync(stock: 500);

        // Step 1 — field report is accepted and stored with an auto-calculated urgency.
        var reportId = await SubmitReportIdAsync(shelterId, userId, quantity: 40);
        await using (var db = NewDb())
        {
            var report = await db.Reports.SingleAsync();
            Assert.InRange(report.UrgencyLevel, 0, 100);
        }

        // Step 2 — starting the workflow queues it; the runner takes it to PendingApproval.
        var runId = await StartWorkflowAsync("E2E water relief", reportId);
        var status = await WaitForStatusAsync(runId, "PendingApproval", "Failed");
        Assert.Equal("PendingApproval", status["state"]!.GetValue<string>());
        Assert.Equal(12.5, status["distanceKm"]!.GetValue<double>());
        Assert.Equal(25, status["etaMinutes"]!.GetValue<double>());

        await using (var db = NewDb())
        {
            Assert.Equal("Triaged", (await db.Reports.SingleAsync()).Status);

            // Matching proposed the stock and validation reserved it — nothing has left the depot.
            var proposal = await db.AllocationProposals.SingleAsync(p => p.WorkflowRunId == runId);
            Assert.Equal("Water", proposal.ItemName);
            Assert.Equal(40, proposal.Quantity);
            Assert.Equal("Reserved", proposal.Status);
            var item = await db.InventoryItems.SingleAsync(i => i.Id == itemId);
            Assert.Equal(500, item.QuantityAvailable);
            Assert.Equal(40, item.QuantityReserved);

            // Route/ETA Agent persisted one leg for the depot → shelter pair.
            var route = await db.Routes.SingleAsync(r => r.WorkflowRunId == runId);
            Assert.Equal(12_500, route.DistanceMeters);
            Assert.Equal(shelterId, route.ShelterId);

            // Validation checks are stored for the approval screen.
            Assert.NotEmpty(db.ValidationResults.Where(v => v.WorkflowRunId == runId));
        }

        // Step 3 — the execution summary lists every step with its tool calls.
        var logs = await _client.GetFromJsonAsync<JsonArray>($"/api/workflows/{runId}/logs");
        Assert.Equal(
            ["TriageAgent", "MatchingAgent", "RoutingAgent", "ValidationAgent", "StockReservation"],
            logs!.Select(l => l!["agentName"]!.GetValue<string>()).ToArray());
        Assert.Contains("mapbox.directions", logs.ToJsonString());
        Assert.Contains("db.inventory.read", logs.ToJsonString());

        // Step 4 — the approval queue lists the run.
        var pending = await _client.GetStringAsync("/api/dispatches/approval-queue");
        Assert.Contains(runId.ToString(), pending);

        // Step 5 — coordinator approves; a dispatch and an audit entry with the approver are written.
        var approve = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "E2E approve" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var dispatchId = Guid.Parse((await approve.Content.ReadFromJsonAsync<JsonObject>())!["dispatchId"]!.GetValue<string>());

        var audit = await _client.GetStringAsync($"/api/audit/{dispatchId}");
        Assert.Contains("DispatchApproved", audit);

        await using (var db = NewDb())
        {
            Assert.Equal(WorkflowState.Approved, (await db.WorkflowRuns.SingleAsync(r => r.Id == runId)).CurrentState);
            var dispatch = Assert.Single(db.Dispatches.Where(d => d.WorkflowRunId == runId));
            Assert.Equal(2, dispatch.ApprovedById); // the Development coordinator (seeded user 2)
            Assert.Equal(2, (await db.AuditTrail.SingleAsync(a => a.DispatchId == dispatchId)).ActorId);
            Assert.Equal("InPlan", (await db.Reports.SingleAsync()).Status);
        }

        // Step 6 — a second approval is refused and never creates a duplicate dispatch.
        var again = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "dup" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        await using (var db = NewDb())
        {
            Assert.Single(db.Dispatches.Where(d => d.WorkflowRunId == runId));
        }

        // Step 7 — delivery confirmed: the report is resolved and the status shows it.
        var delivered = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/confirm-delivery", new { notes = "Arrived" });
        Assert.Equal(HttpStatusCode.OK, delivered.StatusCode);
        var final = await _client.GetFromJsonAsync<JsonObject>($"/api/workflows/{runId}/status");
        Assert.True(final!["isDelivered"]!.GetValue<bool>());
        await using (var db = NewDb())
        {
            Assert.Equal("Resolved", (await db.Reports.SingleAsync()).Status);
        }
    }

    [Fact]
    public async Task E2E001b_Approval_CommitsReservedStockFromDepot()
    {
        var (shelterId, userId, itemId) = await SeedAsync(stock: 500);
        var reportId = await SubmitReportIdAsync(shelterId, userId, quantity: 40);
        var runId = await StartWorkflowAsync("E2E stock commit", reportId);
        await WaitForStatusAsync(runId, "PendingApproval");

        await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "commit" });

        await using var db = NewDb();
        var item = await db.InventoryItems.SingleAsync(i => i.Id == itemId);
        Assert.Equal(460, item.QuantityAvailable);
        Assert.Equal(0, item.QuantityReserved);
        Assert.Equal("Committed", (await db.AllocationProposals.SingleAsync(p => p.WorkflowRunId == runId)).Status);
    }

    [Fact]
    public async Task E2E002_NotEnoughStock_FailsSafely_ThenRetrySucceedsAfterRestock()
    {
        var (shelterId, userId, itemId) = await SeedAsync(stock: 0);
        var reportId = await SubmitReportIdAsync(shelterId, userId, quantity: 40);
        var runId = await StartWorkflowAsync("E2E no stock", reportId);

        var status = await WaitForStatusAsync(runId, "Failed", "PendingApproval");
        Assert.Equal("Failed", status["state"]!.GetValue<string>());
        Assert.Equal("Matching", status["failedAtState"]!.GetValue<string>());
        Assert.StartsWith("NO_STOCK_AVAILABLE", status["failureReason"]!.GetValue<string>());

        var approve = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "should fail" });
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        await using (var db = NewDb())
        {
            Assert.Empty(db.Dispatches.Where(d => d.WorkflowRunId == runId));
            Assert.Contains(db.AgentExecutionLogs.Where(l => l.WorkflowRunId == runId), l => l.Status != "Success");
        }

        // Depot staff restock, the coordinator retries the failed stage, and the plan completes.
        var checkIn = await _client.PutAsJsonAsync($"/api/inventory/{itemId}/check-in", new { quantityReceived = 100 });
        Assert.Equal(HttpStatusCode.OK, checkIn.StatusCode);
        var retry = await _client.PostAsync($"/api/workflows/{runId}/retry", null);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);

        await WaitForStatusAsync(runId, "PendingApproval");
        await using (var db = NewDb())
        {
            var run = await db.WorkflowRuns.SingleAsync(r => r.Id == runId);
            Assert.Equal(1, run.RetryCount);
            Assert.Null(run.FailureReason);
            Assert.Contains(db.AgentExecutionLogs.Where(l => l.WorkflowRunId == runId),
                l => l.AgentName == "MatchingAgent" && l.IsRetry && l.Status == "Success");
        }
    }

    [Fact]
    public async Task E2E003_Reject_ReleasesReservedStock()
    {
        var (shelterId, userId, itemId) = await SeedAsync(stock: 500);
        var reportId = await SubmitReportIdAsync(shelterId, userId, quantity: 40);
        var runId = await StartWorkflowAsync("E2E reject", reportId);
        await WaitForStatusAsync(runId, "PendingApproval");

        var reject = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/reject", new { reason = "Road closed" });
        Assert.Equal(HttpStatusCode.OK, reject.StatusCode);

        await using var db = NewDb();
        var item = await db.InventoryItems.SingleAsync(i => i.Id == itemId);
        Assert.Equal(500, item.QuantityAvailable);
        Assert.Equal(0, item.QuantityReserved);
        Assert.Equal("Released", (await db.AllocationProposals.SingleAsync(p => p.WorkflowRunId == runId)).Status);
        Assert.Equal(WorkflowState.Rejected, (await db.WorkflowRuns.SingleAsync(r => r.Id == runId)).CurrentState);
    }

    [Fact]
    public async Task E2E004_Revision_RematchesAndReturnsToPendingApproval()
    {
        var (shelterId, userId, itemId) = await SeedAsync(stock: 500);
        var reportId = await SubmitReportIdAsync(shelterId, userId, quantity: 40);
        var runId = await StartWorkflowAsync("E2E revision", reportId);
        await WaitForStatusAsync(runId, "PendingApproval");

        var revise = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/request-revision", new { notes = "Use the morning truck" });
        Assert.Equal(HttpStatusCode.OK, revise.StatusCode);

        // The runner re-matches; the run comes back for a new decision.
        await WaitForStatusAsync(runId, "PendingApproval");

        await using (var db = NewDb())
        {
            var proposals = await db.AllocationProposals.Where(p => p.WorkflowRunId == runId).OrderBy(p => p.Id).ToListAsync();
            Assert.Equal(["Released", "Reserved"], proposals.Select(p => p.Status).ToArray());
            Assert.Equal(40, (await db.InventoryItems.SingleAsync(i => i.Id == itemId)).QuantityReserved);
            var run = await db.WorkflowRuns.SingleAsync(r => r.Id == runId);
            Assert.Contains("Use the morning truck", run.PlanJson);
        }

        // A second decision is possible after a revision.
        var approve = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "ok now" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        await using (var db = NewDb())
        {
            Assert.Equal(2, db.Dispatches.Count(d => d.WorkflowRunId == runId));
        }
    }

    [Fact]
    public async Task E2E005_ReportInActiveRun_CannotStartAnotherRun()
    {
        var (shelterId, userId, _) = await SeedAsync(stock: 500);
        var reportId = await SubmitReportIdAsync(shelterId, userId, quantity: 40);
        var runId = await StartWorkflowAsync("E2E first", reportId);
        await WaitForStatusAsync(runId, "PendingApproval");

        var second = await _client.PostAsJsonAsync("/api/workflows", new { objective = "E2E second", reportIds = new[] { reportId } });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var unknown = await _client.PostAsJsonAsync("/api/workflows", new { objective = "E2E unknown", reportIds = new[] { 999_999 } });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    [Fact]
    public async Task E2E006_WorkflowList_FiltersAndPages()
    {
        var (shelterId, userId, _) = await SeedAsync(stock: 500);
        var first = await StartWorkflowAsync("E2E list alpha", await SubmitReportIdAsync(shelterId, userId, 10));
        var second = await StartWorkflowAsync("E2E list beta", await SubmitReportIdAsync(shelterId, userId, 10));
        await WaitForStatusAsync(first, "PendingApproval");
        await WaitForStatusAsync(second, "PendingApproval");

        var page = await _client.GetFromJsonAsync<JsonObject>("/api/workflows?search=beta&state=PendingApproval&pageSize=1");
        Assert.Equal(1, page!["totalItems"]!.GetValue<int>());
        Assert.Equal(second.ToString(), page["items"]![0]!["id"]!.GetValue<string>());
    }
}
