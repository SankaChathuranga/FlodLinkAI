namespace FloodLink.Domain;

/// <summary>
/// Hands a workflow run to the background runner, which advances it through the automatic
/// stages until it needs a human (PendingApproval) or fails safely.
/// </summary>
public interface IWorkflowRunQueue
{
    /// <summary>Queues <paramref name="workflowRunId"/> for processing. Queuing a run twice is harmless.</summary>
    ValueTask EnqueueAsync(Guid workflowRunId, CancellationToken cancellationToken = default);
}
