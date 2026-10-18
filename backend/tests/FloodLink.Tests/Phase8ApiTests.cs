using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FloodLink.Agents.Validation;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using FloodLink.Tests.AgentEvaluation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Phase 8 batch 1 API: route endpoints (8.13), trigger-matching (8.14), analytics per shelter and
/// turnaround (8.17), the manager's own low stock (8.04) and the depot-manager picker (8.01).
/// Uses the scriptable Mapbox host of <see cref="AgentSafetyApiTests"/>.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class Phase8ApiTests : IClassFixture<AgentSafetyApiTests.Factory>
{
    private readonly AgentSafetyApiTests.Factory _factory;
    private readonly HttpClient _coordinator;

    public Phase8ApiTests(AgentSafetyApiTests.Factory factory)
    {
        _factory = factory;
        _factory.Llm = new FakeLlmClient();
        _factory.Mapbox = AgentSafetyApiTests.Factory.DefaultRoute;
        TestAppFactory.ResetDatabase(_factory.Services);
        _coordinator = ClientAs(Roles.Coordinator);
    }

    private HttpClient ClientAs(string role) => _factory.CreateClient().WithToken(TestAppFactory.TokenFor(role));

    private AppDbContext NewDb() => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    // ── 8.13 Routes ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Calculate_ReturnsTheMapboxRoute_WithoutStoringIt()
    {
        var response = await _coordinator.PostAsJsonAsync("/api/routes/calculate", new { depotId = 2, shelterId = 2 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var route = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal((12.5, 25.0), (route["distanceKm"]!.GetValue<double>(), route["etaMinutes"]!.GetValue<double>()));
        Assert.Equal("Kaduwela Supply Depot", route["depotName"]!.GetValue<string>());
        Assert.Equal("Kaduwela Community Hall", route["shelterName"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(route["polyline"]!.GetValue<string>()));
        await using var db = NewDb();
        Assert.Empty(db.Routes);
    }

    [Theory]
    [InlineData("none", HttpStatusCode.BadGateway, "NO_ROUTE")]
    [InlineData("timeout", HttpStatusCode.BadGateway, "MAPBOX_TIMEOUT")]
    [InlineData("unknown-depot", HttpStatusCode.NotFound, "LOCATION_NOT_FOUND")]
    public async Task Calculate_Failures_AreExplained(string failure, HttpStatusCode status, string code)
    {
        _factory.Mapbox = failure switch
        {
            "none" => () => null,
            "timeout" => () => throw new TaskCanceledException("slow"),
            _ => AgentSafetyApiTests.Factory.DefaultRoute
        };

        var response = await _coordinator.PostAsJsonAsync("/api/routes/calculate",
            new { depotId = failure == "unknown-depot" ? 999 : 1, shelterId = 1 });

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task StoredLegs_AreReadByIdAndByRun()
    {
        var runId = Guid.NewGuid();
        var legId = Guid.NewGuid();
        await using (var db = NewDb())
        {
            db.WorkflowRuns.Add(new WorkflowRun { Id = runId, Objective = "routes", CurrentState = WorkflowState.PendingApproval });
            db.Routes.AddRange(
                new RouteEntity { Id = legId, WorkflowRunId = runId, DepotId = 1, ShelterId = 1, DistanceMeters = 2_000, EstimatedDurationSeconds = 360, PolylineString = "a" },
                new RouteEntity { WorkflowRunId = runId, DepotId = 2, ShelterId = 2, DistanceMeters = 6_500, EstimatedDurationSeconds = 900, PolylineString = "b" });
            await db.SaveChangesAsync();
        }

        var leg = await _coordinator.GetFromJsonAsync<JsonObject>($"/api/routes/{legId}");
        Assert.Equal((2.0, 6.0, "Colombo Central Depot"),
            (leg!["distanceKm"]!.GetValue<double>(), leg["etaMinutes"]!.GetValue<double>(), leg["depotName"]!.GetValue<string>()));

        var legs = await _coordinator.GetFromJsonAsync<JsonArray>($"/api/routes?workflowRunId={runId}");
        Assert.Equal([6.5, 2.0], legs!.Select(l => l!["distanceKm"]!.GetValue<double>()));
        Assert.Equal(HttpStatusCode.NotFound, (await _coordinator.GetAsync($"/api/routes/{Guid.NewGuid()}")).StatusCode);
    }

    // ── 8.14 Trigger matching ─────────────────────────────────────────────────

    private async Task<int> AllocationInRunAsync(WorkflowState state)
    {
        await using var db = NewDb();
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(), Objective = "re-match", CurrentState = state,
            FailedAtState = state == WorkflowState.Failed ? WorkflowState.Matching : null
        };
        var allocation = new AllocationProposalEntity { WorkflowRunId = run.Id, DepotId = 1, ShelterId = 1, ItemName = "Water", Quantity = 10, Status = "Released" };
        db.WorkflowRuns.Add(run);
        db.AllocationProposals.Add(allocation);
        await db.SaveChangesAsync();
        return allocation.Id;
    }

    [Fact]
    public async Task TriggerMatching_RetriesTheFailedRun()
    {
        var allocationId = await AllocationInRunAsync(WorkflowState.Failed);

        var response = await _coordinator.PostAsync($"/api/allocations/{allocationId}/trigger-matching", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(("Matching", 1), (body["state"]!.GetValue<string>(), body["retryCount"]!.GetValue<int>()));
    }

    [Fact]
    public async Task TriggerMatching_RefusesARunThatHasNotFailed_AndUnknownAllocations()
    {
        var pending = await AllocationInRunAsync(WorkflowState.PendingApproval);

        var refused = await _coordinator.PostAsync($"/api/allocations/{pending}/trigger-matching", null);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("NOT_FAILED", (await refused.Content.ReadFromJsonAsync<JsonObject>())!["error"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.NotFound, (await _coordinator.PostAsync("/api/allocations/999999/trigger-matching", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await ClientAs(Roles.DepotManager).PostAsync($"/api/allocations/{pending}/trigger-matching", null)).StatusCode);
    }

    // ── 8.17 Analytics ────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_MeasuresTurnaroundFromRunStart_AndCountsDecisionsPerShelter()
    {
        var now = DateTime.UtcNow;
        await using (var db = NewDb())
        {
            // Approved after 30 and 90 minutes; one rejection; a revision then approval at Kaduwela.
            AddRun(db, now, startedMinutesAgo: 30, shelterId: 1, (DispatchDecision.Approved, null));
            AddRun(db, now, startedMinutesAgo: 90, shelterId: 1, (DispatchDecision.Rejected, "Road closed"), (DispatchDecision.Approved, null));
            AddRun(db, now, startedMinutesAgo: 60, shelterId: 2, (DispatchDecision.RevisionRequested, "Use depot 2"));
            await db.SaveChangesAsync();
        }

        var summary = (await _coordinator.GetFromJsonAsync<JsonObject>("/api/dispatches/summary"))!;

        Assert.Equal(4, summary["total"]!.GetValue<int>());
        Assert.InRange(summary["avgApprovalMinutes"]!.GetValue<double>(), 59, 61);    // (30 + 90) / 2
        Assert.InRange(summary["medianApprovalMinutes"]!.GetValue<double>(), 59, 61);
        var perShelter = summary["dispatchesPerShelter"]!.AsArray();
        var colombo = perShelter.Single(s => s!["shelterId"]!.GetValue<int>() == 1)!;
        Assert.Equal(("Central Colombo Relief Center", 2, 1, 0),
            (colombo["shelterName"]!.GetValue<string>(), colombo["approved"]!.GetValue<int>(),
             colombo["rejected"]!.GetValue<int>(), colombo["revisionsRequested"]!.GetValue<int>()));
        var kaduwela = perShelter.Single(s => s!["shelterId"]!.GetValue<int>() == 2)!;
        Assert.Equal(1, kaduwela["revisionsRequested"]!.GetValue<int>());
        Assert.DoesNotContain(perShelter, s => s!["shelterId"]!.GetValue<int>() == 3); // no decisions there
    }

    private static void AddRun(AppDbContext db, DateTime now, int startedMinutesAgo, int shelterId,
        params (DispatchDecision Decision, string? Notes)[] decisions)
    {
        var run = new WorkflowRun { Id = Guid.NewGuid(), Objective = "summary", CurrentState = WorkflowState.Approved, CreatedAt = now.AddMinutes(-startedMinutesAgo) };
        db.WorkflowRuns.Add(run);
        db.AllocationProposals.Add(new AllocationProposalEntity { WorkflowRunId = run.Id, DepotId = 1, ShelterId = shelterId, ItemName = "Water", Quantity = 5, Status = "Committed" });
        foreach (var (decision, notes) in decisions)
            db.Dispatches.Add(new Dispatch { Id = Guid.NewGuid(), WorkflowRunId = run.Id, Decision = decision, ApprovalNotes = notes, CreatedAt = now });
    }

    // ── 8.04 / 8.01 Depot manager ─────────────────────────────────────────────

    [Fact]
    public async Task ManagedByMe_KeepsOnlyTheSignedInManagersDepots()
    {
        await using (var db = NewDb())
        {
            (await db.Depots.SingleAsync(d => d.Id == 2)).ManagerId = null;
            await db.SaveChangesAsync();
        }

        var page = await ClientAs(Roles.DepotManager).GetFromJsonAsync<JsonObject>("/api/inventory?managedByMe=true");

        Assert.All(page!["items"]!.AsArray(), i => Assert.Equal(1, i!["depotId"]!.GetValue<int>()));
        Assert.Equal(2, page["totalItems"]!.GetValue<int>());
        var coordinatorOwn = await _coordinator.GetFromJsonAsync<JsonObject>("/api/inventory?managedByMe=true");
        Assert.Equal(0, coordinatorOwn!["totalItems"]!.GetValue<int>()); // manages no depot
    }

    [Fact]
    public async Task ManagerPicker_ListsDepotManagersOnly_ForDepotStaff()
    {
        var managers = await ClientAs(Roles.DepotManager).GetFromJsonAsync<JsonArray>("/api/depots/managers");

        var only = Assert.Single(managers!);
        Assert.Equal(("Kamal Perera", "depot@floodlink.lk"), (only!["name"]!.GetValue<string>(), only["email"]!.GetValue<string>()));
        Assert.Null(only["hashedPassword"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await _coordinator.GetAsync("/api/depots/managers")).StatusCode);
    }

    // ── 8.15 Validation explanation ───────────────────────────────────────────

    private async Task<Guid> ValidatedRunAsync(bool passed)
    {
        var runId = Guid.NewGuid();
        var results = new ValidationResults
        {
            WorkflowRunId = runId,
            OverallPassed = passed,
            Checks =
            [
                new() { CheckName = SafetyRules.StockAvailability, Passed = passed, ViolationDetail = passed ? null : "Requested 60 Medical exceeds free stock 40 at depot 2." },
                new() { CheckName = SafetyRules.VehicleCapacity, Passed = true }
            ]
        };
        await using var db = NewDb();
        db.WorkflowRuns.Add(new WorkflowRun
        {
            Id = runId, Objective = "explain", CurrentState = passed ? WorkflowState.PendingApproval : WorkflowState.Failed,
            PlanJson = WorkflowPlan.Merge(null, WorkflowPlan.ValidationResultsKey, results)
        });
        await db.SaveChangesAsync();
        return runId;
    }

    [Fact]
    public async Task Explain_UsesTheModelsWords_ButTheRulesVerdict_AndCachesIt()
    {
        _factory.Llm = new FakeLlmClient().Returns(ValidationExplainer.PromptVersion, $$"""
            {"summary":"The plan is blocked: depot 2 has only 40 medical kits free.",
             "points":[{"checkName":"{{SafetyRules.StockAvailability}}","explanation":"60 kits were planned but 40 are free; restock or use another depot."},
                       {"checkName":"{{SafetyRules.VehicleCapacity}}","explanation":"The load fits the truck."}]}
            """);
        var runId = await ValidatedRunAsync(passed: false);

        var first = (await (await _coordinator.PostAsync($"/api/validations/{runId}/explain", null)).Content.ReadFromJsonAsync<JsonObject>())!;
        var second = (await (await _coordinator.PostAsync($"/api/validations/{runId}/explain", null)).Content.ReadFromJsonAsync<JsonObject>())!;

        Assert.Equal("llm", first["source"]!.GetValue<string>());
        Assert.False(first["overallPassed"]!.GetValue<bool>());
        var stock = first["points"]!.AsArray().Single(p => p!["checkName"]!.GetValue<string>() == SafetyRules.StockAvailability)!;
        Assert.False(stock["passed"]!.GetValue<bool>()); // from the rules, not the model
        Assert.Equal(first.ToJsonString(), second.ToJsonString());
        Assert.Single(_factory.Llm.Requests); // the second call came from the plan
        await using var db = NewDb();
        Assert.Contains(db.AgentExecutionLogs.Where(l => l.WorkflowRunId == runId), l => l.AgentName == "ValidationExplainer");
    }

    [Fact]
    public async Task Explain_WithoutValidation_Returns409()
    {
        await using (var db = NewDb())
        {
            db.WorkflowRuns.Add(new WorkflowRun { Id = Guid.NewGuid(), Objective = "not yet", CurrentState = WorkflowState.Matching });
            await db.SaveChangesAsync();
        }
        var runId = NewDb().WorkflowRuns.Single(r => r.Objective == "not yet").Id;

        var response = await _coordinator.PostAsync($"/api/validations/{runId}/explain", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientAs(Roles.DepotManager).PostAsync($"/api/validations/{runId}/explain", null)).StatusCode);
    }

    // ── 8.16 Delivery photo ───────────────────────────────────────────────────

    private async Task<Guid> ApprovedRunForVolunteerAsync()
    {
        await using var db = NewDb();
        var run = new WorkflowRun { Id = Guid.NewGuid(), Objective = "delivered", CurrentState = WorkflowState.Approved };
        db.WorkflowRuns.Add(run);
        db.Reports.Add(new Report { ShelterId = 1, ReportedBy = TestAppFactory.DemoUserId(Roles.Volunteer), NeedType = "Water", QuantityNeeded = 5, Status = "InPlan", WorkflowRunId = run.Id });
        db.Dispatches.Add(new Dispatch { Id = Guid.NewGuid(), WorkflowRunId = run.Id, Decision = DispatchDecision.Approved, DispatchedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        return run.Id;
    }

    private static MultipartFormDataContent PhotoForm(string fileName = "proof.jpg", string type = "image/jpeg")
    {
        var file = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3]);
        file.Headers.ContentType = new MediaTypeHeaderValue(type);
        return new MultipartFormDataContent { { file, "photo", fileName } };
    }

    [Fact]
    public async Task DeliveryPhoto_IsStored_ServedAndRecordedWithTheConfirmation()
    {
        var runId = await ApprovedRunForVolunteerAsync();
        var volunteer = ClientAs(Roles.Volunteer);

        var upload = await volunteer.PostAsync($"/api/dispatches/{runId}/delivery-photo", PhotoForm());
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var url = (await upload.Content.ReadFromJsonAsync<JsonObject>())!["photoUrl"]!.GetValue<string>();
        Assert.Matches("^/uploads/deliveries/[0-9a-f]{32}\\.jpg$", url);

        try
        {
            var served = await _factory.CreateClient().GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, served.StatusCode);

            var confirm = await volunteer.PostAsJsonAsync($"/api/dispatches/{runId}/confirm-delivery", new { notes = "Received.", photoReference = url });
            Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
            await using var db = NewDb();
            var audit = await db.AuditTrail.SingleAsync(a => a.EventType == "DeliveryConfirmed" && a.Dispatch!.WorkflowRunId == runId);
            Assert.Contains(url, audit.EventDetailJson);
        }
        finally
        {
            var root = _factory.Services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
            File.Delete(Path.Combine(root, "wwwroot", url.TrimStart('/')));
        }
    }

    [Fact]
    public async Task DeliveryPhoto_IsRefusedBeforeApproval_ForOthersRuns_AndForNonImages()
    {
        var runId = await ApprovedRunForVolunteerAsync();
        await using (var db = NewDb())
        {
            db.WorkflowRuns.Add(new WorkflowRun { Id = Guid.NewGuid(), Objective = "pending", CurrentState = WorkflowState.PendingApproval });
            await db.SaveChangesAsync();
        }
        var pendingId = NewDb().WorkflowRuns.Single(r => r.Objective == "pending").Id;

        Assert.Equal(HttpStatusCode.Conflict, (await _coordinator.PostAsync($"/api/dispatches/{pendingId}/delivery-photo", PhotoForm())).StatusCode);
        var stranger = _factory.CreateClient().WithToken(TestAppFactory.TokenFor(Roles.Volunteer, userId: 9999));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"/api/dispatches/{runId}/delivery-photo", PhotoForm())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await ClientAs(Roles.Volunteer).PostAsync($"/api/dispatches/{runId}/delivery-photo", PhotoForm("x.html", "text/html"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientAs(Roles.DepotManager).PostAsync($"/api/dispatches/{runId}/delivery-photo", PhotoForm())).StatusCode);
    }

    // ── 8.11 Route legs on the status screen ──────────────────────────────────

    [Fact]
    public async Task Status_CarriesTheRouteLegs_WithBothEndPoints()
    {
        var runId = await ApprovedRunForVolunteerAsync();
        await using (var db = NewDb())
        {
            var run = await db.WorkflowRuns.SingleAsync(r => r.Id == runId);
            run.PlanJson = WorkflowPlan.Merge(null, WorkflowPlan.RouteKey, new Route
            {
                WorkflowRunId = runId, AllocationProposalId = 1, DistanceKm = 6.5, EtaMinutes = 15, Polyline = "p",
                Legs = [new RouteLeg { DepotId = 2, ShelterId = 2, DistanceKm = 6.5, EtaMinutes = 15, Polyline = "encoded" }]
            });
            await db.SaveChangesAsync();
        }

        var status = await ClientAs(Roles.Volunteer).GetFromJsonAsync<JsonObject>($"/api/workflows/{runId}/status");

        var leg = Assert.Single(status!["routeLegs"]!.AsArray())!;
        Assert.Equal(("Kaduwela Supply Depot", 6.9061, "Kaduwela Community Hall", 6.9344, "encoded"),
            (leg["depotName"]!.GetValue<string>(), leg["depotLatitude"]!.GetValue<double>(),
             leg["shelterName"]!.GetValue<string>(), leg["shelterLatitude"]!.GetValue<double>(), leg["polyline"]!.GetValue<string>()));
    }
}
