using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FloodLink.Api.Dtos;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;


public class MatchingAdvanceApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Shelter 1 needs 30 Water; depot 1 holds 20 and depot 2 holds 50, so the need splits.
    private static void SeedShelterReportAndStock(AppDbContext db)
    {
        db.Shelters.Add(new Shelter { Id = 1, Name = "Test Shelter", Latitude = 6.9, Longitude = 79.8, Capacity = 100, CurrentOccupancy = 90 });
        db.Reports.Add(new Report { Id = 1, ShelterId = 1, ReportedBy = 1, NeedType = "Water", QuantityNeeded = 30 });
        db.Depots.AddRange(
            new Depot { Id = 1, Name = "Depot One", Latitude = 6.9, Longitude = 79.8 },
            new Depot { Id = 2, Name = "Depot Two", Latitude = 7.0, Longitude = 79.9 });
        db.InventoryItems.AddRange(
            new InventoryItem { Id = 1, DepotId = 1, ItemName = "water", Unit = "bottles", QuantityAvailable = 20 },
            new InventoryItem { Id = 2, DepotId = 2, ItemName = "Water", Unit = "bottles", QuantityAvailable = 50, QuantityReserved = 5 },
            new InventoryItem { Id = 3, DepotId = 1, ItemName = "Food", Unit = "packs", QuantityAvailable = 40 });
    }

    [Fact]
    public async Task CreateThenAdvance_RunsRealMatching_ReachesRouting_WithoutTouchingInventory()
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(SeedShelterReportAndStock);
        var stockBefore = await factory.QueryAsync(db => db.InventoryItems.AsNoTracking()
            .OrderBy(i => i.Id).Select(i => new { i.Id, i.QuantityAvailable, i.QuantityReserved }).ToListAsync());
        var client = factory.CreateClient();

        // Create runs Triage immediately.
        var created = await client.PostAsJsonAsync("/api/workflows", new { objective = "Gap 1 test" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var run = (await created.Content.ReadFromJsonAsync<WorkflowRunResponse>(Json))!;
        Assert.Equal(nameof(WorkflowState.Matching), run.CurrentState);

        // Advance runs the real Matching Agent.
        var advanced = await client.PostAsync($"/api/workflows/{run.Id}/advance", content: null);
        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);
        var afterMatching = (await advanced.Content.ReadFromJsonAsync<WorkflowRunResponse>(Json))!;
        Assert.Equal(nameof(WorkflowState.Routing), afterMatching.CurrentState);

        // PlanJson.allocationProposal is present and splits the need across both depots.
        using var plan = JsonDocument.Parse(afterMatching.PlanJson!);
        Assert.True(plan.RootElement.TryGetProperty("allocationProposal", out var proposal));
        var allocations = proposal.GetProperty("Allocations").EnumerateArray().ToList();
        Assert.Equal(2, allocations.Count);
        Assert.Equal(30, allocations.Sum(a => a.GetProperty("Quantity").GetDouble()));
        Assert.Empty(proposal.GetProperty("Unfulfillable").EnumerateArray());

        // One AllocationProposals row per allocation, all tied to this run.
        var rows = await factory.QueryAsync(db => db.AllocationProposals.AsNoTracking()
            .Where(p => p.WorkflowRunId == run.Id).OrderBy(p => p.Id).ToListAsync());
        Assert.Equal(2, rows.Count);
        Assert.Equal([1, 2], rows.Select(r => r.DepotId));
        Assert.Equal(proposal.GetProperty("AllocationProposalId").GetInt32(), rows[0].Id);

        // Matching never changes inventory.
        var stockAfter = await factory.QueryAsync(db => db.InventoryItems.AsNoTracking()
            .OrderBy(i => i.Id).Select(i => new { i.Id, i.QuantityAvailable, i.QuantityReserved }).ToListAsync());
        Assert.Equal(stockBefore, stockAfter);

        // One Matching log row for this run.
        var matchingLogs = await factory.QueryAsync(db => db.AgentExecutionLogs.AsNoTracking()
            .CountAsync(l => l.WorkflowRunId == run.Id && l.AgentName == "MatchingAgent"));
        Assert.Equal(1, matchingLogs);
    }

    [Fact]
    public async Task Advance_UnknownRun_Returns404()
    {
        await using var factory = new FloodLinkApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/workflows/{Guid.NewGuid()}/advance", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(WorkflowState.PendingApproval)]
    [InlineData(WorkflowState.Failed)]
    [InlineData(WorkflowState.Approved)]
    [InlineData(WorkflowState.RevisionRequested)]
    public async Task Advance_RunNotInAdvanceableState_Returns409_AndStateIsUnchanged(WorkflowState state)
    {
        await using var factory = new FloodLinkApiFactory();
        var runId = Guid.NewGuid();
        await factory.SeedAsync(db => db.WorkflowRuns.Add(new WorkflowRun { Id = runId, Objective = "Blocked", CurrentState = state }));
        var client = factory.CreateClient();

        var response = await client.PostAsync($"/api/workflows/{runId}/advance", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stored = await factory.QueryAsync(db => db.WorkflowRuns.AsNoTracking().SingleAsync(r => r.Id == runId));
        Assert.Equal(state, stored.CurrentState);
        Assert.Empty(await factory.QueryAsync(db => db.AllocationProposals.ToListAsync()));
    }
}
