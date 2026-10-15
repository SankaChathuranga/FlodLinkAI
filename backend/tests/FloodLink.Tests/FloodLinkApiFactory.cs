using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace FloodLink.Tests;


internal sealed class FloodLinkApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private bool _startupDataCleared;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs refuses to start without a signing key; it is never used by these tests.
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-at-least-32-bytes-long");

        builder.ConfigureServices(services =>
        {
            // Replace the Npgsql registration so the two providers don't collide.
            var npgsql = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                            d.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>))
                .ToList();
            foreach (var descriptor in npgsql)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName, _databaseRoot));
        });
    }

    public async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Program.cs runs EnsureCreated() and seeds demo depots/shelters at startup. Tests seed their
        // own rows with fixed ids, so start each factory from an empty database instead.
        if (!_startupDataCleared)
        {
            await db.Database.EnsureDeletedAsync();
            _startupDataCleared = true;
        }

        seed(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }
}
