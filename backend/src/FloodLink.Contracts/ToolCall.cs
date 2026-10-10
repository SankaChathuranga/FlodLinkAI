namespace FloodLink.Contracts;

/// <summary>
/// One allow-listed tool invocation made by an agent: a database read, a routing API call,
/// a rule evaluation or (later) an LLM call. Input and output hold short structured summaries,
/// never secrets, raw API responses or model reasoning.
/// </summary>
public record ToolCall
{
    /// <summary>Stable tool name, e.g. "db.inventory.read" or "mapbox.directions".</summary>
    public required string Tool { get; init; }

    /// <summary>Validated inputs passed to the tool.</summary>
    public IReadOnlyDictionary<string, object?>? Input { get; init; }

    /// <summary>Summary of what the tool returned.</summary>
    public IReadOnlyDictionary<string, object?>? Output { get; init; }

    /// <summary>True when the tool call completed without error.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Wall-clock time of the call in milliseconds.</summary>
    public long DurationMs { get; init; }

    /// <summary>Error detail when <see cref="Succeeded"/> is false.</summary>
    public string? Error { get; init; }
}
