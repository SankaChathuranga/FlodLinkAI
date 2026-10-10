using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FloodLink.Infrastructure.Mapbox;

/// <summary>
/// Calls the Mapbox Directions API (driving profile) to compute a route between
/// two coordinates. Enforces a configurable timeout and a bounded retry count
/// (max 2 retries, transient failures only) per architecture.md invariant 5.
/// Transient failures are timeouts, network errors, HTTP 5xx and HTTP 429 (rate limited);
/// retries back off exponentially and honour a <c>Retry-After</c> header.
/// </summary>
/// <remarks>
/// Registered as a scoped service via <c>Program.cs</c>. Reads
/// <c>Mapbox:ApiKey</c>, optional <c>Mapbox:TimeoutSeconds</c> and optional
/// <c>Mapbox:RetryBaseDelayMs</c> from configuration (user-secrets / environment variable in production).
/// </remarks>
public sealed class MapboxClient : IMapboxClient
{
    private const string DrivingProfile = "driving";
    private const int MaxRetries = 2;
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly ILogger<MapboxClient> _logger;
    private readonly TimeSpan _retryBaseDelay;

    public MapboxClient(HttpClient http, IConfiguration configuration, ILogger<MapboxClient> logger)
    {
        _http = http;
        _logger = logger;

        _apiKey = configuration["Mapbox:ApiKey"]
            ?? throw new InvalidOperationException(
                "Mapbox:ApiKey is not configured. Add it to user-secrets or environment variables. " +
                "See appsettings.json for the expected key path.");

        _retryBaseDelay = TimeSpan.FromMilliseconds(
            configuration.GetValue("Mapbox:RetryBaseDelayMs", defaultValue: 500));

        // Timeout is set on the HttpClient at registration time (see Program.cs).
        // A missing TimeoutSeconds key falls back to the HttpClient's default (100 s).
    }

    /// <inheritdoc />
    public async Task<MapboxRouteResult?> GetRouteAsync(
        double originLng, double originLat,
        double destLng, double destLat,
        CancellationToken cancellationToken = default)
    {
        // Mapbox Directions API: coordinates are lng,lat (not lat,lng).
        var url = $"https://api.mapbox.com/directions/v5/mapbox/{DrivingProfile}" +
                  $"/{originLng},{originLat};{destLng},{destLat}" +
                  $"?geometries=polyline&overview=full&access_token={_apiKey}";

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await _http.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Mapbox Directions API returned {StatusCode} on attempt {Attempt}/{Max}.",
                        (int)response.StatusCode, attempt + 1, MaxRetries + 1);

                    // 4xx errors other than 429 (rate limited) are not transient — don't retry.
                    var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests;
                    if ((int)response.StatusCode < 500 && !rateLimited)
                        return null;

                    if (attempt == MaxRetries) return null;
                    await Task.Delay(RetryDelay(attempt, response.Headers.RetryAfter), cancellationToken);
                    continue;
                }

                var body = await response.Content.ReadFromJsonAsync<MapboxDirectionsResponse>(
                    cancellationToken: cancellationToken);

                var route = body?.Routes?.FirstOrDefault();
                if (route is null)
                {
                    _logger.LogWarning("Mapbox returned success but no routes for {Coords}.",
                        $"{originLng},{originLat} → {destLng},{destLat}");
                    return null;
                }

                return new MapboxRouteResult
                {
                    // Mapbox returns distance in metres (float) and duration in seconds (float).
                    DistanceMeters = (int)Math.Round(route.Distance),
                    DurationSeconds = (int)Math.Round(route.Duration),
                    EncodedPolyline = route.Geometry ?? string.Empty
                };
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient timeout fired — treat as transient, retry up to MaxRetries.
                _logger.LogWarning("Mapbox request timed out on attempt {Attempt}/{Max}.",
                    attempt + 1, MaxRetries + 1);
                if (attempt == MaxRetries) throw; // re-throw so RoutingAgent converts to Fail
                await Task.Delay(RetryDelay(attempt, null), cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Mapbox HTTP request failed on attempt {Attempt}/{Max}.",
                    attempt + 1, MaxRetries + 1);
                if (attempt == MaxRetries) throw; // re-throw so RoutingAgent converts to Fail
                await Task.Delay(RetryDelay(attempt, null), cancellationToken);
            }
        }

        return null; // unreachable, but satisfies the compiler
    }

    // Exponential backoff (base, 2×base, …), or the server's Retry-After when given; capped.
    private TimeSpan RetryDelay(int attempt, RetryConditionHeaderValue? retryAfter)
    {
        var delay = retryAfter?.Delta
                    ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null)
                    ?? _retryBaseDelay * Math.Pow(2, attempt);
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        return delay > MaxRetryDelay ? MaxRetryDelay : delay;
    }

    // ── Private DTOs (only map fields we actually use) ─────────────────────────

    private sealed class MapboxDirectionsResponse
    {
        [JsonPropertyName("routes")]
        public List<MapboxRoute>? Routes { get; init; }
    }

    private sealed class MapboxRoute
    {
        /// <summary>Total distance of the route in metres.</summary>
        [JsonPropertyName("distance")]
        public double Distance { get; init; }

        /// <summary>Estimated travel duration in seconds.</summary>
        [JsonPropertyName("duration")]
        public double Duration { get; init; }

        /// <summary>Encoded polyline string (Polyline5 format when geometries=polyline).</summary>
        [JsonPropertyName("geometry")]
        public string? Geometry { get; init; }
    }
}
