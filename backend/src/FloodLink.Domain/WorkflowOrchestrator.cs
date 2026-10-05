using System.Diagnostics;
using System.Text.Json;
using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;

namespace FloodLink.Domain;

/// <summary>
/// Sequences all four agents for a single <see cref="WorkflowRun"/>.
/// Entry point: <see cref="AdvanceAsync"/> — given a run in its current state,
/// calls the next agent, logs the result, and transitions the state machine.
/// </summary>
/// <remarks>
/// Lives in FloodLink.Domain (business logic layer). Depends only on
/// IAgentExecutionLogger, WorkflowEngine, and the four thin invoker interfaces —
/// no direct knowledge of any agent's internals.
/// </remarks>
public sealed class WorkflowOrchestrator
{
    private readonly ITriageAgentInvoker _triage;
    private readonly IMatchingAgentInvoker _matching;
    private readonly IRoutingAgentInvoker _routing;
    private readonly IValidationAgentInvoker _validation;
    private readonly IAgentExecutionLogger _logger;
    private readonly IWorkflowRunRepository _runs;

    public WorkflowOrchestrator(
        ITriageAgentInvoker triage,
        IMatchingAgentInvoker matching,
        IRoutingAgentInvoker routing,
        IValidationAgentInvoker validation,
        IAgentExecutionLogger logger,
        IWorkflowRunRepository runs)
    {
        _triage = triage;
        _matching = matching;
        _routing = routing;
        _validation = validation;
        _logger = logger;
        _runs = runs;
    }

    /// <summary>
    /// Advances <paramref name="workflowRunId"/> by one step: calls the agent
    /// appropriate for the run's current state, logs the invocation, and
    /// transitions to the next state (or Failed on error).
    /// </summary>
    /// <returns>The updated <see cref="WorkflowRun"/> after the step.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown only if the run does not exist or is already in a terminal state
    /// where no agent step is applicable (Approved, Rejected, Failed, PendingApproval).
    /// </exception>
    public async Task<WorkflowRun> AdvanceAsync(Guid workflowRunId, CancellationToken ct = default)
    {
        var run = await _runs.GetByIdAsync(workflowRunId, ct)
            ?? throw new InvalidOperationException($"WorkflowRun {workflowRunId} not found.");

        switch (run.CurrentState)
        {
            case WorkflowState.Triage:
                await StepAsync(run, "TriageAgent",
                    async () => await _triage.ExecuteAsync(workflowRunId, ct),
                    onSuccess: (plan) =>
                    {
                        run.PlanJson = MergePlan(run.PlanJson, "triagePlan", plan);
                        WorkflowEngine.TryTransition(run, WorkflowState.Matching);
                    }, ct);
                break;

            case WorkflowState.Matching:
                var triagePlan = ExtractPlan<TriagePlan>(run.PlanJson, "triagePlan");
                await StepAsync(run, "MatchingAgent",
                    async () => await _matching.ExecuteAsync(triagePlan, ct),
                    onSuccess: (proposal) =>
                    {
                        run.PlanJson = MergePlan(run.PlanJson, "allocationProposal", proposal);
                        WorkflowEngine.TryTransition(run, WorkflowState.Routing);
                    }, ct);
                break;

            case WorkflowState.Routing:
                var proposal = ExtractPlan<FloodLink.Contracts.AllocationProposal>(run.PlanJson, "allocationProposal");
                await StepAsync(run, "RoutingAgent",
                    async () => await _routing.ExecuteAsync(proposal, ct),
                    onSuccess: (route) =>
                    {
                        run.PlanJson = MergePlan(run.PlanJson, "route", route);
                        WorkflowEngine.TryTransition(run, WorkflowState.Validating);
                    }, ct);
                break;

            case WorkflowState.Validating:
                var route = ExtractPlan<Route>(run.PlanJson, "route");
                await StepAsync(run, "ValidationAgent",
                    async () => await _validation.ExecuteAsync(route, ct),
                    onSuccess: (results) =>
                    {
                        run.PlanJson = MergePlan(run.PlanJson, "validationResults", results);
                        var next = results.OverallPassed
                            ? WorkflowState.PendingApproval
                            : WorkflowState.Failed;
                        WorkflowEngine.TryTransition(run, next);
                    }, ct);
                break;

            default:
                throw new InvalidOperationException(
                    $"WorkflowRun {workflowRunId} is in state {run.CurrentState} — no agent step to advance.");
        }

        await _runs.SaveAsync(ct);
        return run;
    }

