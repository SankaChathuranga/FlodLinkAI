using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Infrastructure.Services;

/// <summary>
/// Applies a guarded state transition to a <see cref="WorkflowRun"/>.
/// Owned by Member D (Ijini) for the approval/dispatch flow; the same service is
/// intended to back Member C's orchestration endpoints.
/// </summary>
public interface IWorkflowStateService
{
    /// <summary>
    /// Attempts to move the workflow to <paramref name="target"/>. Fails with
    /// "INVALID_TRANSITION" unless the move is legal per <see cref="WorkflowStateTransitions"/>.
    /// </summary>
    Task<WorkflowTransitionResult> TryTransitionAsync(Guid workflowRunId, WorkflowState target, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a guarded state-transition attempt.</summary>
public record WorkflowTransitionResult(bool Succeeded, string? ErrorCode = null);

/// <inheritdoc />
public sealed class WorkflowStateService : IWorkflowStateService
{
    private readonly AppDbContext _db;

    public WorkflowStateService(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<WorkflowTransitionResult> TryTransitionAsync(
        Guid workflowRunId,
        WorkflowState target,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.WorkflowRuns
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, cancellationToken);

        if (run is null)
        {
            return new WorkflowTransitionResult(false, "WORKFLOW_NOT_FOUND");
        }

        if (!WorkflowStateTransitions.IsAllowed(run.CurrentState, target))
        {
            return new WorkflowTransitionResult(
                false,
                "INVALID_TRANSITION");
        }

        run.CurrentState = target;
        run.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new WorkflowTransitionResult(true);
    }
}