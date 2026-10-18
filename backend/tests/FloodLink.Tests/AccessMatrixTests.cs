using System.Net;
using System.Text;
using FloodLink.Domain.Security;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// The access matrix from FloodLink_Project_Finalization.md (Phase 2), one row per endpoint:
/// no token → 401; every role outside the row → 403; every role in the row (and Admin) gets past
/// authorization (any status other than 401/403 — usually 200, 400 or 404 for the dummy ids used).
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AccessMatrixTests : IClassFixture<TestAppFactory>
{
    private const string V = Roles.Volunteer, C = Roles.Coordinator, D = Roles.DepotManager;
    private static readonly string Run = Guid.NewGuid().ToString();

    private readonly TestAppFactory _factory;

    public AccessMatrixTests(TestAppFactory factory)
    {
        _factory = factory;
        _factory.ResetDatabase();
    }

    public static TheoryData<string, string, string?, string[]> Matrix => new()
    {
        // Shelters
        { "GET", "/api/shelters", null, [V, C, D] },
        { "GET", "/api/shelters/1", null, [V, C, D] },
        { "POST", "/api/shelters", "{}", [C] },
        { "PUT", "/api/shelters/1", "{}", [C] },
        // Reports
        { "POST", "/api/reports", "{}", [V] },
        { "GET", "/api/reports/mine", null, [V] },
        { "GET", "/api/reports", null, [C] },
        { "PUT", "/api/reports/1", "{}", [C] },
        { "GET", "/api/reports/triage-plans", null, [C] },
        { "POST", "/api/reports/999999/trigger-triage", null, [C] },
        // Depots, inventory, allocations
        { "GET", "/api/depots", null, [C, D] },
        { "GET", "/api/depots/1", null, [C, D] },
        { "GET", "/api/depots/managers", null, [D] },
        { "GET", "/api/inventory", null, [C, D] },
        { "GET", "/api/allocations", null, [C, D] },
        { "POST", "/api/allocations/999999/trigger-matching", null, [C] },
        { "POST", "/api/routes/calculate", "{}", [C] },
        { "GET", $"/api/routes/{Run}", null, [C] },
        { "GET", $"/api/routes?workflowRunId={Run}", null, [C] },
        { "POST", "/api/depots", "{}", [D] },
        { "PUT", "/api/depots/999999", "{}", [D] },
        { "POST", "/api/inventory", "{}", [D] },
        { "PUT", "/api/inventory/999999/check-in", """{"quantityReceived":0}""", [D] },
        { "PUT", "/api/inventory/999999/reserve", "{}", [D] },
        { "PUT", "/api/inventory/999999/release", "{}", [D] },
        // Workflows
        { "POST", "/api/workflows", "{}", [C] },
        { "GET", "/api/workflows", null, [C] },
        { "GET", $"/api/workflows/{Run}", null, [C] },
        { "GET", $"/api/workflows/{Run}/logs", null, [C] },
        { "POST", $"/api/workflows/{Run}/retry", null, [C] },
        // Decisions, validation, audit, dispatch history
        { "POST", $"/api/dispatches/{Run}/approve", "{}", [C] },
        { "POST", $"/api/dispatches/{Run}/reject", "{}", [C] },
        { "POST", $"/api/dispatches/{Run}/request-revision", "{}", [C] },
        { "GET", "/api/dispatches/approval-queue", null, [C] },
        { "GET", "/api/dispatches", null, [C] },
        { "GET", "/api/dispatches/summary", null, [C] },
        { "POST", "/api/validations/run", "{}", [C] },
        { "GET", $"/api/validations/{Run}", null, [C] },
        { "GET", $"/api/audit/{Run}", null, [C] },
        { "GET", "/api/audit", null, [C] },
        { "GET", "/api/audit/event-types", null, [C] },
        // Ownership rows: a Volunteer passes the role check and then gets 404 for a run they don't own.
        { "GET", $"/api/workflows/{Run}/status", null, [V] },
        { "GET", $"/api/dispatches/by-workflow/{Run}", null, [V, C] },
        { "POST", $"/api/dispatches/{Run}/confirm-delivery", "{}", [V, C] },
        // Users
        { "GET", "/api/users", null, [] },
        { "POST", "/api/users", "{}", [] },
    };

    private async Task<HttpStatusCode> SendAsync(string method, string url, string? body, string? role)
    {
        var client = role is null ? _factory.CreateClient() : _factory.CreateClientAs(role);
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (body is not null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        return (await client.SendAsync(request)).StatusCode;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task MATRIX_EnforcesRolesForEveryEndpoint(string method, string url, string? body, string[] allowed)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, await SendAsync(method, url, body, null));

        foreach (var role in Roles.All)
        {
            var status = await SendAsync(method, url, body, role);
            if (role == Roles.Admin || allowed.Contains(role))
                Assert.True(status is not (HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden),
                    $"{role} should reach {method} {url} but got {(int)status}");
            else
                Assert.True(status == HttpStatusCode.Forbidden, $"{role} should get 403 on {method} {url} but got {(int)status}");
        }
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/auth/register")]
    public async Task MATRIX_AuthEndpoints_ArePublic(string url)
        => Assert.Equal(HttpStatusCode.BadRequest, await SendAsync("POST", url, "{}", null));

    [Fact]
    public async Task MATRIX_HealthAndSwagger_ArePublic()
    {
        Assert.Equal(HttpStatusCode.OK, await SendAsync("GET", "/health", null, null));
        Assert.Equal(HttpStatusCode.OK, await SendAsync("GET", "/swagger/v1/swagger.json", null, null));
    }

    [Fact]
    public async Task MATRIX_VolunteerStatusForOwnRun_IsAllowed_ButNotForCoordinator()
    {
        // Seed report 4 (user 1, the demo volunteer) is attached to a run here.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FloodLink.Infrastructure.AppDbContext>();
        var run = new FloodLink.Domain.Entities.WorkflowRun { Id = Guid.NewGuid(), Objective = "ownership", ReportIds = [4] };
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        Assert.Equal(HttpStatusCode.OK, await SendAsync("GET", $"/api/workflows/{run.Id}/status", null, V));
        Assert.Equal(HttpStatusCode.OK, await SendAsync("GET", $"/api/dispatches/by-workflow/{run.Id}", null, V));
        Assert.Equal(HttpStatusCode.Forbidden, await SendAsync("GET", $"/api/workflows/{run.Id}/status", null, C));

        // Another volunteer (user 99 owns nothing) cannot see it.
        var other = _factory.CreateClientAs(V, userId: 99);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/workflows/{run.Id}/status")).StatusCode);
    }
}
