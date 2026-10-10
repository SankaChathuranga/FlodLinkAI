namespace FloodLink.Contracts;

/// <summary>
/// Unified return type for every agent method. Agents must return this type instead
/// of throwing exceptions for expected/anticipated failure conditions.
/// </summary>
/// <typeparam name="T">The output data type produced by a successful agent run.</typeparam>
public record AgentResult<T>
{
    /// <summary>True when the agent completed successfully and <see cref="Data"/> is populated.</summary>
    public bool Success { get; init; }

    /// <summary>The agent's output when <see cref="Success"/> is true; null otherwise.</summary>
    public T? Data { get; init; }

    /// <summary>Human-readable description of the failure reason when <see cref="Success"/> is false.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Machine-readable error code for programmatic error handling (e.g. "INPUT_NULL",
    /// "MAPS_API_TIMEOUT"). Null on success.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// The allow-listed tool calls the agent made while producing this result (database reads,
    /// routing API calls, LLM calls). Recorded in the execution log for observability.
    /// </summary>
    public IReadOnlyList<ToolCall> ToolCalls { get; init; } = [];

    /// <summary>Creates a successful result containing the given data.</summary>
    public static AgentResult<T> Ok(T data) =>
        new() { Success = true, Data = data };

    /// <summary>Creates a failure result with an error code and human-readable message.</summary>
    public static AgentResult<T> Fail(string errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

    /// <summary>Returns a copy of this result carrying the given tool calls.</summary>
    public AgentResult<T> WithToolCalls(IEnumerable<ToolCall> toolCalls) =>
        this with { ToolCalls = toolCalls.ToList() };
}
