using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using AllocationProposal = FloodLink.Contracts.AllocationProposal;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Phase 4, Task 17 — integration test chaining all four orchestration steps
/// from Triage to PendingApproval using fake in-memory agents and a fake repository.
/// No real DB or external API required.
/// </summary>
public class WorkflowOrchestratorTests
{
    // ── Fakes ──────────────────────────────────────────────────────────────────

    private sealed class FakeRepository : IWorkflowRunRepository
    {
        public WorkflowRun Run { get; }
        public int SaveCount { get; private set; }

        public FakeRepository(WorkflowRun run) => Run = run;

        public Task<WorkflowRun?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<WorkflowRun?>(Run.Id == id ? Run : null);

        public Task AddAsync(WorkflowRun run, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task SaveAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLogger : IAgentExecutionLogger
    {
        public List<(string AgentName, bool Success, string? ErrorMessage)> Entries { get; } = new();

        public Task LogExecutionAsync(Guid workflowRunId, string agentName, long durationMs,
            bool success, bool isRetry = false, string? inputJson = null, string? outputJson = null,
            string? toolCallsJson = null, string? errorMessage = null, CancellationToken cancellationToken = default)
        {
            Entries.Add((agentName, success, errorMessage));
            return Task.CompletedTask;
        }
    }

    private static readonly Guid RunId = Guid.NewGuid();

    private static readonly TriagePlan FakeTriagePlan = new()
    {
        WorkflowRunId = RunId,
        PriorityItems = [new() { ReportId = 1, ShelterId = 1, NeedType = "Water", Quantity = 100, PriorityScore = 90, Justification = "High" }]
    };

    private static readonly AllocationProposal FakeProposal = new()
    {
        WorkflowRunId = RunId,
        Allocations = [new() { DepotId = 1, ShelterId = 1, ItemName = "Water", Quantity = 100 }],
        Unfulfillable = []
    };

    private static readonly Route FakeRoute = new()
    {
        WorkflowRunId = RunId,
        AllocationProposalId = 1,
        DistanceKm = 12.5,
        EtaMinutes = 25,
        Polyline = "abc123"
    };

    private static readonly ValidationResults FakeValidation = new()
    {
        WorkflowRunId = RunId,
        OverallPassed = true,
        Checks = [new() { CheckName = "StockAvailability", Passed = true }]
    };

    private static WorkflowOrchestrator BuildOrchestrator(
        WorkflowRun run,
        FakeLogger logger,
        ITriageAgentInvoker? triage = null,
        IMatchingAgentInvoker? matching = null,
        IRoutingAgentInvoker? routing = null,
        IValidationAgentInvoker? validation = null)
        => new(
            triage ?? new SucceedingTriageAgent(),
            matching ?? new SucceedingMatchingAgent(),
            routing ?? new SucceedingRoutingAgent(),
            validation ?? new SucceedingValidationAgent(),
            logger,
            new FakeRepository(run));

    // ── Succeeding fake agents ─────────────────────────────────────────────────

    private sealed class SucceedingTriageAgent : ITriageAgentInvoker
    {
        public Task<AgentResult<TriagePlan>> ExecuteAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(AgentResult<TriagePlan>.Ok(FakeTriagePlan));
    }

    private sealed class SucceedingMatchingAgent : IMatchingAgentInvoker
    {
        public Task<AgentResult<AllocationProposal>> ExecuteAsync(TriagePlan plan, CancellationToken ct = default)
            => Task.FromResult(AgentResult<AllocationProposal>.Ok(FakeProposal));
    }

    private sealed class SucceedingRoutingAgent : IRoutingAgentInvoker
    {
        public Task<AgentResult<Route>> ExecuteAsync(AllocationProposal proposal, CancellationToken ct = default)
            => Task.FromResult(AgentResult<Route>.Ok(FakeRoute));
    }

    private sealed class SucceedingValidationAgent : IValidationAgentInvoker
    {
        public Task<AgentResult<ValidationResults>> ExecuteAsync(Route route, CancellationToken ct = default)
            => Task.FromResult(AgentResult<ValidationResults>.Ok(FakeValidation));
    }

    // ── Failing fake agents ────────────────────────────────────────────────────

    private sealed class FailingTriageAgent : ITriageAgentInvoker
    {
        public Task<AgentResult<TriagePlan>> ExecuteAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(AgentResult<TriagePlan>.Fail("TRIAGE_ERROR", "Triage failed"));
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_AdvancesFromTriageToPendingApproval_WithFourLogEntries()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger);

        // Four AdvanceAsync calls advance through all four agent stages.
        await sut.AdvanceAsync(RunId);
        Assert.Equal(WorkflowState.Matching, run.CurrentState);

        await sut.AdvanceAsync(RunId);
        Assert.Equal(WorkflowState.Routing, run.CurrentState);

        await sut.AdvanceAsync(RunId);
        Assert.Equal(WorkflowState.Validating, run.CurrentState);

        await sut.AdvanceAsync(RunId);
        Assert.Equal(WorkflowState.PendingApproval, run.CurrentState);

        // One log entry per step, all successful.
        Assert.Equal(4, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.True(e.Success));
        Assert.Equal("TriageAgent", logger.Entries[0].AgentName);
        Assert.Equal("MatchingAgent", logger.Entries[1].AgentName);
        Assert.Equal("RoutingAgent", logger.Entries[2].AgentName);
        Assert.Equal("ValidationAgent", logger.Entries[3].AgentName);
    }

