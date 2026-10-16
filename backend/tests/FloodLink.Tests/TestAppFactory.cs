using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using FloodLink.Api.Auth;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FloodLink.Tests;

/// <summary>
/// Spins up the real FloodLink API host (Program) on the TestServer and points its
/// DbContext at a dedicated Postgres test database (<c>floodlink_test</c>) so the
/// integration tests never touch the demo data in <c>floodlink</c>.
/// Requires a local Postgres running with the <c>floodlink</c> role (setup_db.ps1).
/// There is no token-less access: use <see cref="CreateClientAs"/> or <see cref="TokenFor"/>.
/// </summary>
public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Host=localhost;Port=5432;Database=floodlink_test;Username=floodlink;Password=floodlink_dev_pw";

    public const string SigningKey = "test-only-signing-key-at-least-32-bytes-long!!";
    public const string Issuer = "FloodLink.Api";
    public const string Audience = "FloodLink.Clients";

    /// <summary>Password given to the four demo accounts by <see cref="ResetDatabase"/>.</summary>
    public const string DemoPassword = "test-demo-password";

    protected override void ConfigureWebHost(IWebHostBuilder builder) => ConfigureTestHost(builder);

    /// <summary>Settings shared by every test host (also used by the custom factories in other test classes).</summary>
    public static void ConfigureTestHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestConnectionString);
        builder.UseSetting("Jwt:SigningKey", SigningKey);
        // The test database is reset (and seeded) after the host starts, so skip the startup seeder.
        builder.UseSetting("Seed:SkipOnStartup", "true");
        builder.UseSetting("Auth:LoginPermitLimit", "10000");
        // Tests never call real Gemini, even when a developer's user-secrets hold a key: without a
        // key the agents use their rule-based paths, and LLM behaviour is tested with FakeLlmClient.
        builder.UseSetting("Gemini:ApiKey", "");
    }

    /// <summary>Drops and recreates the test schema so every fixture starts clean, then seeds the demo accounts.</summary>
    public void ResetDatabase() => ResetDatabase(Services);

    public static void ResetDatabase(IServiceProvider services)
    {
        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();
            // EnsureCreated inserts seed rows with explicit ids; move the sequence past them (the migration does the same).
            db.Database.ExecuteSqlRaw(
                "SELECT setval(pg_get_serial_sequence('\"Users\"', 'Id'), GREATEST((SELECT MAX(\"Id\") FROM \"Users\"), 1));");
        }
        DemoAccountSeeder.SeedAsync(services, DemoPassword).GetAwaiter().GetResult();
    }

    /// <summary>Seeded user id for each role (see <see cref="DemoAccounts"/>).</summary>
    public static int DemoUserId(string role) => DemoAccounts.All.First(a => a.Role == role).Id;

    /// <summary>A signed token for <paramref name="role"/>; <c>sub</c> defaults to that role's demo account.</summary>
    public static string TokenFor(string role, int? userId = null, string key = SigningKey, DateTime? expires = null)
    {
        var now = DateTime.UtcNow;
        var expiresAt = expires ?? now.AddHours(1);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, (userId ?? DemoUserId(role)).ToString()),
                new Claim(JwtRegisteredClaimNames.Name, $"test-{role}"),
                new Claim("role", role)
            ]),
            NotBefore = expiresAt.AddHours(-2),
            IssuedAt = expiresAt.AddHours(-2),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256)
        });
    }

    /// <summary>An HTTP client that sends a bearer token for <paramref name="role"/>.</summary>
    public HttpClient CreateClientAs(string role, int? userId = null) => CreateClient().WithToken(TokenFor(role, userId));

    public HttpClient CreateCoordinatorClient() => CreateClientAs(Roles.Coordinator);
}

public static class HttpClientAuthExtensions
{
    public static HttpClient WithToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
