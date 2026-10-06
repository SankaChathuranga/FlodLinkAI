using System;
using System.Threading;
using System.Threading.Tasks;

namespace FloodLink.Domain;

/// <summary>
/// Service responsible for recording agent executions into the audit log.
/// Owned by Member C. Ensures all agent invocations are durably recorded
/// for observability and compliance.
/// </summary>
public interface IAgentExecutionLogger
{
    /// <summary>
    /// Logs the execution of an agent against a specific workflow run.
    /// </summary>
    /// <param name="workflowRunId">The ID of the workflow run this agent is executing for.</param>
    /// <param name="agentName">The name of the agent (e.g. "TriageAgent").</param>
    /// <param name="durationMs">Execution duration in milliseconds.</param>
    /// <param name="success">True if the agent completed successfully, false otherwise.</param>
    /// <param name="isRetry">
    /// True when this invocation is a retry attempt (not the first try).
    /// When true, the recorded <c>Status</c> is <c>"Retried"</c> regardless of
    /// <paramref name="success"/>, so the log distinguishes a retry outcome from
    /// a clean first-attempt outcome. Defaults to false.
    /// </param>
    /// <param name="inputJson">Serialized input passed to the agent.</param>
    /// <param name="outputJson">Serialized output produced by the agent (null on failure).</param>
    /// <param name="toolCallsJson">Serialized tool calls made by the agent.</param>
    /// <param name="errorMessage">Human-readable error message (null on success).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task LogExecutionAsync(
        Guid workflowRunId,
        string agentName,
        long durationMs,
        bool success,
        bool isRetry = false,
        string? inputJson = null,
        string? outputJson = null,
        string? toolCallsJson = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);
}
