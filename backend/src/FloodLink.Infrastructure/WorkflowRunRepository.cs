using FloodLink.Domain;
using FloodLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Infrastructure;

/// <summary>EF Core implementation of <see cref="IWorkflowRunRepository"/>.</summary>
public sealed class WorkflowRunRepository : IWorkflowRunRepository
{
    private readonly AppDbContext _db;

    public WorkflowRunRepository(AppDbContext db) => _db = db;

    public Task<WorkflowRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(WorkflowRun run, CancellationToken ct = default)
        => await _db.WorkflowRuns.AddAsync(run, ct);

    public Task SaveAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
