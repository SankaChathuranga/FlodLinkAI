using System.Net;
using System.Text;
using FloodLink.Infrastructure.Mapbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloodLink.Tests;

/// <summary>Retry behaviour of the Mapbox Directions client against a stubbed HTTP handler.</summary>
public class MapboxClientTests
{
    private const string RouteJson = """{"routes":[{"distance":12345.6,"duration":901.2,"geometry":"poly"}]}""";

    private sealed class SequenceHandler(params HttpStatusCode[] codes) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var code = codes[Math.Min(Calls, codes.Length - 1)];
            Calls++;
            return Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(code == HttpStatusCode.OK ? RouteJson : "{}", Encoding.UTF8, "application/json")
            });
        }
    }

    private static MapboxClient Client(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mapbox:ApiKey"] = "test",
            ["Mapbox:RetryBaseDelayMs"] = "1"
        }).Build(),
        NullLogger<MapboxClient>.Instance);

    [Fact]
    public async Task RateLimited_IsRetried_ThenSucceeds()
    {
        var handler = new SequenceHandler(HttpStatusCode.TooManyRequests, HttpStatusCode.OK);

        var result = await Client(handler).GetRouteAsync(79.86, 6.93, 79.98, 6.93);

        Assert.NotNull(result);
        Assert.Equal(12_346, result.DistanceMeters);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task RateLimited_EveryTime_GivesUpAfterTheRetryLimit()
    {
        var handler = new SequenceHandler(HttpStatusCode.TooManyRequests);

        var result = await Client(handler).GetRouteAsync(79.86, 6.93, 79.98, 6.93);

        Assert.Null(result);
        Assert.Equal(3, handler.Calls); // first try + 2 retries
    }

    [Fact]
    public async Task ClientError_IsNotRetried()
    {
        var handler = new SequenceHandler(HttpStatusCode.Unauthorized);

        var result = await Client(handler).GetRouteAsync(79.86, 6.93, 79.98, 6.93);

        Assert.Null(result);
        Assert.Equal(1, handler.Calls);
    }
}
