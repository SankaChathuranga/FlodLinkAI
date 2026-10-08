using FloodLink.Domain.Entities;

namespace FloodLink.Domain;

/// <summary>
/// Persistence interface for Route/ETA Agent output.
/// Allows the Routing agent to save a <see cref="RouteEntity"/> without
/// depending on EF Core directly.
/// </summary>
public interface IRouteRepository
{
    /// <summary>Persists a new <see cref="RouteEntity"/> row.</summary>
    Task AddAsync(RouteEntity route, CancellationToken ct = default);

    /// <summary>Flushes pending changes to the database.</summary>
    Task SaveAsync(CancellationToken ct = default);
}
