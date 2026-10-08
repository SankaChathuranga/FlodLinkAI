using FloodLink.Domain;
using FloodLink.Domain.Entities;

namespace FloodLink.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="IRouteRepository"/>.
/// Persists <see cref="RouteEntity"/> rows to the <c>Routes</c> table via <see cref="AppDbContext"/>.
/// </summary>
public sealed class RouteRepository : IRouteRepository
{
    private readonly AppDbContext _db;

    public RouteRepository(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task AddAsync(RouteEntity route, CancellationToken ct = default)
        => await _db.Routes.AddAsync(route, ct);

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
