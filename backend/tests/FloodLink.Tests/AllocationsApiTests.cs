using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FloodLink.Api.DTOs;
using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Xunit;
using WorkflowRunResponse = FloodLink.Api.Dtos.WorkflowRunResponse;

namespace FloodLink.Tests;

public class AllocationsApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Guid RunA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RunB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    // Two runs, two shelters, two depots; row 4 holds a status other than "Proposed".
    private static void SeedAllocations(AppDbContext db)
    {
        db.Depots.AddRange(
            new Depot { Id = 1, Name = "North Depot", Latitude = 6.9, Longitude = 79.8 },
            new Depot { Id = 2, Name = "South Depot", Latitude = 7.0, Longitude = 79.9 });
        db.Shelters.AddRange(
            new Shelter { Id = 1, Name = "School Hall", Latitude = 6.9, Longitude = 79.8 },
            new Shelter { Id = 2, Name = "Temple Grounds", Latitude = 7.0, Longitude = 79.9 });
        db.WorkflowRuns.AddRange(
            new WorkflowRun { Id = RunA, Objective = "Run A" },
            new WorkflowRun { Id = RunB, Objective = "Run B" });
        db.AllocationProposals.AddRange(
            new AllocationProposalEntity { Id = 1, WorkflowRunId = RunA, DepotId = 1, ShelterId = 1, ItemName = "Water", Quantity = 20 },
            new AllocationProposalEntity { Id = 2, WorkflowRunId = RunA, DepotId = 2, ShelterId = 2, ItemName = "Food", Quantity = 10 },
            new AllocationProposalEntity { Id = 3, WorkflowRunId = RunB, DepotId = 1, ShelterId = 2, ItemName = "Medical", Quantity = 5 },
            new AllocationProposalEntity { Id = 4, WorkflowRunId = RunB, DepotId = 2, ShelterId = 1, ItemName = "Water", Quantity = 8, Status = "Dispatched" });
    }

    private static async Task<List<AllocationResponseDto>> GetAllocationsAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/allocations{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<AllocationResponseDto>>(Json))!;
    }

    [Fact]
    public async Task GetAllocations_NoFilter_ReturnsEveryRow_WithDepotAndShelterNames()
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(SeedAllocations);
        var client = factory.CreateClient();

        var all = await GetAllocationsAsync(client);

        Assert.Equal([1, 2, 3, 4], all.Select(a => a.Id));
        var first = all[0];
        Assert.Equal(("North Depot", "School Hall", "Water", 20d, "Proposed"),
            (first.DepotName, first.ShelterName, first.ItemName, first.Quantity, first.Status));
    }

    [Fact]
    public async Task GetAllocations_WorkflowRunIdFilter_ReturnsOnlyThatRun()
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(SeedAllocations);
        var client = factory.CreateClient();

        var runB = await GetAllocationsAsync(client, $"?workflowRunId={RunB}");

        Assert.Equal([3, 4], runB.Select(a => a.Id));
    }

    [Fact]
    public async Task GetAllocations_ShelterIdFilter_ReturnsOnlyThatShelter()
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(SeedAllocations);
        var client = factory.CreateClient();

        var shelter2 = await GetAllocationsAsync(client, "?shelterId=2");

        Assert.Equal([2, 3], shelter2.Select(a => a.Id));
        Assert.All(shelter2, a => Assert.Equal("Temple Grounds", a.ShelterName));
    }

    [Fact]
    public async Task GetAllocations_StatusFilter_IsCaseInsensitive_AndExcludesOtherStatuses()
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(SeedAllocations);
        var client = factory.CreateClient();

        var proposed = await GetAllocationsAsync(client, "?status=proposed");

        Assert.Equal([1, 2, 3], proposed.Select(a => a.Id));
    }

    [Fact]
    public async Task GetAllocations_CombinedFilters_AreAnded()
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(SeedAllocations);
        var client = factory.CreateClient();

        var result = await GetAllocationsAsync(client, $"?workflowRunId={RunB}&shelterId=1&status=Proposed");

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("?workflowRunId=not-a-guid")]
    [InlineData("?shelterId=0")]
    [InlineData("?shelterId=-3")]
    [InlineData("?shelterId=abc")]
    [InlineData("?status=Bogus")]
    public async Task GetAllocations_InvalidFilter_Returns400(string query)
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(SeedAllocations);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/allocations{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAllocationProposal_UnknownRun_Returns404()
    {
        await using var factory = new FloodLinkApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/workflows/{Guid.NewGuid()}/allocation-proposal");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAllocationProposal_RunBeforeMatching_Returns404()
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(db => db.WorkflowRuns.Add(new WorkflowRun { Id = RunA, Objective = "Not matched yet" }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/workflows/{RunA}/allocation-proposal");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // End to end: real Triage + Matching via the API, then read the proposal back including the gap.
    [Fact]
    public async Task GetAllocationProposal_AfterPartialMatching_ReturnsAllocationsAndUnfulfillable()
    {
        await using var factory = new FloodLinkApiFactory();
        await factory.SeedAsync(db =>
        {
            db.Shelters.Add(new Shelter { Id = 1, Name = "School Hall", Latitude = 6.9, Longitude = 79.8, Capacity = 100, CurrentOccupancy = 80 });
            db.Reports.AddRange(
                new Report { Id = 1, ShelterId = 1, ReportedBy = 1, NeedType = "Water", QuantityNeeded = 30 },
                new Report { Id = 2, ShelterId = 1, ReportedBy = 1, NeedType = "Medical", QuantityNeeded = 4 });
            db.Depots.Add(new Depot { Id = 1, Name = "North Depot", Latitude = 6.9, Longitude = 79.8 });
            db.InventoryItems.Add(new InventoryItem { Id = 1, DepotId = 1, ItemName = "Water", Unit = "bottles", QuantityAvailable = 12 });
        });
        var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/workflows", new { objective = "Read-back test" });
        var run = (await created.Content.ReadFromJsonAsync<WorkflowRunResponse>(Json))!;
        var advanced = await client.PostAsync($"/api/workflows/{run.Id}/advance", content: null);
        Assert.Equal(HttpStatusCode.OK, advanced.StatusCode);

        var response = await client.GetAsync($"/api/workflows/{run.Id}/allocation-proposal");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var proposal = (await response.Content.ReadFromJsonAsync<AllocationProposal>(Json))!;
        Assert.Equal(run.Id, proposal.WorkflowRunId);
        Assert.Equal(12, Assert.Single(proposal.Allocations).Quantity);
        Assert.Equal(2, proposal.Unfulfillable.Count);
        Assert.Contains(proposal.Unfulfillable, u => u.ItemName == "Water" && u.Reason == "Only 12 of 30 could be allocated.");
        Assert.Contains(proposal.Unfulfillable, u => u.ItemName == "Medical" && u.Reason == "No stock is available at any depot.");

        // The list endpoint agrees with the proposal for this run.
        var rows = await GetAllocationsAsync(client, $"?workflowRunId={run.Id}");
        Assert.Equal(proposal.AllocationProposalId, Assert.Single(rows).Id);
    }
}
