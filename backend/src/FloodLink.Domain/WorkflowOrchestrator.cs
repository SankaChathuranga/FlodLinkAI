using System.Diagnostics;
using System.Text.Json;
using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Domain.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FloodLink.Domain;

/// <summary>
/// Sequences all four agents for a single <see cref="WorkflowRun"/>.
/// <see cref="AdvanceAsync"/> runs one step; <see cref="RunToCompletionAsync"/> keeps going
/// until the run needs a human (PendingApproval) or has failed safely.
/// </summary>
/// <remarks>
/// Lives in FloodLink.Domain (business logic layer). Depends only on the four thin invoker
/// interfaces, the stock reservation service, the execution logger and the run repository —
/// no direct knowledge of any agent's internals. Coordinator decisions (approve, reject,
/// revise) are handled by the dispatch service, not here.
/// </remarks>
public sealed class WorkflowOrchestrator
{
    /// <summary>Upper bound on steps in one <see cref="RunToCompletionAsync"/> call, as a loop guard.</summary>
    public const int MaxStepsPerRun = 12;

    private const int MaxFailureReasonLength = 2000;

    private readonly ITriageAgentInvoker _triage;
    private readonly IMatchingAgentInvoker _matching;
    private readonly IRoutingAgentInvoker _routing;
    private readonly IValidationAgentInvoker _validation;
    private readonly IStockReservationService _stock;
    private readonly IAgentExecutionLogger _logger;
    private readonly IWorkflowRunRepository _runs;
    private readonly IPlannerInvoker? _planner;
    private readonly ILogger _log;

    public WorkflowOrchestrator(
        ITriageAgentInvoker triage,
        IMatchingAgentInvoker matching,
        IRoutingAgentInvoker routing,
        IValidationAgentInvoker validation,
        IStockReservationService stock,
        IAgentExecutionLogger logger,
        IWorkflowRunRepository runs,
        IPlannerInvoker? planner = null,
        ILogger<WorkflowOrchestrator>? log = null)
    {
        _log = log ?? NullLogger<WorkflowOrchestrator>.Instance;
        _triage = triage;
        _matching = matching;
        _routing = routing;
        _validation = validation;
        _stock = stock;
        _logger = logger;
        _runs = runs;
        _planner = planner;
    }

    /// <summary>
    /// Advances the run step by step while it is in an automatic state
    /// (see <see cref="WorkflowEngine.AutomaticStates"/>). Returns as soon as the run reaches
    /// PendingApproval, a terminal state, or Failed. A run that is already waiting or finished
    /// is returned unchanged.
    /// </summary>
    /// <exception cref="InvalidOperationException">The run does not exist.</exception>
    public async Task<WorkflowRun> RunToCompletionAsync(Guid workflowRunId, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(workflowRunId, ct)
            ?? throw new InvalidOperationException($"WorkflowRun {workflowRunId} not found.");

        for (var step = 0; step < MaxStepsPerRun && WorkflowEngine.AutomaticStates.Contains(run.CurrentState); step++)
            run = await AdvanceAsync(workflowRunId, ct);

        return run;
    }

