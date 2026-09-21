namespace FloodLink.Domain.Entities;

/// <summary>
/// Records one agent's execution within a <see cref="WorkflowRun"/>.
/// Owned by Member C. Maps to the <c>AgentExecutionLog</c> table.
/// </summary>
/// <remarks>
/// TODO (Member C — Week 2): Finalize JSON column types; confirm whether
/// <see cref="ToolCallsJson"/> should be <c>jsonb</c> or <c>text</c> in Postgres.
/// </remarks>
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

    /// <summary>JSON serialisation of the agent's input. TODO: use jsonb in Postgres.</summary>
    public string? InputJson { get; set; }

    /// <summary>JSON serialisation of the agent's output. TODO: use jsonb in Postgres.</summary>
    public string? OutputJson { get; set; }

    /// <summary>JSON array of any tool calls the agent made. TODO: use jsonb in Postgres.</summary>
    public string? ToolCallsJson { get; set; }

    /// <summary>Wall-clock execution time in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Execution status: "Success", "Error", or "Retried".</summary>
    public required string Status { get; set; }

    /// <summary>UTC timestamp when the agent execution started.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
