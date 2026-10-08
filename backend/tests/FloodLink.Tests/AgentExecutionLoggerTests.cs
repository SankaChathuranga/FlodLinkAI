using System;
using System.Threading.Tasks;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Tests for <see cref="AgentExecutionLogger"/>.
/// Covers Phase 3, Task 11 (round-trip test).
/// </summary>
public class AgentExecutionLoggerTests
{
    private static DbContextOptions<AppDbContext> CreateNewContextOptions()
    {
        // Using real postgres (docker) since InMemory package download fails due to network
        string dbName = "testdb_" + Guid.NewGuid().ToString().Replace("-", "");
        string connStr = $"Host=localhost;Port=5432;Database={dbName};Username=floodlink;Password=floodlink_dev_pw";
        
        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connStr)
            .Options;
    }

    [Fact]
    public async Task LogExecutionAsync_WritesToDatabase_AndCanBeReadBack()
    {
        var options = CreateNewContextOptions();
        var runId = Guid.NewGuid();

        // 0. Create database
        using (var context = new AppDbContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        try
        {
            // 1. Setup - seed the WorkflowRun
            using (var context = new AppDbContext(options))
            {
                context.WorkflowRuns.Add(new WorkflowRun
                {
                    Id = runId,
                    Objective = "Test Run"
                });
                await context.SaveChangesAsync();
            }

            // 2. Act - log an execution
            using (var context = new AppDbContext(options))
            {
                var logger = new AgentExecutionLogger(context);
                await logger.LogExecutionAsync(
                    workflowRunId: runId,
                    agentName: "TriageAgent",
                    durationMs: 450,
                    success: false,
                    inputJson: "{\"req\": 1}",
                    outputJson: null,
                    toolCallsJson: null,
                    errorMessage: "Failed to parse API response"
                );
            }

            // 3. Assert - read back and verify fields match exactly
            using (var context = new AppDbContext(options))
            {
                var logs = await context.AgentExecutionLogs.ToListAsync();
                Assert.Single(logs);

                var log = logs[0];
                Assert.Equal(runId, log.WorkflowRunId);
                Assert.Equal("TriageAgent", log.AgentName);
                Assert.Equal(450, log.DurationMs);
                Assert.Equal("Error", log.Status);
                Assert.Equal("{\"req\": 1}", log.InputJson);
                Assert.Null(log.OutputJson);
                Assert.Null(log.ToolCallsJson);
                Assert.Equal("Failed to parse API response", log.ErrorMessage);
                Assert.True(log.CreatedAt <= DateTime.UtcNow);
                Assert.True(log.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
            }
        }
        finally
        {
            // 4. Cleanup
            using (var context = new AppDbContext(options))
            {
                await context.Database.EnsureDeletedAsync();
            }
        }
    }
}