    /// <summary>
    /// Advances <paramref name="workflowRunId"/> by one step: calls the agent appropriate for
    /// the run's current state, logs the invocation, and transitions the state machine
    /// (to Failed with a recorded reason on error).
    /// </summary>
    /// <returns>The updated <see cref="WorkflowRun"/> after the step.</returns>
    /// <exception cref="InvalidOperationException">
    /// The run does not exist, or it is in a state where no automatic step applies
    /// (PendingApproval, Approved, Rejected, Failed).
    /// </exception>
    public async Task<WorkflowRun> AdvanceAsync(Guid workflowRunId, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(workflowRunId, ct)
            ?? throw new InvalidOperationException($"WorkflowRun {workflowRunId} not found.");

        switch (run.CurrentState)
        {
            // The Planner runs once per run, as its own logged step, before Triage. It doesn't
            // change state: the next step is Triage.
            case WorkflowState.Triage when _planner is not null
                                           && WorkflowPlan.TryRead<ExecutionPlan>(run.PlanJson, WorkflowPlan.ExecutionPlanKey) is null:
                await StepAsync(run, "PlannerAgent",
                    () => _planner.ExecuteAsync(workflowRunId, ct),
                    plan =>
                    {
                        run.PlanJson = WorkflowPlan.Merge(run.PlanJson, WorkflowPlan.ExecutionPlanKey, plan);
                        return Task.CompletedTask;
                    }, ct, consumesRetry: false);
                break;

            case WorkflowState.Triage:
                await StepAsync(run, "TriageAgent",
                    () => _triage.ExecuteAsync(workflowRunId, ct),
                    plan =>
                    {
                        run.PlanJson = WorkflowPlan.Merge(run.PlanJson, WorkflowPlan.TriagePlanKey, plan);
                        WorkflowEngine.TryTransition(run, WorkflowState.Matching);
                        return Task.CompletedTask;
                    }, ct);
                break;

            case WorkflowState.Matching:
                await StepAsync(run, "MatchingAgent",
                    () => _matching.ExecuteAsync(
                        WorkflowPlan.Read<TriagePlan>(run.PlanJson, WorkflowPlan.TriagePlanKey),
                        RevisionNotes(run.PlanJson), ct),
                    proposal =>
                    {
                        run.PlanJson = WorkflowPlan.Merge(run.PlanJson, WorkflowPlan.AllocationProposalKey, proposal);
                        WorkflowEngine.TryTransition(run, WorkflowState.Routing);
                        return Task.CompletedTask;
                    }, ct);
                break;

            case WorkflowState.Routing:
                await StepAsync(run, "RoutingAgent",
                    () => _routing.ExecuteAsync(
                        WorkflowPlan.Read<AllocationProposal>(run.PlanJson, WorkflowPlan.AllocationProposalKey), ct),
                    route =>
                    {
                        run.PlanJson = WorkflowPlan.Merge(run.PlanJson, WorkflowPlan.RouteKey, route);
                        WorkflowEngine.TryTransition(run, WorkflowState.Validating);
                        return Task.CompletedTask;
                    }, ct);
                break;

            case WorkflowState.Validating:
                await StepAsync(run, "ValidationAgent",
                    () => _validation.ExecuteAsync(
                        WorkflowPlan.Read<Route>(run.PlanJson, WorkflowPlan.RouteKey), ct),
                    async results =>
                    {
                        run.PlanJson = WorkflowPlan.Merge(run.PlanJson, WorkflowPlan.ValidationResultsKey, results);
                        if (!results.OverallPassed)
                        {
                            Fail(run, "VALIDATION_FAILED", DescribeFailedChecks(results));
                            return;
                        }

                        await ReserveStockAsync(run, ct);
                    }, ct);
                break;

            case WorkflowState.RevisionRequested:
                await RequeueForRevisionAsync(run, ct);
                break;

            default:
                throw new InvalidOperationException(
                    $"WorkflowRun {workflowRunId} is in state {run.CurrentState} — no agent step to advance.");
        }

        await _runs.SaveAsync(ct);
        return run;
    }