    [Fact]
    public async Task TriageFailure_TransitionsToFailed_LogsError()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger, triage: new FailingTriageAgent());

        await sut.AdvanceAsync(RunId);

        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.Equal(WorkflowState.Triage, run.FailedAtState);
        Assert.Single(logger.Entries);
        Assert.False(logger.Entries[0].Success);
        Assert.Equal("Triage failed", logger.Entries[0].ErrorMessage);
    }

    [Fact]
    public async Task AdvanceAsync_OnTerminalState_Throws()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        run.CurrentState = WorkflowState.PendingApproval;
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.AdvanceAsync(RunId));
        Assert.Empty(logger.Entries); // nothing was logged
    }

    [Fact]
    public async Task ValidationFail_TransitionsToFailed_NotPendingApproval()
    {
        var failedValidation = new ValidationResults
        {
            WorkflowRunId = RunId,
            OverallPassed = false,
            Checks = [new() { CheckName = "StockAvailability", Passed = false, ViolationDetail = "Insufficient stock" }]
        };

        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger,
            validation: new AlwaysPassValidationAgent(failedValidation));

        // Advance to Validating state first
        await sut.AdvanceAsync(RunId); // Triage → Matching
        await sut.AdvanceAsync(RunId); // Matching → Routing
        await sut.AdvanceAsync(RunId); // Routing → Validating
        await sut.AdvanceAsync(RunId); // Validating → Failed (because OverallPassed = false)

        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.Equal(WorkflowState.Validating, run.FailedAtState);
    }

    [Fact]
    public async Task ApplyCoordinatorDecision_WhenNotInPendingApproval_Throws()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test", CurrentState = WorkflowState.Triage };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ApplyCoordinatorDecisionAsync(RunId, WorkflowState.Approved));
        
        Assert.Contains("Cannot apply Approved to a run in state Triage", ex.Message);
    }

    [Fact]
    public async Task ApplyCoordinatorDecision_Approved_SetsStateToApproved()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test", CurrentState = WorkflowState.PendingApproval };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger);

        await sut.ApplyCoordinatorDecisionAsync(RunId, WorkflowState.Approved);

        Assert.Equal(WorkflowState.Approved, run.CurrentState);
    }

    [Fact]
    public async Task ApplyCoordinatorDecision_RevisionRequested_AutoTransitionsToMatching()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test", CurrentState = WorkflowState.PendingApproval };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger);

        await sut.ApplyCoordinatorDecisionAsync(RunId, WorkflowState.RevisionRequested);

        // Orchestrator automatically re-queues to Matching per transition table
        Assert.Equal(WorkflowState.Matching, run.CurrentState);
    }

    private sealed class AlwaysPassValidationAgent : IValidationAgentInvoker

    {
        private readonly ValidationResults _results;
        public AlwaysPassValidationAgent(ValidationResults results) => _results = results;

        public Task<AgentResult<ValidationResults>> ExecuteAsync(Route route, CancellationToken ct = default)
            => Task.FromResult(AgentResult<ValidationResults>.Ok(_results));
    }
}
