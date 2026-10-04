using FloodLink.Domain;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using FloodLink.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("FloodLinkDB"));
builder.Services.AddControllers();
builder.Services.AddScoped<IMatchingAgentService, MatchingAgentService>();

builder.Services.AddScoped<IAgentExecutionLogger, AgentExecutionLogger>();
builder.Services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();
builder.Services.AddScoped<WorkflowOrchestrator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FloodLink AI API", Version = "v1" });
});
builder.Services.AddControllers();

// TODO (Week 5–6): Register each agent invoker once the real implementations are built.
// Each FloodLink.Agents.* project's class implements the matching IXxxAgentInvoker
// interface from FloodLink.Domain, then gets registered here, e.g.:
//   builder.Services.AddScoped<ITriageAgentInvoker, TriageAgentAdapter>();
// TODO (Week 2): Add JWT authentication / authorization services here.

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();
    if (!context.Depots.Any())
    {
        var depot = new FloodLink.Domain.Entities.Depot { Name = "Central Warehouse", Latitude = 0, Longitude = 0 };
        context.Depots.Add(depot);
        context.SaveChanges();
        
        context.InventoryItems.Add(new FloodLink.Domain.Entities.InventoryItem
        {
            DepotId = depot.Id,
            ItemName = "Water Bottles",
            Unit = "boxes",
            QuantityAvailable = 500,
            QuantityReserved = 50,
            ReorderThreshold = 100
        });
        context.InventoryItems.Add(new FloodLink.Domain.Entities.InventoryItem
        {
            DepotId = depot.Id,
            ItemName = "Blankets",
            Unit = "pieces",
            QuantityAvailable = 10,
            QuantityReserved = 5,
            ReorderThreshold = 20 // Low stock!
        });
        context.SaveChanges();
    }
}

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
