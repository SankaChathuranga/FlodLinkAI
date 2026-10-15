using System.Text.Json.Nodes;
using FloodLink.Contracts;

namespace FloodLink.Domain.Llm;

/// <summary>
/// A large-language-model call that must return JSON matching <see cref="LlmRequest.ResponseSchema"/>.
/// Implementations enforce temperature 0, a timeout and bounded retries, and never throw for
/// anticipated failures (no key, timeout, rate limit, bad output): they return a failed
/// <see cref="LlmResult"/> so the calling agent can fall back to its rule-based logic.
/// </summary>
public interface ILlmClient
{
    Task<LlmResult> GenerateJsonAsync(LlmRequest request, CancellationToken ct = default);
}

/// <param name="PromptVersion">Version of the prompt file, e.g. <c>triage.v1</c>; logged with the call.</param>
/// <param name="SystemInstruction">Fixed instructions from the versioned prompt file.</param>
/// <param name="UserContent">The data for this call. Untrusted text must already be fenced (see <c>LlmData</c>).</param>
/// <param name="ResponseSchema">Gemini-style response schema (OpenAPI subset, upper-case types).</param>
public sealed record LlmRequest(
    string PromptVersion,
    string SystemInstruction,
    string UserContent,
    JsonObject ResponseSchema);

/// <summary>Outcome of one <see cref="ILlmClient.GenerateJsonAsync"/> call (all attempts).</summary>
public sealed record LlmResult
{
    public required bool Succeeded { get; init; }

    /// <summary>The model's JSON output when <see cref="Succeeded"/>.</summary>
    public JsonNode? Json { get; init; }

    public required string Model { get; init; }
    public long LatencyMs { get; init; }
    public int Attempts { get; init; }
    public int? PromptTokens { get; init; }
    public int? OutputTokens { get; init; }

    /// <summary>Short machine-readable reason on failure, e.g. LLM_NOT_CONFIGURED, LLM_TIMEOUT.</summary>
    public string? ErrorCode { get; init; }
    public string? Error { get; init; }

    public static LlmResult Fail(string model, string code, string error, long latencyMs = 0, int attempts = 0) =>
        new() { Succeeded = false, Model = model, ErrorCode = code, Error = error, LatencyMs = latencyMs, Attempts = attempts };
}

/// <summary>Builds the execution-log entry for an LLM call. Holds metadata only — never prompts, output or keys.</summary>
public static class LlmToolCall
{
    public const string ToolName = "llm.generate";

    /// <param name="usedFallback">True when the agent discarded the LLM output (call failed or output was invalid).</param>
    /// <param name="rejectedReason">Why valid-looking output was rejected by the deterministic check.</param>
    public static ToolCall From(LlmRequest request, LlmResult result, bool usedFallback, string? rejectedReason = null) => new()
    {
        Tool = ToolName,
        Input = new Dictionary<string, object?>
        {
            ["promptVersion"] = request.PromptVersion,
            ["model"] = result.Model
        },
        Output = new Dictionary<string, object?>
        {
            ["latencyMs"] = result.LatencyMs,
            ["attempts"] = result.Attempts,
            ["promptTokens"] = result.PromptTokens,
            ["outputTokens"] = result.OutputTokens,
            ["usedFallback"] = usedFallback,
            ["rejectedReason"] = rejectedReason
        },
        Succeeded = result.Succeeded && !usedFallback,
        DurationMs = result.LatencyMs,
        Error = result.Succeeded ? rejectedReason : $"{result.ErrorCode}: {result.Error}"
    };
}
