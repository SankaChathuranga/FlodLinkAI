using FloodLink.Agents.Validation;
using FloodLink.Api.Endpoints;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FloodLink AI API", Version = "v1" });
});

// Member D (Ijini) — Validation/Safety Agent + guarded workflow state transitions.
builder.Services.AddScoped<IValidationAgent, ValidationAgent>();
builder.Services.AddScoped<IWorkflowStateService, WorkflowStateService>();

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

// ── Endpoints ─────────────────────────────────────────────────────────────────

/// <summary>
/// Health check endpoint. Returns HTTP 200 with a simple JSON payload.
/// Used by CI, Docker healthchecks, and load balancer probes.
/// </summary>
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "FloodLink API" }))
   .WithName("HealthCheck")
   .WithTags("Health");

// Member D (Ijini) — validation and coordinator approval/dispatch/audit.
app.MapValidationEndpoints();
app.MapDispatchEndpoints();

// TODO (Week 2-3): Controllers / minimal-API endpoints per member's area will be
// added here. See CONTRIBUTING.md for ownership details.

app.Run();

// Expose for integration test projects (WebApplicationFactory<Program>)
public partial class Program { }