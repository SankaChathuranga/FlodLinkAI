using FloodLink.Agents.Matching;
using FloodLink.Agents.Routing;
using FloodLink.Agents.Triage;
using FloodLink.Agents.Validation;
using FloodLink.Api.Middleware;
using FloodLink.Api;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Services;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Mapbox;
using FloodLink.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDbContext<AppDbContext>(options =>
    FloodLinkDatabaseProvider.Configure(options, builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUrgencyScoringService, UrgencyScoringService>();
builder.Services.AddScoped<IPhotoStorageService, PhotoStorageService>();
builder.Services.AddScoped<ITriageAgent, TriageAgent>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddScoped<IAgentExecutionLogger, AgentExecutionLogger>();
builder.Services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();
builder.Services.AddScoped<WorkflowOrchestrator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FloodLink AI API", Version = "v1" });
    var xmlDocs = Path.Combine(AppContext.BaseDirectory, "FloodLink.Api.xml");
    if (File.Exists(xmlDocs))
        c.IncludeXmlComments(xmlDocs);
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalDev", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
                  Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
                  uri.Scheme == Uri.UriSchemeHttp &&
                  uri.IsLoopback)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

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

// Agent invokers. Matching (B) and Validation (D) are still stubs that throw
// NotImplementedException; the orchestrator records that as a Failed run until they land.
builder.Services.AddScoped<IMatchingAgent, MatchingAgent>();
builder.Services.AddScoped<IValidationAgent, ValidationAgent>();
builder.Services.AddScoped<ITriageAgentInvoker, TriageAgentInvoker>();
builder.Services.AddScoped<IMatchingAgentInvoker, MatchingAgentInvoker>();
builder.Services.AddScoped<IValidationAgentInvoker, ValidationAgentInvoker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (db.Database.IsRelational())
    {
        db.Database.ExecuteSqlRaw(@"
            ALTER TABLE IF EXISTS ""Depots""
                ADD COLUMN IF NOT EXISTS ""ManagerId"" integer;

            ALTER TABLE IF EXISTS ""InventoryItems""
                ADD COLUMN IF NOT EXISTS ""UpdatedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                ADD COLUMN IF NOT EXISTS ""QuantityReserved"" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS ""ReorderThreshold"" integer NOT NULL DEFAULT 10;
        ");
    }

    if (!db.Depots.Any())
    {
        var depots = new[]
        {
            new Depot { Name = "Central Depot", Latitude = 6.9271, Longitude = 79.8612 },
            new Depot { Name = "North Relief Hub", Latitude = 6.9500, Longitude = 79.9000 },
            new Depot { Name = "Harbor Supply Point", Latitude = 6.8900, Longitude = 79.8800 }
        };

        db.Depots.AddRange(depots);
        db.SaveChanges();

        db.InventoryItems.AddRange(
            new InventoryItem { DepotId = depots[0].Id, ItemName = "Water", Unit = "bottles", QuantityAvailable = 240, UpdatedAt = DateTime.UtcNow },
            new InventoryItem { DepotId = depots[0].Id, ItemName = "Rice", Unit = "bags", QuantityAvailable = 90, UpdatedAt = DateTime.UtcNow },
            new InventoryItem { DepotId = depots[1].Id, ItemName = "Medical Kits", Unit = "kits", QuantityAvailable = 48, UpdatedAt = DateTime.UtcNow },
            new InventoryItem { DepotId = depots[2].Id, ItemName = "Blankets", Unit = "packs", QuantityAvailable = 120, UpdatedAt = DateTime.UtcNow }
        );

        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("AllowLocalDev");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () =>
    Results.Ok(new { status = "Healthy", service = "FloodLink API" }));

app.Run();

public partial class Program { }
