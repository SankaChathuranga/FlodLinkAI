using FloodLink.Agents.Routing;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure.Mapbox;
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
        Allocations = [new() { DepotId = 1, ShelterId = 1, ItemName = "Water", Quantity = 100 }],
        Unfulfillable = []
    };

    private static readonly MapboxRouteResult FakeMapboxResult = new()
    {
        DistanceMeters = 15_000,
        DurationSeconds = 1_800,
        EncodedPolyline = "abc123polyline"
    };

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Success_ReturnsOkResult_WithCorrectValues()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ReturnsRouteClient(FakeMapboxResult), repo);

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
        var sut = new RoutingAgentInvoker(new ReturnsRouteClient(FakeMapboxResult), repo);

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
        var sut = new RoutingAgentInvoker(new ReturnsRouteClient(FakeMapboxResult), repo);

        var emptyProposal = new AllocationProposal
        {
            WorkflowRunId = RunId,
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
        var sut = new RoutingAgentInvoker(new ReturnsNullClient(), repo);

        var result = await sut.ExecuteAsync(MakeProposal());

        Assert.False(result.Success);
        Assert.Equal("NO_ROUTE", result.ErrorCode);
        Assert.Empty(repo.Saved);
    }

    [Fact]
    public async Task MapboxTimeout_ReturnsFailWithTimeoutCode()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ThrowsTimeoutClient(), repo);

        var result = await sut.ExecuteAsync(MakeProposal());

        Assert.False(result.Success);
        Assert.Equal("MAPBOX_TIMEOUT", result.ErrorCode);
    }

    [Fact]
    public async Task MapboxHttpError_ReturnsFailWithHttpErrorCode()
    {
        var repo = new FakeRouteRepository();
        var sut = new RoutingAgentInvoker(new ThrowsHttpErrorClient(), repo);

        var result = await sut.ExecuteAsync(MakeProposal());

        Assert.False(result.Success);
        Assert.Equal("MAPBOX_HTTP_ERROR", result.ErrorCode);
    }
}
