using FloodLink.Agents.Routing;
using FloodLink.Domain;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Mapbox;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAgentExecutionLogger, AgentExecutionLogger>();
builder.Services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();
builder.Services.AddScoped<WorkflowOrchestrator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FloodLink AI API", Version = "v1" });
});
builder.Services.AddControllers();

// Standard RFC 7807 problem-details responses for all error results.
builder.Services.AddProblemDetails();

// JWT tokens are issued by the shared authentication component. This API only
// validates them; the signing key is intentionally supplied through user-secrets
// or the Jwt__SigningKey environment variable, never committed configuration.
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer configuration is required.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience configuration is required.");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SigningKey configuration is required. Set it through user-secrets or Jwt__SigningKey.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role
        };
    });
builder.Services.AddAuthorization();

// ── Phase 5: Mapbox client + Route/ETA Agent ─────────────────────────────────

builder.Services.AddHttpClient<IMapboxClient, MapboxClient>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue<int>("Mapbox:TimeoutSeconds", defaultValue: 10);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IRoutingAgentInvoker, RoutingAgentInvoker>();

// TODO (Week 5–6): Register remaining agent invokers once real implementations are built.
//   builder.Services.AddScoped<ITriageAgentInvoker, TriageAgentAdapter>();
//   builder.Services.AddScoped<IMatchingAgentInvoker, MatchingAgentAdapter>();
//   builder.Services.AddScoped<IValidationAgentInvoker, ValidationAgentAdapter>();
// TODO (Week 2): Add JWT authentication / authorization services here.


var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Endpoints ─────────────────────────────────────────────────────────────────

/// <summary>
/// Health check endpoint. Returns HTTP 200 with a simple JSON payload.
/// Used by CI, Docker healthchecks, and load balancer probes.
/// </summary>
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "FloodLink API" }))
   .WithName("HealthCheck")
   .WithTags("Health")
   .WithOpenApi();

// TODO (Week 2-3): Controllers / minimal-API endpoints per member's area will be
// added here. See CONTRIBUTING.md for ownership details.

app.Run();

// Expose for integration test projects (WebApplicationFactory<Program>)
public partial class Program { }
