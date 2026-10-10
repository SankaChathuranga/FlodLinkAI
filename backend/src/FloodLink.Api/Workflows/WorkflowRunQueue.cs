using System.Threading.Channels;
using FloodLink.Domain;

namespace FloodLink.Api.Workflows;

/// <summary>
/// In-process queue of workflow runs waiting for the <see cref="WorkflowRunnerService"/>.
/// Registered as a singleton; anything that starts, retries or revises a run enqueues it here.
/// </summary>
public sealed class WorkflowRunQueue : IWorkflowRunQueue
{
    private readonly Channel<Guid> _channel =
        Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    /// <inheritdoc />
    public ValueTask EnqueueAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(workflowRunId, cancellationToken);

    /// <summary>Yields queued run ids until <paramref name="cancellationToken"/> is cancelled.</summary>
    public IAsyncEnumerable<Guid> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);
}
