using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FloodLink.Tests;

/// <summary>
/// Spins up the real FloodLink API host (Program) on the TestServer and points its
/// DbContext at a dedicated Postgres test database (<c>floodlink_test</c>) so the
/// integration tests never touch the demo data in <c>floodlink</c>.
/// Requires a local Postgres running with the <c>floodlink</c> role (setup_db.ps1).
/// </summary>
public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Host=localhost;Port=5432;Database=floodlink_test;Username=floodlink;Password=floodlink_dev_pw";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestConnectionString);
    }

    /// <summary>Drops and recreates the test schema so every fixture starts clean.</summary>
    public void ResetDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureDeleted();
        db.Database.EnsureCreated();
    }
}