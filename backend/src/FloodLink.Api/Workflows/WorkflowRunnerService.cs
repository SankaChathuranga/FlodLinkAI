using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Workflows;

/// <summary>
/// Background worker that drives queued workflow runs through the automatic stages
/// (Triage → Matching → Routing → Validating) until each one reaches PendingApproval or fails.
/// </summary>
/// <remarks>
/// <para>
/// Runs are processed one at a time, so two plans never match against the same stock
/// simultaneously; the stock reservation at the end of validation then holds that stock until
/// a coordinator decides.
/// </para>
/// <para>
/// On startup, runs left in an automatic state (for example after a restart mid-run) are queued
/// again. Unexpected errors fail the run with a recorded reason; they never stop the worker.
/// </para>
/// </remarks>
public sealed class WorkflowRunnerService(
    WorkflowRunQueue queue,
    IServiceScopeFactory scopes,
    ILogger<WorkflowRunnerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeueUnfinishedRunsAsync(stoppingToken);

        try
        {
            await foreach (var workflowRunId in queue.DequeueAllAsync(stoppingToken))
                await ProcessAsync(workflowRunId, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is shutting down.
        }
    }

    private async Task ProcessAsync(Guid workflowRunId, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<WorkflowOrchestrator>();
            var run = await orchestrator.RunToCompletionAsync(workflowRunId, ct);
            logger.LogInformation("Workflow {WorkflowRunId} stopped in {State}.", workflowRunId, run.CurrentState);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down; the run is picked up again on the next start.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow {WorkflowRunId} failed unexpectedly.", workflowRunId);
            await MarkFailedAsync(workflowRunId, ex.Message, ct);
        }
    }

    private async Task MarkFailedAsync(Guid workflowRunId, string message, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await db.WorkflowRuns.FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);
            if (run is null || !WorkflowEngine.TryTransition(run, WorkflowState.Failed))
                return;

            run.FailureReason = $"RUNNER_ERROR: {message}";
            db.AgentExecutionLogs.Add(new AgentExecutionLog
            {
                Id = Guid.NewGuid(),
                WorkflowRunId = workflowRunId,
                AgentName = "Orchestrator",
                Status = "Error",
                ErrorMessage = message
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Could not record the failure of workflow {WorkflowRunId}.", workflowRunId);
        }
    }

    private async Task RequeueUnfinishedRunsAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var automatic = WorkflowEngine.AutomaticStates.ToList();
            var ids = await db.WorkflowRuns.AsNoTracking()
                .Where(r => automatic.Contains(r.CurrentState))
                .OrderBy(r => r.CreatedAt)
                .Select(r => r.Id)
                .ToListAsync(ct);

            foreach (var id in ids)
                await queue.EnqueueAsync(id, ct);

            if (ids.Count > 0)
                logger.LogInformation("Re-queued {Count} unfinished workflow runs.", ids.Count);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // The database may not be ready yet (e.g. before migrations); new runs still queue normally.
            logger.LogWarning(ex, "Could not re-queue unfinished workflow runs at startup.");
        }
    }
}
