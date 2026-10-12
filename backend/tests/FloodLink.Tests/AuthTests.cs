using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// AUTH-001..: login, registration, current user, Admin staff management, ownership and the
/// login rate limit (FloodLink_Project_Finalization.md 2.01–2.12).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AuthTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    private readonly HttpClient _anonymous;

    public AuthTests(TestAppFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
        _anonymous = _factory.CreateClient();
    }

    private AppDbContext NewDb() => _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();

    private Task<HttpResponseMessage> LoginAsync(string email, string password)
        => _anonymous.PostAsJsonAsync("/api/auth/login", new { email, password });

    private async Task<string> LoginTokenAsync(string email, string password)
    {
        var response = await LoginAsync(email, password);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonObject>())!["token"]!.GetValue<string>();
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("volunteer@floodlink.lk", "Volunteer")]
    [InlineData("coordinator@floodlink.lk", "Coordinator")]
    [InlineData("depot@floodlink.lk", "DepotManager")]
    [InlineData("admin@floodlink.lk", "Admin")]
    public async Task AUTH001_DemoAccount_LogsIn_AndMeReturnsIt(string email, string role)
    {
        var response = await LoginAsync(email.ToUpperInvariant(), TestAppFactory.DemoPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(role, body["user"]!["role"]!.GetValue<string>());
        var expiresAt = body["expiresAt"]!.GetValue<DateTime>();
        Assert.InRange(expiresAt, DateTime.UtcNow.AddHours(7.9), DateTime.UtcNow.AddHours(8.1));

        var me = await _factory.CreateClient().WithToken(body["token"]!.GetValue<string>())
            .GetFromJsonAsync<JsonObject>("/api/auth/me");
        Assert.Equal(email, me!["email"]!.GetValue<string>());
        Assert.Equal(role, me["role"]!.GetValue<string>());
        Assert.Null(me["hashedPassword"]);
    }

    [Fact]
    public async Task AUTH002_WrongPassword_AndUnknownEmail_GiveTheSame401()
    {
        var wrongPassword = await LoginAsync("coordinator@floodlink.lk", "not-the-password");
        var unknownEmail = await LoginAsync("nobody@floodlink.lk", "not-the-password");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        var a = (await wrongPassword.Content.ReadFromJsonAsync<JsonObject>())!;
        var b = (await unknownEmail.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(a["title"]!.GetValue<string>(), b["title"]!.GetValue<string>());
    }

    [Fact]
    public async Task AUTH003_PlaceholderHashes_AreGone_AndPasswordsAreHashed()
    {
        await using var db = NewDb();
        var hashes = await db.Users.Select(u => u.HashedPassword).ToListAsync();
        Assert.DoesNotContain(hashes, h => h.StartsWith("hashed_password"));
        Assert.All(hashes, h => Assert.NotEqual(TestAppFactory.DemoPassword, h));
    }

    [Fact]
    public async Task AUTH004_Me_WithoutToken_Returns401()
        => Assert.Equal(HttpStatusCode.Unauthorized, (await _anonymous.GetAsync("/api/auth/me")).StatusCode);

    // ── Registration ──────────────────────────────────────────────────────────

    [Fact]
    public async Task AUTH005_Register_AlwaysCreatesVolunteer_EvenWhenBodyAsksForAdmin()
    {
        var response = await _anonymous.PostAsJsonAsync("/api/auth/register", new
        {
            name = "New Helper", email = "Helper@Example.com", password = "long-enough-pw", phone = "+94771112223",
            role = "Admin"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(Roles.Volunteer, body["user"]!["role"]!.GetValue<string>());
        Assert.Equal("helper@example.com", body["user"]!["email"]!.GetValue<string>());

        await using var db = NewDb();
        var stored = await db.Users.SingleAsync(u => u.Email == "helper@example.com");
        Assert.Equal(Roles.Volunteer, stored.Role);
        Assert.NotEqual("long-enough-pw", stored.HashedPassword);

        // The returned token is a Volunteer token: it can read its own reports but not manage users.
        var client = _factory.CreateClient().WithToken(body["token"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/reports/mine")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/users")).StatusCode);

        // And the new account can log in with its password.
        await LoginTokenAsync("helper@example.com", "long-enough-pw");
    }

    [Fact]
    public async Task AUTH006_Register_ShortPassword_Returns400()
    {
        var response = await _anonymous.PostAsJsonAsync("/api/auth/register",
            new { name = "Short", email = "short@example.com", password = "1234567", phone = "+94771112223" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AUTH007_Register_TakenEmail_Returns409()
    {
        var response = await _anonymous.PostAsJsonAsync("/api/auth/register",
            new { name = "Dup", email = "Volunteer@FloodLink.lk", password = "long-enough-pw", phone = "+94771112223" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    // ── Admin user management ─────────────────────────────────────────────────

    [Fact]
    public async Task AUTH008_Admin_CreatesCoordinator_WhoCanThenLogIn()
    {
        var admin = _factory.CreateClientAs(Roles.Admin);
        var create = await admin.PostAsJsonAsync("/api/users", new
        {
            name = "Second Coordinator", email = "coord2@floodlink.lk", password = "coordinator-pw",
            phone = "+94770000002", role = Roles.Coordinator
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var token = await LoginTokenAsync("coord2@floodlink.lk", "coordinator-pw");
        var asCoordinator = _factory.CreateClient().WithToken(token);
        Assert.Equal(HttpStatusCode.OK, (await asCoordinator.GetAsync("/api/dispatches/approval-queue")).StatusCode);

        var list = await admin.GetStringAsync("/api/users");
        Assert.Contains("coord2@floodlink.lk", list);
        Assert.DoesNotContain("hashedPassword", list, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(Roles.Volunteer)]
    [InlineData(Roles.Admin)]
    [InlineData("SuperUser")]
    public async Task AUTH009_Admin_CannotCreateNonStaffRoles(string role)
    {
        var response = await _factory.CreateClientAs(Roles.Admin).PostAsJsonAsync("/api/users", new
        {
            name = "X", email = $"{role.ToLowerInvariant()}-new@floodlink.lk", password = "long-enough-pw",
            phone = "+94770000003", role
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AUTH010_Coordinator_CannotCreateUsers()
    {
        var response = await _factory.CreateCoordinatorClient().PostAsJsonAsync("/api/users", new
        {
            name = "X", email = "x@floodlink.lk", password = "long-enough-pw", phone = "+94770000003", role = Roles.Coordinator
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Ownership (Volunteer sees only their own) ─────────────────────────────

    [Fact]
    public async Task AUTH011_Volunteer_ReadsOwnReport_But404ForAnotherVolunteersReport()
    {
        // Seed: reports 1, 2 and 4 belong to user 1 (the demo volunteer); report 3 to user 2.
        var volunteer = _factory.CreateClientAs(Roles.Volunteer);
        Assert.Equal(HttpStatusCode.OK, (await volunteer.GetAsync("/api/reports/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await volunteer.GetAsync("/api/reports/3")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await volunteer.GetAsync("/api/reports")).StatusCode);
    }

    [Fact]
    public async Task AUTH012_ReportsMine_ReturnsOnlyOwnReports_WithWorkflowFields()
    {
        var mine = await _factory.CreateClientAs(Roles.Volunteer).GetFromJsonAsync<JsonArray>("/api/reports/mine");
        var ids = mine!.Select(r => r!["id"]!.GetValue<int>()).Order().ToArray();
        Assert.Equal([1, 2, 4], ids);
        Assert.True(mine[0]!.AsObject().ContainsKey("workflowState"));
        Assert.True(mine[0]!.AsObject().ContainsKey("decision"));
    }

    [Fact]
    public async Task AUTH013_SubmittedReport_ReporterComesFromToken_NotTheForm()
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("1"), "ShelterId" },
            { new StringContent("2"), "ReportedBy" }, // ignored: not the signed-in user
            { new StringContent("Water"), "NeedType" },
            { new StringContent("10"), "QuantityNeeded" }
        };
        var response = await _factory.CreateClientAs(Roles.Volunteer).PostAsync("/api/reports", form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var id = (await response.Content.ReadFromJsonAsync<JsonObject>())!["id"]!.GetValue<int>();

        await using var db = NewDb();
        Assert.Equal(TestAppFactory.DemoUserId(Roles.Volunteer), (await db.Reports.FindAsync(id))!.ReportedBy);
    }

    [Fact]
    public async Task AUTH014_ReportList_NeverSerializesPasswordHashes()
    {
        var json = await _factory.CreateCoordinatorClient().GetStringAsync("/api/reports");
        Assert.Contains("reporter", json);
        Assert.DoesNotContain("hashedPassword", json, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>AUTH-015: login is rate-limited per client (built-in ASP.NET Core rate limiter).</summary>
[Collection(PostgresCollection.Name)]
public sealed class LoginRateLimitTests : IClassFixture<LoginRateLimitTests.Factory>
{
    public const int Limit = 3;

    public sealed class Factory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            TestAppFactory.ConfigureTestHost(builder);
            builder.UseSetting("Auth:LoginPermitLimit", Limit.ToString());
            builder.UseSetting("Auth:LoginWindowSeconds", "300");
        }
    }

    private readonly Factory _factory;

    public LoginRateLimitTests(Factory factory)
    {
        _factory = factory;
        TestAppFactory.ResetDatabase(_factory.Services);
    }

    [Fact]
    public async Task AUTH015_Login_Returns429_AfterLimitIsReached()
    {
        var client = _factory.CreateClient();
        for (var i = 0; i < Limit; i++)
        {
            var attempt = await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@floodlink.lk", password = "wrong-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "admin@floodlink.lk", password = TestAppFactory.DemoPassword });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }
}
