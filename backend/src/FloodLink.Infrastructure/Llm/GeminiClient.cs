using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using FloodLink.Domain.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FloodLink.Infrastructure.Llm;

/// <summary>
/// <see cref="ILlmClient"/> for the Gemini API (<c>models/{model}:generateContent</c>, REST).
/// Structured JSON output against a response schema, temperature 0, a per-attempt timeout
/// (<c>Gemini:TimeoutSeconds</c>, default 30) and at most 2 retries for timeouts, network errors,
/// HTTP 5xx and 429 (honouring <c>Retry-After</c>). Without <c>Gemini:ApiKey</c> it makes no request
/// and returns LLM_NOT_CONFIGURED, so every agent runs on its rule-based fallback.
/// <c>Gemini:Model</c> defaults to <c>gemini-3.8-flash</c> (Google closed <c>gemini-2.5-flash</c> to new
/// keys in 2026). <c>Gemini:ThinkingLevel</c> (default <c>low</c>; empty to omit) trades the model's
/// internal reasoning for latency — the agents' tasks are small and every output is checked in code.
/// </summary>
/// <remarks>
/// Logs status codes and timings only — never prompts, model output or the key.
/// The key is sent in the <c>x-goog-api-key</c> header, not the URL.
/// </remarks>
public sealed class GeminiClient : ILlmClient
{
    public const int MaxRetries = 2;
    private const string Endpoint = "https://generativelanguage.googleapis.com/v1beta/models";
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

    private readonly HttpClient _http;
    private readonly ILogger<GeminiClient> _logger;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly string? _thinkingLevel;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _retryBaseDelay;

    public GeminiClient(HttpClient http, IConfiguration configuration, ILogger<GeminiClient> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = configuration["Gemini:ApiKey"];
        _model = configuration["Gemini:Model"] is { Length: > 0 } model ? model : "gemini-3.8-flash";
        // Only Gemini 3+ models accept thinkingLevel; set it to "" for older models.
        _thinkingLevel = configuration["Gemini:ThinkingLevel"] ?? "low";
        _timeout = TimeSpan.FromSeconds(configuration.GetValue("Gemini:TimeoutSeconds", 30));
        _retryBaseDelay = TimeSpan.FromMilliseconds(configuration.GetValue("Gemini:RetryBaseDelayMs", 1000));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey) && _apiKey != "YOUR_GEMINI_API_KEY_HERE";

    public async Task<LlmResult> GenerateJsonAsync(LlmRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return LlmResult.Fail(_model, "LLM_NOT_CONFIGURED", "Gemini:ApiKey is not set.");

        var body = new JsonObject
        {
            ["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray(new JsonObject { ["text"] = request.SystemInstruction })
            },
            ["contents"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = request.UserContent })
            }),
            ["generationConfig"] = new JsonObject
            {
                ["temperature"] = 0,
                ["responseMimeType"] = "application/json",
                ["responseSchema"] = request.ResponseSchema.DeepClone()
            }
        };

        if (!string.IsNullOrWhiteSpace(_thinkingLevel))
            body["generationConfig"]!["thinkingConfig"] = new JsonObject { ["thinkingLevel"] = _thinkingLevel };

        var sw = Stopwatch.StartNew();
        string? lastError = null;
        var lastCode = "LLM_ERROR";

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_timeout);
            RetryConditionHeaderValue? retryAfter = null;

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{_model}:generateContent")
                {
                    Content = JsonContent.Create(body)
                };
                message.Headers.Add("x-goog-api-key", _apiKey);

                using var response = await _http.SendAsync(message, timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<JsonObject>(timeout.Token);
                    return Parse(payload, sw.ElapsedMilliseconds, attempt + 1);
                }

                var status = (int)response.StatusCode;
                lastCode = response.StatusCode == HttpStatusCode.TooManyRequests ? "LLM_RATE_LIMITED" : "LLM_HTTP_ERROR";
                lastError = $"Gemini returned HTTP {status}.";
                _logger.LogWarning("Gemini returned {StatusCode} on attempt {Attempt}/{Max} ({PromptVersion})",
                    status, attempt + 1, MaxRetries + 1, request.PromptVersion);

                // 4xx other than 429 (bad key, bad schema) will not succeed on retry.
                if (status < 500 && response.StatusCode != HttpStatusCode.TooManyRequests)
                    break;
                retryAfter = response.Headers.RetryAfter;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                lastCode = "LLM_TIMEOUT";
                lastError = $"No response within {_timeout.TotalSeconds:F0} s.";
                _logger.LogWarning("Gemini timed out on attempt {Attempt}/{Max} ({PromptVersion})",
                    attempt + 1, MaxRetries + 1, request.PromptVersion);
            }
            catch (HttpRequestException ex)
            {
                lastCode = "LLM_NETWORK_ERROR";
                lastError = ex.Message;
                _logger.LogWarning("Gemini network error on attempt {Attempt}/{Max}: {Error}",
                    attempt + 1, MaxRetries + 1, ex.Message);
            }

            if (attempt < MaxRetries)
                await Task.Delay(RetryDelay(attempt, retryAfter), ct);
        }

        return LlmResult.Fail(_model, lastCode, lastError ?? "Gemini call failed.", sw.ElapsedMilliseconds, MaxRetries + 1);
    }

    private LlmResult Parse(JsonObject? payload, long latencyMs, int attempts)
    {
        var usage = payload?["usageMetadata"];
        int? promptTokens = usage?["promptTokenCount"]?.GetValue<int>();
        int? outputTokens = usage?["candidatesTokenCount"]?.GetValue<int>();

        var text = payload?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            var finish = payload?["candidates"]?[0]?["finishReason"]?.GetValue<string>()
                         ?? payload?["promptFeedback"]?["blockReason"]?.GetValue<string>() ?? "EMPTY";
            return LlmResult.Fail(_model, "LLM_EMPTY_OUTPUT", $"No text in the response ({finish}).", latencyMs, attempts)
                with { PromptTokens = promptTokens, OutputTokens = outputTokens };
        }

        try
        {
            return new LlmResult
            {
                Succeeded = true,
                Json = JsonNode.Parse(text),
                Model = _model,
                LatencyMs = latencyMs,
                Attempts = attempts,
                PromptTokens = promptTokens,
                OutputTokens = outputTokens
            };
        }
        catch (JsonException)
        {
            return LlmResult.Fail(_model, "LLM_INVALID_JSON", "The model did not return valid JSON.", latencyMs, attempts)
                with { PromptTokens = promptTokens, OutputTokens = outputTokens };
        }
    }

    private TimeSpan RetryDelay(int attempt, RetryConditionHeaderValue? retryAfter)
    {
        var requested = retryAfter?.Delta
            ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null);
        var delay = requested is { } d && d > TimeSpan.Zero
            ? d
            : TimeSpan.FromMilliseconds(_retryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt));
        return delay > MaxRetryDelay ? MaxRetryDelay : delay;
    }
}
