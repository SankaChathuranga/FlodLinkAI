using FloodLink.Agents.Routing;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure.Mapbox;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Phase 5 — unit tests for <see cref="RoutingAgentInvoker"/>.
/// Uses a fake <see cref="IMapboxClient"/> and a fake <see cref="IRouteRepository"/>
/// so no live Mapbox call or real database is required.
/// </summary>
public class RoutingAgentInvokerTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed class FakeRouteRepository : IRouteRepository
    {
        public List<RouteEntity> Saved { get; } = new();

        public Task AddAsync(RouteEntity route, CancellationToken ct = default)
        {
            Saved.Add(route);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class ReturnsRouteClient : IMapboxClient
    {
        private readonly MapboxRouteResult _result;
        public ReturnsRouteClient(MapboxRouteResult result) => _result = result;

        public Task<MapboxRouteResult?> GetRouteAsync(
            double originLng, double originLat, double destLng, double destLat,
            CancellationToken cancellationToken = default)
            => Task.FromResult<MapboxRouteResult?>(_result);
    }

    // Returns a longer route for shelter 2 so the longest leg is identifiable.
    private sealed class PerShelterClient : IMapboxClient
    {
        public int Calls { get; private set; }

        public Task<MapboxRouteResult?> GetRouteAsync(
            double originLng, double originLat, double destLng, double destLat,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            var far = destLat > 7;
            return Task.FromResult<MapboxRouteResult?>(new MapboxRouteResult
            {
                DistanceMeters = far ? 40_000 : 10_000,
                DurationSeconds = far ? 3_600 : 900,
                EncodedPolyline = far ? "far" : "near"
            });
        }
    }

    private sealed class ReturnsNullClient : IMapboxClient
    {
        public Task<MapboxRouteResult?> GetRouteAsync(
            double originLng, double originLat, double destLng, double destLat,
            CancellationToken cancellationToken = default)
            => Task.FromResult<MapboxRouteResult?>(null);
    }

    private sealed class ThrowsTimeoutClient : IMapboxClient
    {
        public Task<MapboxRouteResult?> GetRouteAsync(
            double originLng, double originLat, double destLng, double destLat,
            CancellationToken cancellationToken = default)
            => throw new TaskCanceledException("Simulated timeout");
    }

    private sealed class ThrowsHttpErrorClient : IMapboxClient
    {
        public Task<MapboxRouteResult?> GetRouteAsync(
            double originLng, double originLat, double destLng, double destLat,
            CancellationToken cancellationToken = default)
            => throw new HttpRequestException("Simulated HTTP error");
    }

    // ── Shared test data ───────────────────────────────────────────────────────

    private static readonly Guid RunId = Guid.NewGuid();

    private static AllocationProposal MakeProposal() => new()
    {
        WorkflowRunId = RunId,
        AllocationProposalId = 1,
        Allocations = [new() { DepotId = 1, ShelterId = 1, ItemName = "Water", Quantity = 100 }],
        Unfulfillable = []
    };

    private static readonly MapboxRouteResult FakeMapboxResult = new()
    {
        DistanceMeters = 15_000,
        DurationSeconds = 1_800,
        EncodedPolyline = "abc123polyline"
    };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new AppDbContext(options);
        context.Depots.Add(new Depot { Id = 1, Name = "Test Depot", Latitude = 6.9271, Longitude = 79.8612 });
        context.Shelters.Add(new Shelter { Id = 1, Name = "Test Shelter", Latitude = 6.9344, Longitude = 79.9841 });
        context.SaveChanges();
        return context;
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Success_ReturnsOkResult_WithCorrectValues()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ReturnsRouteClient(FakeMapboxResult), repo, CreateContext());

        var result = await sut.ExecuteAsync(MakeProposal());

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(RunId, result.Data.WorkflowRunId);
        Assert.Equal(15.0, result.Data.DistanceKm, precision: 3);     // 15000 m → 15 km
        Assert.Equal(30.0, result.Data.EtaMinutes, precision: 3);     // 1800 s → 30 min
        Assert.Equal("abc123polyline", result.Data.Polyline);
    }

    [Fact]
    public async Task Success_PersistsRouteEntity_WithCorrectValues()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ReturnsRouteClient(FakeMapboxResult), repo, CreateContext());

        await sut.ExecuteAsync(MakeProposal());

        Assert.Single(repo.Saved);
        var entity = repo.Saved[0];
        Assert.Equal(RunId, entity.WorkflowRunId);
        Assert.Equal(15_000, entity.DistanceMeters);
        Assert.Equal(1_800, entity.EstimatedDurationSeconds);
        Assert.Equal("abc123polyline", entity.PolylineString);
    }

    [Fact]
    public async Task NoAllocations_ReturnsFailWithNoAllocationsCode()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ReturnsRouteClient(FakeMapboxResult), repo, CreateContext());

        var emptyProposal = new AllocationProposal
        {
            WorkflowRunId = RunId,
            AllocationProposalId = 1,
            Allocations = [],
            Unfulfillable = []
        };

        var result = await sut.ExecuteAsync(emptyProposal);

        Assert.False(result.Success);
        Assert.Equal("NO_ALLOCATIONS", result.ErrorCode);
        Assert.Empty(repo.Saved); // nothing persisted
    }

    [Fact]
    public async Task MapboxReturnsNull_ReturnsFailWithNoRouteCode()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ReturnsNullClient(), repo, CreateContext());

        var result = await sut.ExecuteAsync(MakeProposal());

        Assert.False(result.Success);
        Assert.Equal("NO_ROUTE", result.ErrorCode);
        Assert.Empty(repo.Saved);
    }

    [Fact]
    public async Task MapboxTimeout_ReturnsFailWithTimeoutCode()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ThrowsTimeoutClient(), repo, CreateContext());

        var result = await sut.ExecuteAsync(MakeProposal());

        Assert.False(result.Success);
        Assert.Equal("MAPBOX_TIMEOUT", result.ErrorCode);
    }

    [Fact]
    public async Task MapboxHttpError_ReturnsFailWithHttpErrorCode()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ThrowsHttpErrorClient(), repo, CreateContext());

        var result = await sut.ExecuteAsync(MakeProposal());

        Assert.False(result.Success);
        Assert.Equal("MAPBOX_HTTP_ERROR", result.ErrorCode);
    }

    [Fact]
    public async Task MultiplePairs_RoutesEachLegOnce_AndReportsTheLongest()
    {
        var context = CreateContext();
        context.Shelters.Add(new Shelter { Id = 2, Name = "Far Shelter", Latitude = 7.2, Longitude = 80.1 });
        context.SaveChanges();
        var repo = new FakeRouteRepository();
        var mapbox = new PerShelterClient();
        var sut = new RoutingAgentInvoker(mapbox, repo, context);
        var proposal = MakeProposal() with
        {
            Allocations =
            [
                new() { DepotId = 1, ShelterId = 1, ItemName = "Water", Quantity = 100 },
                new() { DepotId = 1, ShelterId = 1, ItemName = "Food", Quantity = 50 },
                new() { DepotId = 1, ShelterId = 2, ItemName = "Water", Quantity = 30 }
            ]
        };

        var result = await sut.ExecuteAsync(proposal);

        Assert.True(result.Success);
        Assert.Equal(2, mapbox.Calls);
        Assert.Equal(2, result.Data!.Legs.Count);
        Assert.Equal("far", result.Data.Polyline);
        Assert.Equal(60, result.Data.EtaMinutes, precision: 3);
        Assert.Equal(2, repo.Saved.Count);
        Assert.Equal([1, 2], repo.Saved.Select(r => r.ShelterId!.Value).OrderBy(id => id).ToArray());
        Assert.Equal(2, result.ToolCalls.Count(call => call.Tool == "mapbox.directions" && call.Succeeded));
    }

    [Fact]
    public async Task FailedLeg_FailsTheStep_AndPersistsNoRoutes()
    {
        var context = CreateContext();
        context.Shelters.Add(new Shelter { Id = 2, Name = "Far Shelter", Latitude = 7.2, Longitude = 80.1 });
        context.SaveChanges();
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ReturnsNullClient(), repo, context);
        var proposal = MakeProposal() with
        {
            Allocations =
            [
                new() { DepotId = 1, ShelterId = 1, ItemName = "Water", Quantity = 100 },
                new() { DepotId = 1, ShelterId = 2, ItemName = "Water", Quantity = 30 }
            ]
        };

        var result = await sut.ExecuteAsync(proposal);

        Assert.False(result.Success);
        Assert.Equal("NO_ROUTE", result.ErrorCode);
        Assert.Empty(repo.Saved);
        Assert.False(result.ToolCalls.Single().Succeeded);
    }
}