    /// <summary>
    /// Prepares a failed run for a coordinator retry: moves it back into the agent stage that
    /// failed, increments <see cref="WorkflowRun.RetryCount"/> and marks the next execution of
    /// that stage as a retry. The caller queues the run afterwards.
    /// </summary>
    public async Task<RetryOutcome> PrepareRetryAsync(Guid workflowRunId, int maxRetries, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(workflowRunId, ct);
        if (run is null)
            return RetryOutcome.NotFound;
        if (run.CurrentState != WorkflowState.Failed)
            return RetryOutcome.NotFailed;
        if (run.RetryCount >= maxRetries)
            return RetryOutcome.LimitReached;

        var stage = run.FailedAtState;
        if (stage is null || !WorkflowEngine.TryTransition(run, stage.Value))
            return RetryOutcome.NotRetryable;

        run.RetryCount++;
        run.RetryOfState = stage;
        await _runs.SaveAsync(ct);
        return RetryOutcome.Ready;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task StepAsync<T>(
        WorkflowRun run,
        string agentName,
        Func<Task<AgentResult<T>>> invoke,
        Func<T, Task> onSuccess,
        CancellationToken ct,
        bool consumesRetry = true)
    {
        var isRetry = consumesRetry && run.RetryOfState == run.CurrentState;
        if (consumesRetry)
            run.RetryOfState = null;
        var inputJson = run.PlanJson; // current plan is the agent's effective input snapshot
        var sw = Stopwatch.StartNew();

        AgentResult<T> result;
        try
        {
            result = await invoke();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Unexpected exception — not an anticipated agent failure. Convert to Failed.
            sw.Stop();
            await _logger.LogExecutionAsync(run.Id, agentName, sw.ElapsedMilliseconds, success: false,
                isRetry: isRetry, inputJson: inputJson, errorMessage: ex.Message, cancellationToken: ct);
            _log.LogError(ex, "Workflow {WorkflowRunId} agent {Agent} threw after {DurationMs} ms (retry {IsRetry})",
                run.Id, agentName, sw.ElapsedMilliseconds, isRetry);
            Fail(run, "UNEXPECTED_ERROR", ex.Message);
            return;
        }

        sw.Stop();
        await _logger.LogExecutionAsync(
            run.Id,
            agentName,
            sw.ElapsedMilliseconds,
            result.Success,
            isRetry,
            inputJson,
            result.Data is not null ? JsonSerializer.Serialize(result.Data) : null,
            SerializeToolCalls(result.ToolCalls),
            result.ErrorMessage,
            ct);

        LogStep(run, agentName, result.Success, sw.ElapsedMilliseconds, isRetry, result.ToolCalls, result.ErrorCode);

        if (result.Success && result.Data is not null)
            await onSuccess(result.Data);
        else
            Fail(run, result.ErrorCode ?? "AGENT_FAILED", result.ErrorMessage ?? $"{agentName} failed.");
    }

    // A validated plan holds its stock until the coordinator decides, so another plan
    // can't promise the same supplies in the meantime.
    private async Task ReserveStockAsync(WorkflowRun run, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var reservation = await _stock.ReserveForRunAsync(run.Id, ct);
        sw.Stop();

        await _logger.LogExecutionAsync(run.Id, "StockReservation", sw.ElapsedMilliseconds,
            reservation.Succeeded,
            outputJson: JsonSerializer.Serialize(new { reserved = reservation.Succeeded }),
            toolCallsJson: SerializeToolCalls(reservation.ToolCalls),
            errorMessage: reservation.ErrorMessage,
            cancellationToken: ct);

        LogStep(run, "StockReservation", reservation.Succeeded, sw.ElapsedMilliseconds, isRetry: false,
            reservation.ToolCalls, reservation.ErrorCode);

        if (reservation.Succeeded)
            WorkflowEngine.TryTransition(run, WorkflowState.PendingApproval);
        else
            Fail(run, reservation.ErrorCode ?? "RESERVATION_FAILED", reservation.ErrorMessage ?? "Stock could not be reserved.");
    }

    // The coordinator asked for changes: drop the stale route and checks, then re-run Matching.
    // Stock was already released when the revision was requested.
    private async Task RequeueForRevisionAsync(WorkflowRun run, CancellationToken ct)
    {
        run.PlanJson = WorkflowPlan.Remove(run.PlanJson, WorkflowPlan.RouteKey);
        run.PlanJson = WorkflowPlan.Remove(run.PlanJson, WorkflowPlan.ValidationResultsKey);
        WorkflowEngine.TryTransition(run, WorkflowState.Matching);

        await _logger.LogExecutionAsync(run.Id, "Orchestrator", durationMs: 0, success: true,
            outputJson: JsonSerializer.Serialize(new { action = "RevisionRequeued", nextState = run.CurrentState.ToString() }),
            cancellationToken: ct);
    }

    // Coordinator notes from the latest request-revision, passed to Matching as untrusted data.
    private static string? RevisionNotes(string? planJson)
    {
        if (string.IsNullOrWhiteSpace(planJson))
            return null;
        using var doc = JsonDocument.Parse(planJson);
        return doc.RootElement.TryGetProperty(WorkflowPlan.RevisionKey, out var revision)
               && revision.ValueKind == JsonValueKind.Object
               && revision.TryGetProperty("Notes", out var notes)
               && notes.ValueKind == JsonValueKind.String
            ? notes.GetString()
            : null;
    }

    // One structured line per step: which agent, how long, how many tool and LLM calls, and
    // whether an LLM agent fell back to its rule-based logic.
    private void LogStep(WorkflowRun run, string agentName, bool succeeded, long durationMs, bool isRetry,
        IReadOnlyList<ToolCall>? toolCalls, string? errorCode)
    {
        var llmCalls = toolCalls?.Where(t => t.Tool == LlmToolCall.ToolName).ToList() ?? [];
        var usedFallback = llmCalls.Any(t => t.Output?.GetValueOrDefault("usedFallback") is true);
        _log.Log(succeeded ? LogLevel.Information : LogLevel.Warning,
            "Workflow {WorkflowRunId} agent {Agent} {Outcome} in {DurationMs} ms (state {State}, retry {IsRetry}, tool calls {ToolCallCount}, LLM calls {LlmCallCount}, used fallback {UsedFallback}, error {ErrorCode})",
            run.Id, agentName, succeeded ? "succeeded" : "failed", durationMs, run.CurrentState, isRetry,
            toolCalls?.Count ?? 0, llmCalls.Count, usedFallback, errorCode);
    }

    private void Fail(WorkflowRun run, string code, string message)
    {
        if (!WorkflowEngine.TryTransition(run, WorkflowState.Failed))
            return;

        _log.LogWarning("Workflow {WorkflowRunId} failed at {State}: {Code}", run.Id, run.FailedAtState, code);

        var reason = $"{code}: {message}";
        run.FailureReason = reason.Length > MaxFailureReasonLength ? reason[..MaxFailureReasonLength] : reason;
    }

    private static string DescribeFailedChecks(ValidationResults results)
    {
        var failed = results.Checks
            .Where(check => !check.Passed)
            .Select(check => $"{check.CheckName}: {check.ViolationDetail}")
            .ToList();
        return failed.Count == 0 ? "Validation did not pass." : string.Join("; ", failed);
    }

    private static string? SerializeToolCalls(IReadOnlyList<ToolCall>? toolCalls)
        => toolCalls is { Count: > 0 } ? JsonSerializer.Serialize(toolCalls) : null;
}

/// <summary>Result of <see cref="WorkflowOrchestrator.PrepareRetryAsync"/>.</summary>
public enum RetryOutcome
{
    /// <summary>The run was moved back into its failed stage and can be queued.</summary>
    Ready,

    /// <summary>No run has that id.</summary>
    NotFound,

    /// <summary>Only failed runs can be retried.</summary>
    NotFailed,

    /// <summary>The run has used all its retries.</summary>
    LimitReached,

    /// <summary>The run did not fail in an agent stage, so there is nothing to re-run.</summary>
    NotRetryable
}
