using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FloodLink.Infrastructure;

public static class FloodLinkDatabaseProvider
{
    public static void Configure(DbContextOptionsBuilder options, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            UseDemoDatabase(options);
            return;
        }

        var isPostgresConnection = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase);

        if (!isPostgresConnection)
        {
            UseDemoDatabase(options);
            return;
        }

        try
        {
            var connectionBuilder = new NpgsqlConnectionStringBuilder(connectionString)
            {
                Timeout = 2,
                CommandTimeout = 2
            };

            using var connection = new NpgsqlConnection(connectionBuilder.ConnectionString);
            connection.Open();

            options.UseNpgsql(connectionString);
            return;
        }
        catch
        {
            UseDemoDatabase(options);
        }
    }

    private static void UseDemoDatabase(DbContextOptionsBuilder options)
    {
        options.UseInMemoryDatabase($"FloodLinkDemoDb_{Guid.NewGuid():N}");
    }
}
