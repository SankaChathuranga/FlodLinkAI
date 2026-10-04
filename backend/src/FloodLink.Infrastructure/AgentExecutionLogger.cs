using System;
using System.Threading;
using System.Threading.Tasks;
using FloodLink.Domain;
using FloodLink.Domain.Entities;

namespace FloodLink.Infrastructure;

/// <summary>
/// EF Core implementation of <see cref="IAgentExecutionLogger"/>.
/// </summary>
public class AgentExecutionLogger : IAgentExecutionLogger
{
    private readonly AppDbContext _dbContext;

    public AgentExecutionLogger(AppDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task LogExecutionAsync(
        Guid workflowRunId,
        string agentName,
        long durationMs,
        bool success,
        bool isRetry = false,
        string? inputJson = null,
        string? outputJson = null,
        string? toolCallsJson = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var status = isRetry ? "Retried" : (success ? "Success" : "Error");

        var log = new AgentExecutionLog
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = workflowRunId,
            AgentName = agentName,
            DurationMs = durationMs,
            Status = status,
            InputJson = inputJson,
            OutputJson = outputJson,
            ToolCallsJson = toolCallsJson,
            ErrorMessage = errorMessage,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AgentExecutionLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
