using System.Diagnostics;
using System.Text.Json.Nodes;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Llm;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Llm;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Planner;

/// <summary>
/// Planner step (Member C). Asks the LLM to turn the coordinator's objective and a summary of the
/// run's reports into a four-step <see cref="ExecutionPlan"/>. The output is accepted only when it
/// names exactly Triage, Matching, Routing and Validation in that order with rationales of at most
/// 200 characters; anything else (or no LLM) gives the default plan, with the reason recorded.
/// Only counts and need types reach the model — no names, phone numbers or photos.
/// </summary>
public sealed class PlannerAgent(AppDbContext db, ILlmClient llm) : IPlannerInvoker
{
    public const string PromptVersion = "planner.v1";
    public const int MaxRationaleLength = 200;
    public const int MaxPurposeLength = 120;
    public static readonly string[] AgentOrder = ["Triage", "Matching", "Routing", "Validation"];

    private static readonly Lazy<string> Prompt = new(() => PromptLibrary.Load(typeof(PlannerAgent).Assembly, PromptVersion));

    public static ExecutionPlan DefaultPlan(string reason) => new()
    {
        Source = "default",
        PromptVersion = PromptVersion,
        FallbackReason = reason,
        Steps =
        [
            new() { Agent = "Triage", Purpose = "Rank the reported needs by urgency.", Rationale = "The most urgent needs are served first when stock is limited." },
            new() { Agent = "Matching", Purpose = "Allocate free depot stock to the ranked needs.", Rationale = "Only stock above each depot's reserve floor is offered." },
            new() { Agent = "Routing", Purpose = "Compute routes and ETAs from each depot to each shelter.", Rationale = "The coordinator needs distance and arrival time to decide." },
            new() { Agent = "Validation", Purpose = "Check the plan against the safety rules.", Rationale = "Unsafe plans must never reach approval." }
        ]
    };

    public async Task<AgentResult<ExecutionPlan>> ExecuteAsync(Guid workflowRunId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var run = await db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);
        if (run is null)
            return AgentResult<ExecutionPlan>.Fail("WORKFLOW_NOT_FOUND", $"No workflow run with id {workflowRunId}.");

        var reportIds = run.ReportIds ?? [];
        var reports = await db.Reports.AsNoTracking()
            .Where(r => reportIds.Contains(r.Id))
            .Select(r => new { r.NeedType, r.QuantityNeeded, r.ShelterId, r.UrgencyLevel })
            .ToListAsync(ct);
        var summary = new JsonObject
        {
            ["reportCount"] = reports.Count,
            ["shelterCount"] = reports.Select(r => r.ShelterId).Distinct().Count(),
            ["highestUrgency"] = reports.Count == 0 ? 0 : reports.Max(r => r.UrgencyLevel),
            ["needs"] = new JsonArray(reports
                .GroupBy(r => r.NeedType)
                .Select(g => (JsonNode)new JsonObject
                {
                    ["needType"] = g.Key,
                    ["reports"] = g.Count(),
                    ["totalQuantity"] = g.Sum(r => r.QuantityNeeded)
                }).ToArray())
        };
        var readCall = new ToolCall
        {
            Tool = "db.reports.summary",
            Input = new Dictionary<string, object?> { ["reportIds"] = reportIds },
            Output = new Dictionary<string, object?> { ["reports"] = reports.Count },
            Succeeded = true,
            DurationMs = sw.ElapsedMilliseconds
        };

        var request = new LlmRequest(
            PromptVersion,
            Prompt.Value,
            $"{LlmData.Fence("objective", run.Objective, 500)}\n<report_summary>{summary.ToJsonString()}</report_summary>",
            ResponseSchema);
        var llmResult = await llm.GenerateJsonAsync(request, ct);

        ExecutionPlan plan;
        string? rejected = null;
        if (!llmResult.Succeeded)
        {
            plan = DefaultPlan($"{llmResult.ErrorCode}: {llmResult.Error}");
        }
        else if (TryAccept(llmResult.Json, out var accepted, out rejected))
        {
            plan = accepted;
        }
        else
        {
            plan = DefaultPlan($"LLM_OUTPUT_REJECTED: {rejected}");
        }

        var llmCall = LlmToolCall.From(request, llmResult, usedFallback: plan.Source != "llm", rejected);
        return AgentResult<ExecutionPlan>.Ok(plan).WithToolCalls([readCall, llmCall]);
    }

    /// <summary>The deterministic check every LLM plan must pass before it is stored.</summary>
    public static bool TryAccept(JsonNode? json, out ExecutionPlan plan, out string? reason)
    {
        plan = null!;
        reason = null;
        if (json?["steps"] is not JsonArray steps)
        {
            reason = "missing steps";
            return false;
        }
        if (steps.Count != AgentOrder.Length)
        {
            reason = $"expected {AgentOrder.Length} steps, got {steps.Count}";
            return false;
        }

        var accepted = new List<ExecutionPlanStep>();
        for (var i = 0; i < steps.Count; i++)
        {
            var agent = steps[i]?["agent"]?.GetValue<string>();
            var purpose = steps[i]?["purpose"]?.GetValue<string>();
            var rationale = steps[i]?["rationale"]?.GetValue<string>();
            if (!string.Equals(agent, AgentOrder[i], StringComparison.Ordinal))
            {
                reason = $"step {i + 1} is '{LlmData.CleanOutput(agent, 40)}', expected {AgentOrder[i]}";
                return false;
            }
            if (string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(rationale))
            {
                reason = $"step {i + 1} has no purpose or rationale";
                return false;
            }
            if (rationale.Trim().Length > MaxRationaleLength)
            {
                reason = $"step {i + 1} rationale is longer than {MaxRationaleLength} characters";
                return false;
            }
            accepted.Add(new ExecutionPlanStep
            {
                Agent = AgentOrder[i],
                Purpose = LlmData.CleanOutput(purpose, MaxPurposeLength),
                Rationale = LlmData.CleanOutput(rationale, MaxRationaleLength)
            });
        }

        plan = new ExecutionPlan { Steps = accepted, Source = "llm", PromptVersion = PromptVersion };
        return true;
    }

    public static readonly JsonObject ResponseSchema = (JsonObject)JsonNode.Parse("""
    {
      "type": "OBJECT",
      "properties": {
        "steps": {
          "type": "ARRAY",
          "items": {
            "type": "OBJECT",
            "properties": {
              "agent": { "type": "STRING", "enum": ["Triage", "Matching", "Routing", "Validation"] },
              "purpose": { "type": "STRING" },
              "rationale": { "type": "STRING" }
            },
            "required": ["agent", "purpose", "rationale"],
            "propertyOrdering": ["agent", "purpose", "rationale"]
          }
        }
      },
      "required": ["steps"]
    }
    """)!;
}
