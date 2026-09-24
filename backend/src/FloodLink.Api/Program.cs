using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using FloodLink.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddControllers();
builder.Services.AddScoped<IMatchingAgentService, MatchingAgentService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FloodLink AI API", Version = "v1" });
});

// TODO (Week 2): Add JWT authentication / authorization services here.
// TODO (Week 2): Register agent interface implementations via DI here (all as stubs initially).

var app = builder.Build();

// ── Middleware ────────────────────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
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
