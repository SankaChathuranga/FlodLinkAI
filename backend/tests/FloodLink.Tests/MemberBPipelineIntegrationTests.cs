using FloodLink.Agents.Matching;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;

// T-08: Member B pipeline integration through WorkflowOrchestrator.
// REAL:   MatchingAgent (Member B), WorkflowOrchestrator, WorkflowEngine, AppDbContext on EF InMemory.
// MOCKED: Triage (Member A), Routing (Member C) and Validation (Member D) invokers return fixed results;
//         the AgentExecutionLogger and WorkflowRunRepository are in-memory fakes.
// End-to-end HTTP coverage with the real Triage agent is in MatchingAdvanceApiTests / AllocationsApiTests.
public class MemberBPipelineIntegrationTests
{
    private sealed class Repository(WorkflowRun run) : IWorkflowRunRepository
    {
        public Task<WorkflowRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<WorkflowRun?>(id == run.Id ? run : null);

        public Task AddAsync(WorkflowRun workflowRun, CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Logger : IAgentExecutionLogger
    {
        public Task LogExecutionAsync(Guid workflowRunId, string agentName, long durationMs, bool success,
            bool isRetry = false, string? inputJson = null, string? outputJson = null,
            string? toolCallsJson = null, string? errorMessage = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class TriageInvoker(Guid workflowRunId) : ITriageAgentInvoker
    {
        public Task<AgentResult<TriagePlan>> ExecuteAsync(Guid ignored, CancellationToken ct = default)
            => Task.FromResult(AgentResult<TriagePlan>.Ok(new TriagePlan
            {
                WorkflowRunId = workflowRunId,
                PriorityItems = [new TriagePriorityItem
                {
                    ReportId = 1,
                    ShelterId = 1,
                    NeedType = "Water",
                    Quantity = 20,
                    PriorityScore = 90,
                    Justification = "High priority"
                }]
            }));
    }

    private sealed class MatchingInvoker(MatchingAgent agent) : IMatchingAgentInvoker
    {
        public Task<AgentResult<AllocationProposal>> ExecuteAsync(TriagePlan plan, CancellationToken ct = default)
            => agent.ExecuteAsync(plan, ct);
    }

    private sealed class RoutingInvoker(Guid workflowRunId) : IRoutingAgentInvoker
    {
        public Task<AgentResult<Route>> ExecuteAsync(AllocationProposal proposal, CancellationToken ct = default)
            => Task.FromResult(AgentResult<Route>.Ok(new Route
            {
                WorkflowRunId = workflowRunId,
                AllocationProposalId = proposal.AllocationProposalId,
                DistanceKm = 10,
                EtaMinutes = 20,
                Polyline = "test"
            }));
    }

    private sealed class ValidationInvoker(Guid workflowRunId) : IValidationAgentInvoker
    {
        public Task<AgentResult<ValidationResults>> ExecuteAsync(Route route, CancellationToken ct = default)
            => Task.FromResult(AgentResult<ValidationResults>.Ok(new ValidationResults
            {
                WorkflowRunId = workflowRunId,
                OverallPassed = true,
                Checks = []
            }));
    }

    [Fact]
    public async Task RealMatchingAgent_AllowsPipelineToReachPendingApproval()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        context.Depots.Add(new Depot { Id = 1, Name = "Test Depot", Latitude = 6.9, Longitude = 79.8 });
        context.InventoryItems.Add(new InventoryItem
        {
            Id = 1,
            DepotId = 1,
            ItemName = "Water",
            Unit = "bottles",
            QuantityAvailable = 50
        });
        await context.SaveChangesAsync();

        var run = new WorkflowRun { Id = Guid.NewGuid(), Objective = "Test workflow" };
        var orchestrator = new WorkflowOrchestrator(
            new TriageInvoker(run.Id),
            new MatchingInvoker(new MatchingAgent(context)),
            new RoutingInvoker(run.Id),
            new ValidationInvoker(run.Id),
            new Logger(),
            new Repository(run));

        await orchestrator.AdvanceAsync(run.Id); // Triage → Matching
        await orchestrator.AdvanceAsync(run.Id); // Matching → Routing (real MatchingAgent)
        await orchestrator.AdvanceAsync(run.Id); // Routing → Validating
        await orchestrator.AdvanceAsync(run.Id); // Validating → PendingApproval

        Assert.Equal(WorkflowState.PendingApproval, run.CurrentState);
        Assert.Single(context.AllocationProposals);
    }

    [Fact]
    public async Task RevisionRequested_RerunsRealMatching_AgainstNewStock_WithoutDuplicateRows()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new AppDbContext(options);
        context.Depots.Add(new Depot { Id = 1, Name = "Test Depot", Latitude = 6.9, Longitude = 79.8 });
        context.InventoryItems.Add(new InventoryItem
        {
            Id = 1,
            DepotId = 1,
            ItemName = "Water",
            Unit = "bottles",
            QuantityAvailable = 50
        });
        await context.SaveChangesAsync();

        var run = new WorkflowRun { Id = Guid.NewGuid(), Objective = "Revision workflow" };
        var orchestrator = new WorkflowOrchestrator(
            new TriageInvoker(run.Id),
            new MatchingInvoker(new MatchingAgent(context)),
            new RoutingInvoker(run.Id),
            new ValidationInvoker(run.Id),
            new Logger(),
            new Repository(run));

        for (var step = 0; step < 4; step++)
            await orchestrator.AdvanceAsync(run.Id); // Triage → … → PendingApproval
        var firstRowId = Assert.Single(context.AllocationProposals).Id;

        // Stock drops below the 20 needed before the coordinator asks for a revision.
        var water = await context.InventoryItems.SingleAsync(item => item.Id == 1);
        water.QuantityAvailable = 15;
        await context.SaveChangesAsync();

        await orchestrator.ApplyCoordinatorDecisionAsync(run.Id, WorkflowState.RevisionRequested);
        Assert.Equal(WorkflowState.Matching, run.CurrentState);
        await orchestrator.AdvanceAsync(run.Id); // Matching runs again → Routing
        Assert.Equal(WorkflowState.Routing, run.CurrentState);

        var row = Assert.Single(context.AllocationProposals);
        Assert.NotEqual(firstRowId, row.Id);
        Assert.Equal(15, row.Quantity);

        using var plan = System.Text.Json.JsonDocument.Parse(run.PlanJson!);
        var proposal = System.Text.Json.JsonSerializer.Deserialize<AllocationProposal>(
            plan.RootElement.GetProperty("allocationProposal").GetRawText())!;
        Assert.Equal(row.Id, proposal.AllocationProposalId);
        Assert.Equal(15, Assert.Single(proposal.Allocations).Quantity);
        Assert.Contains("15 of 20", Assert.Single(proposal.Unfulfillable).Reason);
        Assert.Equal(15, (await context.InventoryItems.AsNoTracking().SingleAsync()).QuantityAvailable);
    }
}
