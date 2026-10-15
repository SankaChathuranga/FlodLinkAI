using FloodLink.Agents.Matching;
using FloodLink.Agents.Planner;
using FloodLink.Agents.Routing;
using FloodLink.Agents.Triage;
using FloodLink.Agents.Validation;
using FloodLink.Api.Auth;
using FloodLink.Api.Controllers;
using FloodLink.Api.Middleware;
using FloodLink.Api;
using FloodLink.Api.Services;
using FloodLink.Api.Workflows;
using FloodLink.Domain;
using FloodLink.Domain.Llm;
using FloodLink.Domain.Services;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Llm;
using FloodLink.Infrastructure.Mapbox;
using FloodLink.Infrastructure.Services;
using FloodLink.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

// Hosting platforms (Render, Railway, Fly) hand the listening port to the app via PORT.
var hostedPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(hostedPort))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostedPort}");
}

// Outside Development, logs are written as JSON so the host can index the structured fields
// (WorkflowRunId, Agent, DurationMs, UserId, ...) that the services log.
if (!builder.Environment.IsDevelopment())
{
    builder.Logging.AddJsonConsole(options =>
    {
        options.IncludeScopes = true;
        options.UseUtcTimestamp = true;
        options.TimestampFormat = "O";
    });
}

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUrgencyScoringService, UrgencyScoringService>();
builder.Services.AddScoped<IPhotoStorageService, PhotoStorageService>();
builder.Services.AddScoped<ITriageAgent, TriageAgent>();
builder.Services.AddScoped<ReportService>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddScoped<IAgentExecutionLogger, AgentExecutionLogger>();
builder.Services.AddScoped<IWorkflowRunRepository, WorkflowRunRepository>();
builder.Services.AddScoped<WorkflowOrchestrator>();
builder.Services.AddScoped<WorkflowService>();

// Background runner: started, retried and revised runs are queued and advanced off the request thread.
builder.Services.AddSingleton<WorkflowRunQueue>();
builder.Services.AddSingleton<IWorkflowRunQueue>(sp => sp.GetRequiredService<WorkflowRunQueue>());
builder.Services.AddHostedService<WorkflowRunnerService>();

// Stock lifecycle (reserve / commit / release) and coordinator decisions.
builder.Services.AddScoped<StockReservationService>();
builder.Services.AddScoped<IStockReservationService>(sp => sp.GetRequiredService<StockReservationService>());
builder.Services.AddScoped<DispatchService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "FloodLink AI API", Version = "v1" });
    // "Authorize" button: paste the token from POST /api/auth/login.
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token from POST /api/auth/login (without the 'Bearer ' prefix)."
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalDev", policy =>
    {
        // Extra origins for hosted deployments, comma-separated (Cors__AllowedOrigins).
        var extraOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'));
        var allowedOrigins = new[]
        {
            "http://localhost:5173", // React development server
            "http://localhost:5174"  // Flutter Web development server
        }.Concat(extraOrigins).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isDevelopment = builder.Environment.IsDevelopment();
        // In Development any localhost port is allowed: `flutter run -d chrome` picks a random port.
        policy.SetIsOriginAllowed(origin =>
                  allowedOrigins.Contains(origin) ||
                  (isDevelopment && Uri.TryCreate(origin, UriKind.Absolute, out var uri) && uri.IsLoopback))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Standard RFC 7807 problem-details responses for all error results.
builder.Services.AddProblemDetails();

// ── Authentication and role-based access (Phase 2) ───────────────────────────
// The API issues its own tokens (POST /api/auth/login) and validates them on every request.
// The signing key comes from user-secrets or the Jwt__SigningKey environment variable, never
// committed configuration. There is no token-less fallback in any environment.
var jwtSettings = JwtSettings.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuthService>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = jwtSettings.Key,
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
    });

builder.Services.AddSingleton<IAuthorizationHandler, RoleOrAdminHandler>();
builder.Services.AddAuthorization(options => options.AddFloodLinkPolicies());

// Login and registration are rate-limited per client IP to slow password guessing.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var permitLimit = builder.Configuration.GetValue("Auth:LoginPermitLimit", 10);
    var window = TimeSpan.FromSeconds(builder.Configuration.GetValue("Auth:LoginWindowSeconds", 60));
    options.AddPolicy(AuthController.LoginRateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = window, QueueLimit = 0 }));
});

// ── Phase 5: Mapbox client + Route/ETA Agent ─────────────────────────────────

builder.Services.AddHttpClient<IMapboxClient, MapboxClient>(client =>
{
    var timeoutSeconds = builder.Configuration.GetValue<int>("Mapbox:TimeoutSeconds", defaultValue: 10);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
});
builder.Services.AddScoped<IRouteRepository, RouteRepository>();
builder.Services.AddScoped<IRoutingAgentInvoker, RoutingAgentInvoker>();

// ── Phase 3: LLM (Gemini) ─────────────────────────────────────────────────────
// Planner, Triage and Matching ask Gemini for structured JSON and check it; without
// Gemini:ApiKey (or when Gemini fails) they fall back to rule-based logic. The client applies
// its own per-attempt timeout (Gemini:TimeoutSeconds), so the HttpClient's is only a backstop.
builder.Services.AddHttpClient<ILlmClient, GeminiClient>(client => client.Timeout = TimeSpan.FromMinutes(2));
builder.Services.AddScoped<IPlannerInvoker, PlannerAgent>();

// Agent invokers.
builder.Services.AddScoped<IMatchingAgent, MatchingAgent>();
// Member D — Validation/Safety Agent.
builder.Services.AddScoped<IValidationAgent, ValidationAgent>();
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

// Demo accounts (one per role) get their password hash from Seed:DemoPassword. Skipped when unset.
// Tests reset their database after startup and seed it themselves.
if (!app.Configuration.GetValue<bool>("Seed:SkipOnStartup"))
{
    await DemoAccountSeeder.SeedAsync(app.Services, app.Configuration["Seed:DemoPassword"]);
}

// Swagger is served in every environment so the deployed API documents itself.
app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("AllowLocalDev");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.MapGet("/health", () =>
    Results.Ok(new { status = "Healthy", service = "FloodLink API" }));

app.Run();

// Expose for integration test projects (WebApplicationFactory<Program>)
public partial class Program { }
