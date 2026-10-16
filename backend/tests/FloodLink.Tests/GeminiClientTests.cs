using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using FloodLink.Domain.Llm;
using FloodLink.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FloodLink.Tests;

/// <summary>GeminiClient request shape, retries and failure handling against a stubbed HTTP handler.</summary>
public class GeminiClientTests
{
    private const string OkBody = """
        {"candidates":[{"content":{"parts":[{"text":"{\"answer\":42}"}]}}],
         "usageMetadata":{"promptTokenCount":120,"candidatesTokenCount":8}}
        """;

    private sealed class StubHandler(Func<int, HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<(HttpRequestMessage Request, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls.Add((request, request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct)));
            return await respond(Calls.Count, request, ct);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body = "{}") =>
        new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static GeminiClient Client(StubHandler handler, string? key = "test-key", int timeoutSeconds = 20) => new(
        new HttpClient(handler),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Gemini:ApiKey"] = key,
            ["Gemini:Model"] = "gemini-test",
            ["Gemini:TimeoutSeconds"] = timeoutSeconds.ToString(),
            ["Gemini:RetryBaseDelayMs"] = "1"
        }).Build(),
        NullLogger<GeminiClient>.Instance);

    private static LlmRequest Request() => new("test.v1", "system text", "user text",
        (JsonObject)JsonNode.Parse("""{"type":"OBJECT","properties":{"answer":{"type":"INTEGER"}}}""")!);

    [Fact]
    public async Task Success_SendsStructuredRequest_AndParsesJson()
    {
        var handler = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, OkBody)));

        var result = await Client(handler).GenerateJsonAsync(Request());

        Assert.True(result.Succeeded);
        Assert.Equal(42, result.Json!["answer"]!.GetValue<int>());
        Assert.Equal((120, 8, 1), (result.PromptTokens!.Value, result.OutputTokens!.Value, result.Attempts));

        var (request, body) = handler.Calls.Single();
        Assert.EndsWith("/models/gemini-test:generateContent", request.RequestUri!.AbsolutePath);
        Assert.Equal("test-key", request.Headers.GetValues("x-goog-api-key").Single());
        Assert.DoesNotContain("test-key", request.RequestUri.ToString()); // never in the URL (logs, proxies)
        var json = JsonNode.Parse(body)!;
        Assert.Equal(0, json["generationConfig"]!["temperature"]!.GetValue<int>());
        Assert.Equal("application/json", json["generationConfig"]!["responseMimeType"]!.GetValue<string>());
        Assert.NotNull(json["generationConfig"]!["responseSchema"]);
        Assert.Equal("system text", json["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task NoKey_MakesNoRequest()
    {
        var handler = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, OkBody)));

        var result = await Client(handler, key: null).GenerateJsonAsync(Request());

        Assert.False(result.Succeeded);
        Assert.Equal("LLM_NOT_CONFIGURED", result.ErrorCode);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task RateLimited_IsRetried_HonouringRetryAfter()
    {
        var handler = new StubHandler((call, _, _) =>
        {
            if (call > 1)
                return Task.FromResult(Json(HttpStatusCode.OK, OkBody));
            var limited = Json(HttpStatusCode.TooManyRequests);
            limited.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(10));
            return Task.FromResult(limited);
        });

        var result = await Client(handler).GenerateJsonAsync(Request());

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task ServerErrors_GiveUpAfterTwoRetries()
    {
        var handler = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.ServiceUnavailable)));

        var result = await Client(handler).GenerateJsonAsync(Request());

        Assert.False(result.Succeeded);
        Assert.Equal("LLM_HTTP_ERROR", result.ErrorCode);
        Assert.Equal(3, handler.Calls.Count);
    }

    [Fact]
    public async Task BadRequest_IsNotRetried()
    {
        var handler = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.BadRequest)));

        var result = await Client(handler).GenerateJsonAsync(Request());

        Assert.False(result.Succeeded);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Timeout_FailsAfterRetries()
    {
        var handler = new StubHandler(async (_, _, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return Json(HttpStatusCode.OK, OkBody);
        });

        var result = await Client(handler, timeoutSeconds: 1).GenerateJsonAsync(Request());

        Assert.False(result.Succeeded);
        Assert.Equal("LLM_TIMEOUT", result.ErrorCode);
        Assert.Equal(3, handler.Calls.Count);
    }

    [Fact]
    public async Task NonJsonText_IsReportedAsInvalidJson()
    {
        var body = """{"candidates":[{"content":{"parts":[{"text":"Sure! Here is the plan:"}]}}]}""";
        var handler = new StubHandler((_, _, _) => Task.FromResult(Json(HttpStatusCode.OK, body)));

        var result = await Client(handler).GenerateJsonAsync(Request());

        Assert.False(result.Succeeded);
        Assert.Equal("LLM_INVALID_JSON", result.ErrorCode);
    }
}
