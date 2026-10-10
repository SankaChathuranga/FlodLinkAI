using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FloodLink.Domain;
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

    private async Task<WorkflowState> AdvanceAsync(Guid runId)
    {
        using var scope = _factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<WorkflowOrchestrator>();
        var run = await orchestrator.AdvanceAsync(runId);
        return run.CurrentState;
    }

    [Fact]
    public async Task E2E001_ReportToApprovedDispatch_PersistsEveryStage()
    {
        var (shelterId, userId, itemId) = await SeedAsync(stock: 500);

        // Step 1 — field report is accepted and stored with an auto-calculated urgency.
        var reportResponse = await SubmitReportAsync(shelterId, userId, quantity: 40);
        Assert.Equal(HttpStatusCode.Created, reportResponse.StatusCode);
        await using (var db = NewDb())
        {
            var report = await db.Reports.SingleAsync();
            Assert.InRange(report.UrgencyLevel, 0, 100);
        }

        // Step 2 — creating the workflow runs the Triage Agent immediately.
        var create = await _client.PostAsJsonAsync("/api/workflows", new { objective = "E2E water relief" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var runId = Guid.Parse((await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>());
        await using (var db = NewDb())
        {
            Assert.Equal(WorkflowState.Matching, (await db.WorkflowRuns.SingleAsync(r => r.Id == runId)).CurrentState);
            Assert.Equal("Triaged", (await db.Reports.SingleAsync()).Status);
        }

        // Step 3 — Matching Agent proposes stock from the depot.
        Assert.Equal(WorkflowState.Routing, await AdvanceAsync(runId));
        await using (var db = NewDb())
        {
            var proposal = await db.AllocationProposals.SingleAsync(p => p.WorkflowRunId == runId);
            Assert.Equal("Water", proposal.ItemName);
            Assert.Equal(40, proposal.Quantity);
        }

        // Step 4 — Route/ETA Agent persists the (stubbed) Mapbox route.
        Assert.Equal(WorkflowState.Validating, await AdvanceAsync(runId));
        await using (var db = NewDb())
        {
            var route = await db.Routes.SingleAsync(r => r.WorkflowRunId == runId);
            Assert.Equal(12_500, route.DistanceMeters);
            Assert.Equal(1_500, route.EstimatedDurationSeconds);
        }

        // Step 5 — Validation/Safety Agent passes; the plan waits for a human.
        Assert.Equal(WorkflowState.PendingApproval, await AdvanceAsync(runId));

        // Step 6 — the approval queue lists the run.
        var pending = await _client.GetStringAsync("/api/dispatches/approval-queue");
        Assert.Contains(runId.ToString(), pending);

        // Step 7 — coordinator approves; a dispatch and an audit entry are written.
        var approve = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "E2E approve" });
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        var dispatchId = Guid.Parse((await approve.Content.ReadFromJsonAsync<JsonObject>())!["dispatchId"]!.GetValue<string>());

        var audit = await _client.GetStringAsync($"/api/audit/{dispatchId}");
        Assert.Contains("DispatchApproved", audit);

        await using (var db = NewDb())
        {
            Assert.Equal(WorkflowState.Approved, (await db.WorkflowRuns.SingleAsync(r => r.Id == runId)).CurrentState);
            Assert.Single(db.Dispatches.Where(d => d.WorkflowRunId == runId));
            Assert.True(await db.AgentExecutionLogs.CountAsync(l => l.WorkflowRunId == runId) >= 4);
        }

        // Step 8 — a second approval is refused and never creates a duplicate dispatch.
        var again = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "dup" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        await using (var db = NewDb())
        {
            Assert.Single(db.Dispatches.Where(d => d.WorkflowRunId == runId));
        }
    }

    [Fact]
    public async Task E2E001b_Approval_CommitsStockFromDepot()
    {
        var (shelterId, userId, itemId) = await SeedAsync(stock: 500);
        await SubmitReportAsync(shelterId, userId, quantity: 40);
        var create = await _client.PostAsJsonAsync("/api/workflows", new { objective = "E2E stock commit" });
        var runId = Guid.Parse((await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>());
        await AdvanceAsync(runId);
        await AdvanceAsync(runId);
        Assert.Equal(WorkflowState.PendingApproval, await AdvanceAsync(runId));

        await using (var db = NewDb())
        {
            // Matching only proposes; nothing is taken out of stock before approval.
            Assert.Equal(500, (await db.InventoryItems.SingleAsync(i => i.Id == itemId)).QuantityAvailable);
        }

        await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "commit" });

        await using (var db = NewDb())
        {
            Assert.Equal(460, (await db.InventoryItems.SingleAsync(i => i.Id == itemId)).QuantityAvailable);
        }
    }

    [Fact]
    public async Task E2E002_NotEnoughStock_NeverReachesApproval()
    {
        var (shelterId, userId, _) = await SeedAsync(stock: 0);
        await SubmitReportAsync(shelterId, userId, quantity: 40);

        var create = await _client.PostAsJsonAsync("/api/workflows", new { objective = "E2E no stock" });
        var runId = Guid.Parse((await create.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<string>());

        Assert.Equal(WorkflowState.Failed, await AdvanceAsync(runId));

        var approve = await _client.PostAsJsonAsync($"/api/dispatches/{runId}/approve", new { notes = "should fail" });
        Assert.Equal(HttpStatusCode.Conflict, approve.StatusCode);
        await using var db = NewDb();
        Assert.Empty(db.Dispatches.Where(d => d.WorkflowRunId == runId));
        Assert.Contains(db.AgentExecutionLogs.Where(l => l.WorkflowRunId == runId), l => l.Status != "Success");
    }
}
