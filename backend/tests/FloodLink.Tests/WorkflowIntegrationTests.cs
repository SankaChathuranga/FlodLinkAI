using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// End-to-end tests for Member D's surface (Ijini): the Validation/Safety Agent
/// pipeline plus the guarded coordinator approval/dispatch/audit flow.
/// Runs against a real Postgres test database (floodlink_test) through the in-memory
/// TestServer, so it exercises routing, auth (Development auto-auth), controllers,
/// EF, Npgsql and the audit trail exactly as the demo does.
/// </summary>
public sealed class WorkflowIntegrationTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _client;

    public WorkflowIntegrationTests(TestAppFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
        _client = _factory.CreateClient();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private AppDbContext NewDb() =>
        _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private async Task<Guid> SeedRunAsync(WorkflowState state, string? planJson = null, string? objective = null)
    {
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            Objective = objective ?? "Shelter #12 - Water and Food needs, Region Colombo",
            CurrentState = state,
            PlanJson = planJson
        };
        await using (var db = NewDb())
        {
            db.WorkflowRuns.Add(run);
            await db.SaveChangesAsync();
        }
        return run.Id;
    }

    /// <summary>Seeds a Validating run whose PlanJson targets its own id.</summary>
    private async Task<Guid> SeedValidatingRunAsync(bool passing = true)
    {
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            Objective = "Shelter #12 - Water and Food needs, Region Colombo",
            CurrentState = WorkflowState.Validating
        };
        run.PlanJson = PlanJson(run.Id, passing).ToJsonString();
        await using (var db = NewDb())
        {
            db.WorkflowRuns.Add(run);
            await db.SaveChangesAsync();
        }
        return run.Id;
    }

    private static JsonObject PlanJson(Guid workflowRunId, bool passing = true)
    {
        var (qty, avail, reserved, reorder) = passing ? (50, 200, 50, 50) : (300, 100, 10, 20);
        return new JsonObject
        {
            ["WorkflowRunId"] = workflowRunId,
            ["AllocationProposalId"] = 1,
            ["VehicleCapacity"] = passing ? 100 : 50,
            ["DistanceKm"] = 50,
            ["EtaMinutes"] = 90,
            ["Polyline"] = "abc",
            ["OriginLat"] = 6.90,
            ["OriginLng"] = 79.80,
            ["DestLat"] = 6.02,
            ["DestLng"] = 80.00,
            ["Allocations"] = new JsonArray(new JsonObject
            {
                ["DepotId"] = 1,
                ["ShelterId"] = 1,
                ["ItemName"] = "Water",
                ["Quantity"] = qty,
                ["QuantityAvailable"] = avail,
                ["QuantityReserved"] = reserved,
                ["ReorderThreshold"] = reorder
            })
        };
    }

    private static async Task<JsonNode> ReadBodyAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(text) ?? throw new InvalidOperationException("Empty response body.");
    }

    // ── Validation pipeline ────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_ValidPlan_AdvancesToPendingApproval()
    {
        var id = await SeedValidatingRunAsync();

        var response = await _client.PostAsJsonAsync("/api/validations/run", new { workflowRunId = id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadBodyAsync(response);
        Assert.True(body["overallPassed"]!.GetValue<bool>());
        Assert.Equal("PendingApproval", body["state"]!.GetValue<string>());
        Assert.Equal(4, body["checks"]!.AsArray().Count);

        var results = await _client.GetAsync($"/api/validations/{id}");
        var resultBody = await ReadBodyAsync(results);
        Assert.True(resultBody["hasRunValidation"]!.GetValue<bool>());
        Assert.True(resultBody["overallPassed"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Validate_FailingPlan_TransitionsToFailed()
    {
        var id = await SeedValidatingRunAsync(passing: false);

        var response = await _client.PostAsJsonAsync("/api/validations/run", new { workflowRunId = id });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadBodyAsync(response);
        Assert.False(body["overallPassed"]!.GetValue<bool>());
        Assert.Equal("Failed", body["state"]!.GetValue<string>());

        var failedCheck = body["checks"]!.AsArray().First(c => c!["passed"]!.GetValue<bool>() == false);
        Assert.NotNull(failedCheck!["violationDetail"]!.GetValue<string>());

        await using var db = NewDb();
        var persisted = await db.WorkflowRuns.SingleAsync(r => r.Id == id);
        Assert.Equal(WorkflowState.Failed, persisted.CurrentState);
    }

    [Fact]
    public async Task Validate_WrongState_Returns409()
    {
        var id = await SeedRunAsync(WorkflowState.Matching, PlanJson(Guid.NewGuid()).ToJsonString());

        var response = await _client.PostAsJsonAsync("/api/validations/run", new { workflowRunId = id });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATE", (await ReadBodyAsync(response))["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Validate_MissingPlan_Returns422()
    {
        var id = await SeedRunAsync(WorkflowState.Validating);

        var response = await _client.PostAsJsonAsync("/api/validations/run", new { workflowRunId = id });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("PLAN_NOT_FOUND", (await ReadBodyAsync(response))["error"]!.GetValue<string>());
    }

    // ── Approve / dispatch ─────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_PendingPlan_CommitsDispatchAndAudits()
    {
        var id = await SeedRunAsync(WorkflowState.PendingApproval);

        var response = await _client.PostAsJsonAsync($"/api/dispatches/{id}/approve", new { notes = "Looks good" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadBodyAsync(response);
        Assert.Equal("Approved", body["decision"]!.GetValue<string>());
        Assert.Equal("Approved", body["state"]!.GetValue<string>());
        var dispatchId = Guid.Parse(body["dispatchId"]!.GetValue<string>());

        var status = await _client.GetAsync($"/api/dispatches/by-workflow/{id}");
        var statusBody = await ReadBodyAsync(status);
        Assert.Equal("Approved", statusBody["decision"]!.GetValue<string>());
        Assert.False(statusBody["isDelivered"]!.GetValue<bool>());

        var audit = await _client.GetAsync($"/api/audit/{dispatchId}");
        var auditBody = await ReadBodyAsync(audit);
        Assert.Equal("Approved", auditBody["decision"]!.GetValue<string>());
        var events = auditBody["events"]!.AsArray().Select(e => e!["eventType"]!.GetValue<string>()).ToList();
        Assert.Equal(["DispatchApproved"], events);
    }

    [Fact]
    public async Task Approve_WrongState_Returns409()
    {
        var id = await SeedRunAsync(WorkflowState.Matching);

        var response = await _client.PostAsJsonAsync($"/api/dispatches/{id}/approve", new { });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATE", (await ReadBodyAsync(response))["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Approve_UnknownRun_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/dispatches/{Guid.NewGuid()}/approve", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("WORKFLOW_NOT_FOUND", (await ReadBodyAsync(response))["error"]!.GetValue<string>());
    }

    // ── Reject / revision ──────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_WithoutReason_Returns400()
    {
        var id = await SeedRunAsync(WorkflowState.PendingApproval);

        var response = await _client.PostAsJsonAsync($"/api/dispatches/{id}/reject", new { reason = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("REASON_REQUIRED", (await ReadBodyAsync(response))["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task Reject_WithReason_RecordsRejectionAndAudit()
    {
        var id = await SeedRunAsync(WorkflowState.PendingApproval);

        var response = await _client.PostAsJsonAsync($"/api/dispatches/{id}/reject", new { reason = "Duplicate route with shelter #9" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadBodyAsync(response);
        Assert.Equal("Rejected", body["decision"]!.GetValue<string>());
        Assert.Equal("Rejected", body["state"]!.GetValue<string>());
        var dispatchId = Guid.Parse(body["dispatchId"]!.GetValue<string>());

        var status = await _client.GetAsync($"/api/dispatches/by-workflow/{id}");
        var statusBody = await ReadBodyAsync(status);
        Assert.Equal("Rejected", statusBody["decision"]!.GetValue<string>());
        Assert.Equal("Duplicate route with shelter #9", statusBody["notes"]!.GetValue<string>());

        var audit = await _client.GetAsync($"/api/audit/{dispatchId}");
        var events = (await ReadBodyAsync(audit))["events"]!.AsArray().Select(e => e!["eventType"]!.GetValue<string>()).ToList();
        Assert.Equal(["DispatchRejected"], events);
    }

    [Fact]
    public async Task RequestRevision_WithoutNotes_Returns400()
    {
        var id = await SeedRunAsync(WorkflowState.PendingApproval);

        var response = await _client.PostAsJsonAsync($"/api/dispatches/{id}/request-revision", new { notes = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("NOTES_REQUIRED", (await ReadBodyAsync(response))["error"]!.GetValue<string>());
    }

    [Fact]
    public async Task RequestRevision_WithNotes_LoopsBackToMatching()
    {
        var id = await SeedRunAsync(WorkflowState.PendingApproval);

        var response = await _client.PostAsJsonAsync($"/api/dispatches/{id}/request-revision", new { notes = "Re-route via depot 3" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadBodyAsync(response);
        Assert.Equal("RevisionRequested", body["state"]!.GetValue<string>());
        Assert.Equal("Matching", body["loopsBackTo"]!.GetValue<string>());

        await using var db = NewDb();
        var run = await db.WorkflowRuns.SingleAsync(r => r.Id == id);
        Assert.Equal(WorkflowState.RevisionRequested, run.CurrentState);
    }

    // ── Approval queue ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ApprovalQueue_ReturnsOnlyPendingApprovalRuns()
    {
        var pendingId = await SeedRunAsync(WorkflowState.PendingApproval);
        var ownedId = await SeedRunAsync(WorkflowState.Approved);

        var response = await _client.GetAsync("/api/dispatches/approval-queue");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = (await ReadBodyAsync(response))["items"]!.AsArray();
        var ids = items.Select(i => Guid.Parse(i!["workflowRunId"]!.GetValue<string>())).ToList();

        Assert.Contains(pendingId, ids);
        Assert.DoesNotContain(ownedId, ids);
        Assert.All(items, i => Assert.Equal("PendingApproval", i!["state"]!.GetValue<string>()));
    }

    // ── Delivery confirmation (audit-only; state stays Approved) ───────────────

    [Fact]
    public async Task ConfirmDelivery_ApprovedDispatch_RecordsAuditEvent()
    {
        var id = await SeedRunAsync(WorkflowState.PendingApproval);
        await _client.PostAsJsonAsync($"/api/dispatches/{id}/approve", new { });

        var response = await _client.PostAsJsonAsync($"/api/dispatches/{id}/confirm-delivery",
            new { notes = "Handed to shelter manager", photoReference = "img_20260926_01.jpg" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await ReadBodyAsync(response))["isDelivered"]!.GetValue<bool>());

        var status = await _client.GetAsync($"/api/dispatches/by-workflow/{id}");
        var body = await ReadBodyAsync(status);
        Assert.True(body["isDelivered"]!.GetValue<bool>());
        Assert.NotNull(body["deliveredAt"]);

        var dispatch = await _client.GetAsync("/api/dispatches?status=Approved");
        var dispatchBody = await ReadBodyAsync(dispatch);
        var dispatchId = Guid.Parse(dispatchBody["items"]![0]!["id"]!.GetValue<string>());

        var audit = await _client.GetAsync($"/api/audit/{dispatchId}");
        var events = (await ReadBodyAsync(audit))["events"]!.AsArray().Select(e => e!["eventType"]!.GetValue<string>()).ToList();
        Assert.Equal(["DispatchApproved", "DeliveryConfirmed"], events);
    }

    [Fact]
    public async Task ConfirmDelivery_NonApprovedRun_Returns409()
    {
        var id = await SeedRunAsync(WorkflowState.PendingApproval);

        var response = await _client.PostAsJsonAsync($"/api/dispatches/{id}/confirm-delivery", new { });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATE", (await ReadBodyAsync(response))["error"]!.GetValue<string>());
    }

    // ── History / analytics ────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_CountsApprovalsAndRejections()
    {
        var approved1 = await SeedRunAsync(WorkflowState.PendingApproval);
        var approved2 = await SeedRunAsync(WorkflowState.PendingApproval);
        var rejected = await SeedRunAsync(WorkflowState.PendingApproval);

        await _client.PostAsJsonAsync($"/api/dispatches/{approved1}/approve", new { });
        await _client.PostAsJsonAsync($"/api/dispatches/{approved2}/approve", new { notes = "ok" });
        await _client.PostAsJsonAsync($"/api/dispatches/{rejected}/reject", new { reason = "Out of range road closure" });

        var summary = await _client.GetAsync("/api/dispatches/summary");
        var body = await ReadBodyAsync(summary);

        Assert.Equal(3, body["total"]!.GetValue<int>());
        Assert.Equal(2, body["approved"]!.GetValue<int>());
        Assert.Equal(1, body["rejected"]!.GetValue<int>());
        Assert.NotNull(body["avgApprovalMinutes"]);
        Assert.Single(body["topRejectionReasons"]!.AsArray());
    }

    [Fact]
    public async Task List_FiltersByStatusAndRejectsUnknown()
    {
        var id = await SeedRunAsync(WorkflowState.PendingApproval);
        await _client.PostAsJsonAsync($"/api/dispatches/{id}/reject", new { reason = "No fuel available" });

        var filtered = await _client.GetAsync("/api/dispatches?status=Rejected");
        var filteredBody = await ReadBodyAsync(filtered);
        Assert.Equal(1, filteredBody["total"]!.GetValue<int>());
        Assert.Equal("Rejected", filteredBody["items"]![0]!["decision"]!.GetValue<string>());

        var bad = await _client.GetAsync("/api/dispatches?status=Bogus");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("INVALID_STATUS", (await ReadBodyAsync(bad))["error"]!.GetValue<string>());
    }
}