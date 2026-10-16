using System.Text.Json.Nodes;
using FloodLink.Domain.Llm;

namespace FloodLink.Tests;

/// <summary>
/// Scripted <see cref="ILlmClient"/> for tests and CI: answers by prompt version (e.g. "triage.v1")
/// with a handler, and records every request. Unscripted prompts fail with LLM_NOT_CONFIGURED,
/// exactly like Gemini without a key.
/// </summary>
public sealed class FakeLlmClient : ILlmClient
{
    private readonly Dictionary<string, Func<LlmRequest, LlmResult>> _handlers = new();

    public List<LlmRequest> Requests { get; } = [];

    public FakeLlmClient Returns(string promptVersion, string json)
        => On(promptVersion, _ => Ok(json));

    public FakeLlmClient On(string promptVersion, Func<LlmRequest, LlmResult> handler)
    {
        _handlers[promptVersion] = handler;
        return this;
    }

    public FakeLlmClient Fails(string promptVersion, string code = "LLM_TIMEOUT")
        => On(promptVersion, _ => LlmResult.Fail("fake-model", code, "scripted failure", 5, 3));

    public static LlmResult Ok(string json) => new()
    {
        Succeeded = true,
        Json = JsonNode.Parse(json),
        Model = "fake-model",
        LatencyMs = 12,
        Attempts = 1,
        PromptTokens = 100,
        OutputTokens = 50
    };

    public Task<LlmResult> GenerateJsonAsync(LlmRequest request, CancellationToken ct = default)
    {
        lock (Requests)
            Requests.Add(request);
        return Task.FromResult(_handlers.TryGetValue(request.PromptVersion, out var handler)
            ? handler(request)
            : LlmResult.Fail("fake-model", "LLM_NOT_CONFIGURED", "not scripted"));
    }
}
