using FloodLink.Domain.Entities;

namespace FloodLink.Domain;

/// <summary>
/// Minimal read/save contract for WorkflowRun, used by WorkflowOrchestrator.
/// Keeps the orchestrator testable without a real DbContext.
/// </summary>
public interface IWorkflowRunRepository
{
    Task<WorkflowRun?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(WorkflowRun run, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