    /// <summary>
    /// Applies a coordinator decision (Approve / Reject / RevisionRequested) to a run
    /// that is in <see cref="WorkflowState.PendingApproval"/>. Reuses
    /// <see cref="WorkflowEngine.TryTransition"/> — no transition logic is duplicated here.
    /// </summary>
    /// <remarks>
    /// On <see cref="WorkflowState.RevisionRequested"/> the run is automatically
    /// re-queued to <see cref="WorkflowState.Matching"/> per the transition table.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="decision"/> is not a coordinator-action state.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Run not found, or run is not in PendingApproval.
    /// </exception>
    public async Task<WorkflowRun> ApplyCoordinatorDecisionAsync(
        Guid workflowRunId,
        WorkflowState decision,
        CancellationToken ct = default)
    {
        if (decision is not (WorkflowState.Approved or WorkflowState.Rejected or WorkflowState.RevisionRequested))
            throw new ArgumentException($"{decision} is not a valid coordinator decision.", nameof(decision));

        var run = await _runs.GetByIdAsync(workflowRunId, ct)
            ?? throw new InvalidOperationException($"WorkflowRun {workflowRunId} not found.");

        if (!WorkflowEngine.TryTransition(run, decision))
            throw new InvalidOperationException(
                $"Cannot apply {decision} to a run in state {run.CurrentState}. Run must be in PendingApproval.");

        // Auto re-queue: RevisionRequested → Matching (orchestrator-driven, per transition table).
        if (decision == WorkflowState.RevisionRequested)
            WorkflowEngine.TryTransition(run, WorkflowState.Matching);

        await _runs.SaveAsync(ct);
        return run;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task StepAsync<T>(
        WorkflowRun run,
        string agentName,
        Func<Task<AgentResult<T>>> invoke,
        Action<T> onSuccess,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        AgentResult<T>? result = null;
        string? inputJson = null;

        try
        {
            inputJson = run.PlanJson; // current plan is the agent's effective input snapshot
            result = await invoke();
        }
        catch (Exception ex)
        {
            // Unexpected exception — not an anticipated agent failure. Convert to Failed.
            sw.Stop();
            await _logger.LogExecutionAsync(workflowRunId: run.Id, agentName: agentName,
                durationMs: sw.ElapsedMilliseconds, success: false,
                inputJson: inputJson, errorMessage: ex.Message, cancellationToken: ct);
            WorkflowEngine.TryTransition(run, WorkflowState.Failed);
            return;
        }

        sw.Stop();
        var outputJson = result.Data is not null
            ? JsonSerializer.Serialize(result.Data)
            : null;

        await _logger.LogExecutionAsync(
            workflowRunId: run.Id,
            agentName: agentName,
            durationMs: sw.ElapsedMilliseconds,
            success: result.Success,
            inputJson: inputJson,
            outputJson: outputJson,
            errorMessage: result.ErrorMessage,
            cancellationToken: ct);

        if (result.Success && result.Data is not null)
            onSuccess(result.Data);
        else
            WorkflowEngine.TryTransition(run, WorkflowState.Failed);
    }

    // Merges agent output into the plan JSON blob as a named key.
    private static string MergePlan<T>(string? existing, string key, T value)
    {
        var dict = existing is not null
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existing) ?? new()
            : new Dictionary<string, JsonElement>();

        var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
        dict[key] = doc.RootElement.Clone();
        return JsonSerializer.Serialize(dict);
    }

    // Extracts a typed value from the plan JSON blob by key.
    private static T ExtractPlan<T>(string? planJson, string key)
    {
        if (planJson is null)
            throw new InvalidOperationException($"PlanJson is null; cannot extract '{key}'.");

        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(planJson)
            ?? throw new InvalidOperationException("PlanJson could not be deserialized.");

        if (!dict.TryGetValue(key, out var element))
            throw new InvalidOperationException($"PlanJson does not contain key '{key}'.");

        return JsonSerializer.Deserialize<T>(element.GetRawText())
            ?? throw new InvalidOperationException($"Could not deserialize '{key}' as {typeof(T).Name}.");
    }
}
