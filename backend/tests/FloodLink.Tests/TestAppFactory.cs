using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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
        builder.UseSetting("Jwt:SigningKey", "test-only-signing-key-at-least-32-bytes-long!!");
        // Disable Program.cs auto-migration so ResetDatabase controls the schema lifecycle.
        builder.UseSetting("Database:MigrateOnStartup", "false");
    }

    /// <summary>
    /// Drops all tables in the test DB and re-applies all migrations so every
    /// fixture starts with a clean, fully-migrated schema.
    /// Uses a direct Npgsql connection to drop tables (bypassing the running server)
    /// then calls Migrate() through the EF DbContext.
    /// </summary>
    public void ResetDatabase()
    {
        // Step 1: Drop all public tables via a direct connection.
        // This is safe whether the server is running or not.
        DropAllTablesDirectStatic();

        // Step 2: Re-apply all migrations through the EF context.
        // Since MigrateOnStartup=false, this is the only migration call.
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    /// <summary>
    /// Drops all user tables in the public schema of floodlink_test using raw SQL,
    /// then clears Npgsql connection pools to release stale connections.
    /// Public static so other test factories (SecurityTests, E2E) can reuse it.
    /// </summary>
    public static void DropAllTablesDirectStatic()
    {
        using var conn = new NpgsqlConnection(TestConnectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DO $$
            DECLARE r RECORD;
            BEGIN
                FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') LOOP
                    EXECUTE 'DROP TABLE IF EXISTS public.' || quote_ident(r.tablename) || ' CASCADE';
                END LOOP;
            END $$;
            """;
        cmd.ExecuteNonQuery();
        conn.Close();
        NpgsqlConnection.ClearAllPools(); // Release stale pooled connections
    }
}