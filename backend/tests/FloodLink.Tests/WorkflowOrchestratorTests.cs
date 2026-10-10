using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
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
        public List<(string AgentName, bool Success, string? ErrorMessage, bool IsRetry, string? ToolCallsJson)> Entries { get; } = new();

        public Task LogExecutionAsync(Guid workflowRunId, string agentName, long durationMs,
            bool success, bool isRetry = false, string? inputJson = null, string? outputJson = null,
            string? toolCallsJson = null, string? errorMessage = null, CancellationToken cancellationToken = default)
        {
            Entries.Add((agentName, success, errorMessage, isRetry, toolCallsJson));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStock(bool succeeds = true) : IStockReservationService
    {
        public int Reservations { get; private set; }

        public Task<StockOperationResult> ReserveForRunAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
        {
            Reservations++;
            return Task.FromResult(succeeds
                ? StockOperationResult.Ok()
                : StockOperationResult.Fail("INSUFFICIENT_STOCK", "Depot 1 has 0 Water free; the plan needs 100."));
        }

        public Task<StockOperationResult> ReleaseForRunAsync(Guid workflowRunId, CancellationToken cancellationToken = default)
            => Task.FromResult(StockOperationResult.Ok());
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
        AllocationProposalId = 1,
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
        IValidationAgentInvoker? validation = null,
        IStockReservationService? stock = null)
        => new(
            triage ?? new SucceedingTriageAgent(),
            matching ?? new SucceedingMatchingAgent(),
            routing ?? new SucceedingRoutingAgent(),
            validation ?? new SucceedingValidationAgent(),
            stock ?? new FakeStock(),
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

    private sealed class FailingMatchingAgent : IMatchingAgentInvoker
    {
        public Task<AgentResult<AllocationProposal>> ExecuteAsync(TriagePlan plan, CancellationToken ct = default)
            => Task.FromResult(AgentResult<AllocationProposal>.Fail("NO_STOCK_AVAILABLE", "No matching stock"));
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullPipeline_AdvancesFromTriageToPendingApproval_WithFiveLogEntries()
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

        // One log entry per agent plus the stock reservation, all successful.
        Assert.Equal(5, logger.Entries.Count);
        Assert.All(logger.Entries, e => Assert.True(e.Success));
        Assert.Equal("TriageAgent", logger.Entries[0].AgentName);
        Assert.Equal("MatchingAgent", logger.Entries[1].AgentName);
        Assert.Equal("RoutingAgent", logger.Entries[2].AgentName);
        Assert.Equal("ValidationAgent", logger.Entries[3].AgentName);
        Assert.Equal("StockReservation", logger.Entries[4].AgentName);
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
        Assert.Equal("TRIAGE_ERROR: Triage failed", run.FailureReason);
    }

    [Fact]
    public async Task MatchingFailure_TransitionsToFailed_AndRecordsMatchingAsFailedStage()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger, matching: new FailingMatchingAgent());

        await sut.AdvanceAsync(RunId); // Triage → Matching
        await sut.AdvanceAsync(RunId); // Matching → Failed

        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.Equal(WorkflowState.Matching, run.FailedAtState);
        Assert.Equal("MatchingAgent", logger.Entries.Last().AgentName);
        Assert.False(logger.Entries.Last().Success);
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
        Assert.Equal("VALIDATION_FAILED: StockAvailability: Insufficient stock", run.FailureReason);
    }

    [Fact]
    public async Task RunToCompletion_StopsAtPendingApproval_AndReservesStock()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var stock = new FakeStock();
        var sut = BuildOrchestrator(run, new FakeLogger(), stock: stock);

        var result = await sut.RunToCompletionAsync(RunId);

        Assert.Equal(WorkflowState.PendingApproval, result.CurrentState);
        Assert.Equal(1, stock.Reservations);
        Assert.Null(run.FailureReason);
    }

    [Fact]
    public async Task RunToCompletion_OnWaitingRun_DoesNothing()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test", CurrentState = WorkflowState.PendingApproval };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger);

        var result = await sut.RunToCompletionAsync(RunId);

        Assert.Equal(WorkflowState.PendingApproval, result.CurrentState);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task ReservationFailure_FailsRunAtValidating_WithReason()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger, stock: new FakeStock(succeeds: false));

        await sut.RunToCompletionAsync(RunId);

        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.Equal(WorkflowState.Validating, run.FailedAtState);
        Assert.StartsWith("INSUFFICIENT_STOCK", run.FailureReason);
        Assert.Equal("StockReservation", logger.Entries.Last().AgentName);
        Assert.False(logger.Entries.Last().Success);
    }

    [Fact]
    public async Task MissingPlanSection_FailsSafely_InsteadOfThrowing()
    {
        // A run in Matching with no triage plan in PlanJson: the step fails, the run is Failed.
        var run = new WorkflowRun { Id = RunId, Objective = "Test", CurrentState = WorkflowState.Matching };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger);

        await sut.AdvanceAsync(RunId);

        Assert.Equal(WorkflowState.Failed, run.CurrentState);
        Assert.StartsWith("UNEXPECTED_ERROR", run.FailureReason);
        Assert.False(logger.Entries.Single().Success);
    }

    [Fact]
    public async Task AgentToolCalls_AreWrittenToTheExecutionLog()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger, triage: new ToolCallingTriageAgent());

        await sut.AdvanceAsync(RunId);

        Assert.Contains("db.reports.read", logger.Entries.Single().ToolCallsJson);
    }

    [Fact]
    public async Task Revision_RequeuesToMatching_AndReturnsToPendingApproval()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var logger = new FakeLogger();
        var sut = BuildOrchestrator(run, logger);
        await sut.RunToCompletionAsync(RunId);

        // Coordinator requested a revision (the dispatch service sets this state).
        Assert.True(WorkflowEngine.TryTransition(run, WorkflowState.RevisionRequested));
        logger.Entries.Clear();

        await sut.RunToCompletionAsync(RunId);

        Assert.Equal(WorkflowState.PendingApproval, run.CurrentState);
        Assert.Equal(
            ["Orchestrator", "MatchingAgent", "RoutingAgent", "ValidationAgent", "StockReservation"],
            logger.Entries.Select(e => e.AgentName).ToArray());
    }

    [Fact]
    public async Task PrepareRetry_ReentersFailedStage_AndLogsTheRetry()
    {
        var run = new WorkflowRun { Id = RunId, Objective = "Test" };
        var logger = new FakeLogger();
        var matching = new FlakyMatchingAgent();
        var sut = BuildOrchestrator(run, logger, matching: matching);
        await sut.RunToCompletionAsync(RunId);
        Assert.Equal(WorkflowState.Failed, run.CurrentState);

        var outcome = await sut.PrepareRetryAsync(RunId, maxRetries: 3);

        Assert.Equal(RetryOutcome.Ready, outcome);
        Assert.Equal(WorkflowState.Matching, run.CurrentState);
        Assert.Null(run.FailedAtState);
        Assert.Null(run.FailureReason);
        Assert.Equal(1, run.RetryCount);

        await sut.RunToCompletionAsync(RunId);

        Assert.Equal(WorkflowState.PendingApproval, run.CurrentState);
        var retried = logger.Entries.Where(e => e.AgentName == "MatchingAgent").ToList();
        Assert.False(retried[0].IsRetry);
        Assert.True(retried[1].IsRetry);
        Assert.False(logger.Entries.Single(e => e.AgentName == "RoutingAgent").IsRetry);
    }

    [Fact]
    public async Task PrepareRetry_RefusesWhenLimitReachedOrNotFailed()
    {
        var failed = new WorkflowRun
        {
            Id = RunId, Objective = "Test", CurrentState = WorkflowState.Failed,
            FailedAtState = WorkflowState.Routing, RetryCount = 3
        };
        Assert.Equal(RetryOutcome.LimitReached,
            await BuildOrchestrator(failed, new FakeLogger()).PrepareRetryAsync(RunId, maxRetries: 3));

        var waiting = new WorkflowRun { Id = RunId, Objective = "Test", CurrentState = WorkflowState.PendingApproval };
        Assert.Equal(RetryOutcome.NotFailed,
            await BuildOrchestrator(waiting, new FakeLogger()).PrepareRetryAsync(RunId, maxRetries: 3));
    }

    private sealed class ToolCallingTriageAgent : ITriageAgentInvoker
    {
        public Task<AgentResult<TriagePlan>> ExecuteAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(AgentResult<TriagePlan>.Ok(FakeTriagePlan)
                .WithToolCalls([new ToolCall { Tool = "db.reports.read", Succeeded = true }]));
    }

    // Fails the first time (e.g. stock missing), succeeds on retry.
    private sealed class FlakyMatchingAgent : IMatchingAgentInvoker
    {
        private int _calls;

        public Task<AgentResult<AllocationProposal>> ExecuteAsync(TriagePlan plan, CancellationToken ct = default)
            => Task.FromResult(++_calls == 1
                ? AgentResult<AllocationProposal>.Fail("NO_STOCK_AVAILABLE", "No stock")
                : AgentResult<AllocationProposal>.Ok(FakeProposal));
    }

    private sealed class AlwaysPassValidationAgent : IValidationAgentInvoker

    {
        private readonly ValidationResults _results;
        public AlwaysPassValidationAgent(ValidationResults results) => _results = results;

        public Task<AgentResult<ValidationResults>> ExecuteAsync(Route route, CancellationToken ct = default)
            => Task.FromResult(AgentResult<ValidationResults>.Ok(_results));
    }
}
