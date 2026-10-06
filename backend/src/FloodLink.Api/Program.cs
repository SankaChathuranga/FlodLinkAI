using FloodLink.Agents.Matching;
using FloodLink.Agents.Routing;
using FloodLink.Agents.Triage;
using FloodLink.Agents.Validation;
using FloodLink.Api.Middleware;
using FloodLink.Api;
using FloodLink.Domain;
using FloodLink.Domain.Services;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Mapbox;
using FloodLink.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Hosting platforms (Render, Railway, Fly) hand the listening port to the app via PORT.
var hostedPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(hostedPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostedPort}");
}

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUrgencyScoringService, UrgencyScoringService>();
builder.Services.AddScoped<IPhotoStorageService, PhotoStorageService>();
builder.Services.AddScoped<ITriageAgent, TriageAgent>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddScoped<IAgentExecutionLogger, AgentExecutionLogger>();
builder.Services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();
builder.Services.AddScoped<WorkflowOrchestrator>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FloodLink AI API", Version = "v1" });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalDev", policy =>
    {
        // Extra origins for hosted deployments, comma-separated (Cors__AllowedOrigins).
        var extraOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'));
        policy.WithOrigins(
                  new[]
                  {
                      "http://localhost:5173", // React development server
                      "http://localhost:5174"  // Flutter Web development server
                  }.Concat(extraOrigins).ToArray())
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

var authBuilder = builder.Services
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

// Development-only: requests without a bearer token are auto-authenticated as a
// Coordinator so Member D's [Authorize(Roles="Coordinator")] endpoints stay demoable
// before the shared login flow exists. Requests WITH a bearer token still go through
// real JWT validation. Never registered outside Development.
if (builder.Environment.IsDevelopment())
{
    authBuilder
        .AddScheme<AuthenticationSchemeOptions, FloodLink.Api.Auth.DevCoordinatorHandler>("Dev", null)
        .AddPolicyScheme("JwtOrDev", "JWT or dev coordinator", options =>
        {
            options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey("Authorization")
                    ? JwtBearerDefaults.AuthenticationScheme
                    : "Dev";
        });
    builder.Services.Configure<AuthenticationOptions>(o =>
    {
        o.DefaultAuthenticateScheme = "JwtOrDev";
        o.DefaultChallengeScheme = "JwtOrDev";
    });
}
builder.Services.AddAuthorization();

// ── Phase 5: Mapbox client + Route/ETA Agent ─────────────────────────────────

builder.Services.AddHttpClient<IMapboxClient, MapboxClient>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue<int>("Mapbox:TimeoutSeconds", defaultValue: 10);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IRoutingAgentInvoker, RoutingAgentInvoker>();

// Agent invokers.
builder.Services.AddScoped<IMatchingAgent, MatchingAgent>();
// Member D — Validation/Safety Agent + guarded workflow state transitions.
builder.Services.AddScoped<IValidationAgent, ValidationAgent>();
builder.Services.AddScoped<IWorkflowStateService, WorkflowStateService>();
builder.Services.AddScoped<ITriageAgentInvoker, TriageAgentInvoker>();
builder.Services.AddScoped<IMatchingAgentInvoker, MatchingAgentInvoker>();
builder.Services.AddScoped<IValidationAgentInvoker, ValidationAgentInvoker>();

var app = builder.Build();

// Hosted environments start with an empty database; opt in with Database__MigrateOnStartup=true.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
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

// Expose for integration test projects (WebApplicationFactory<Program>)
public partial class Program { }
