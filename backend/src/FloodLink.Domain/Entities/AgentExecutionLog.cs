namespace FloodLink.Domain.Entities;

/// <summary>
/// Records one agent's execution within a <see cref="WorkflowRun"/>.
/// Owned by Member C. Maps to the <c>AgentExecutionLog</c> table.
/// </summary>
public class AgentExecutionLog
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>FK → <see cref="WorkflowRun"/>.</summary>
    public Guid WorkflowRunId { get; set; }

    /// <summary>Navigation property.</summary>
    public WorkflowRun? WorkflowRun { get; set; }

    /// <summary>
    /// The logical name of the agent that produced this log entry
    /// (e.g. "TriageAgent", "MatchingAgent", "RoutingAgent", "ValidationAgent").
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>JSON serialisation of the agent's input.</summary>
    public string? InputJson { get; set; }

    /// <summary>JSON serialisation of the agent's output.</summary>
    public string? OutputJson { get; set; }

    /// <summary>JSON array of the tool calls the agent made (see <c>Contracts.ToolCall</c>).</summary>
    public string? ToolCallsJson { get; set; }

    /// <summary>Wall-clock execution time in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Execution status: "Success" or "Error".</summary>
    public required string Status { get; set; }

    /// <summary>True when this execution was a coordinator-requested retry of a failed stage.</summary>
    public bool IsRetry { get; set; }

    /// <summary>
    /// Human-readable failure detail when <see cref="Status"/> is "Error" or "Retried".
    /// Sourced directly from <c>AgentResult.ErrorMessage</c> — null on success.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC timestamp when the agent execution started.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
