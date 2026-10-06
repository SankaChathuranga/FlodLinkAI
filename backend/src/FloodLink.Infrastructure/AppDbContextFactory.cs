using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FloodLink.Infrastructure;

/// <summary>
/// Creates the database context for EF Core tooling without starting the API host.
/// The connection string is supplied through environment configuration so design-time
/// commands do not require JWT settings or committed secrets.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection must be set to create EF Core migrations.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
