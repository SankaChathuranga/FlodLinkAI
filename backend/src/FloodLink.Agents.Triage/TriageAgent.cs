using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using FloodLink.Contracts;
using FloodLink.Domain.Llm;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Services;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Llm;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Triage;

/// <summary>
/// Production implementation of <see cref="ITriageAgent"/> (Member A — Sharani).
/// Every report gets a rule-based urgency score (<see cref="IUrgencyScoringService"/>). The LLM
/// then reads the volunteers' notes and shelter context and may adjust each score by at most
/// ±<see cref="MaxAdjustment"/> with a short justification. Its output is checked
/// (<see cref="TryAcceptAdjustments"/>) and discarded in favour of the rule scores and template
/// justifications when the call fails or any check fails. Final scores are clamped to 0–100.
/// </summary>
public sealed class TriageAgent : ITriageAgent
{
    public const string PromptVersion = "triage.v1";
    public const int MaxAdjustment = 15;
    public const int MaxJustificationLength = 200;
    public const int MaxNotesLength = 500;

    private static readonly Lazy<string> Prompt = new(() => PromptLibrary.Load(typeof(TriageAgent).Assembly, PromptVersion));

    private readonly AppDbContext _context;
    private readonly IUrgencyScoringService _urgencyScoringService;
    private readonly ILlmClient? _llm;

    /// <param name="llm">Optional: without it (or when it fails) triage is purely rule-based.</param>
    public TriageAgent(AppDbContext context, IUrgencyScoringService urgencyScoringService, ILlmClient? llm = null)
    {
        _context = context;
        _urgencyScoringService = urgencyScoringService;
        _llm = llm;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Triages the reports the run was started for (<see cref="WorkflowRun.ReportIds"/>), or every
    /// unresolved report for older runs that have no report list. Fails when the run doesn't exist.
    /// </remarks>
    public async Task<AgentResult<TriagePlan>> ExecuteAsync(
        Guid workflowRunId,
        CancellationToken cancellationToken = default)
    {
        WorkflowRun? workflowRun = await _context.WorkflowRuns
            .FirstOrDefaultAsync(w => w.Id == workflowRunId, cancellationToken);
        if (workflowRun is null)
        {
            return AgentResult<TriagePlan>.Fail(
                "WORKFLOW_NOT_FOUND", $"No workflow run with id {workflowRunId}.");
        }

        return await ExecuteInternalAsync(workflowRun, workflowRun.ReportIds, cancellationToken);
    }

    /// <summary>
    /// Previews triage for a single report without starting a workflow run. The plan is persisted
    /// as a <see cref="TriagePlanEntity"/> with no run, and its <see cref="TriagePlan.WorkflowRunId"/>
    /// is <see cref="Guid.Empty"/>.
    /// </summary>
    public async Task<AgentResult<TriagePlan>> ExecuteForReportAsync(
        int reportId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInternalAsync(workflowRun: null, [reportId], cancellationToken);
    }

    private async Task<AgentResult<TriagePlan>> ExecuteInternalAsync(
        WorkflowRun? workflowRun,
        IReadOnlyCollection<int>? reportIds,
        CancellationToken cancellationToken)
    {
        var toolCalls = new List<ToolCall>();
        try
        {
            // Tool: read-only report query, scoped to the requested reports.
            var sw = Stopwatch.StartNew();
            IQueryable<Report> reportsQuery = _context.Reports.Include(r => r.Shelter);
            reportsQuery = reportIds is { Count: > 0 }
                ? reportsQuery.Where(r => reportIds.Contains(r.Id))
                : reportsQuery.Where(r => r.Status != "Resolved");

            List<Report> reportsToTriage = await reportsQuery.ToListAsync(cancellationToken);
            toolCalls.Add(new ToolCall
            {
                Tool = "db.reports.read",
                Input = new Dictionary<string, object?> { ["reportIds"] = reportIds?.ToList() },
                Output = new Dictionary<string, object?> { ["count"] = reportsToTriage.Count },
                Succeeded = true,
                DurationMs = sw.ElapsedMilliseconds
            });

            if (reportsToTriage.Count == 0)
            {
                return AgentResult<TriagePlan>.Fail(
                    "NO_REPORTS_FOUND",
                    $"No active reports found for triage (reports: {(reportIds is { Count: > 0 } ? string.Join(", ", reportIds) : "ALL")}).")
                    .WithToolCalls(toolCalls);
            }

            // Every requested report must exist; a plan must never reference a missing report.
            if (reportIds is { Count: > 0 })
            {
                var missing = reportIds.Except(reportsToTriage.Select(r => r.Id)).ToList();
                if (missing.Count > 0)
                {
                    return AgentResult<TriagePlan>.Fail(
                        "REPORT_NOT_FOUND", $"Reports not found: {string.Join(", ", missing)}.")
                        .WithToolCalls(toolCalls);
                }
            }

            // Tool: get_shelter_context(shelterId) for every shelter in the reports (ids come
            // from the database rows, never from model output). Read-only.
            sw.Restart();
            var shelterContext = new Dictionary<int, ShelterContext>();
            foreach (var shelterId in reportsToTriage.Select(r => r.ShelterId).Distinct())
            {
                DateTime? lastResupplyUtc = await _context.Reports
                    .Where(r => r.ShelterId == shelterId && r.Status == "Resolved")
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => (DateTime?)r.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                var shelter = reportsToTriage.First(r => r.ShelterId == shelterId).Shelter;
                shelterContext[shelterId] = new ShelterContext(shelterId, shelter, lastResupplyUtc);
            }
            toolCalls.Add(new ToolCall
            {
                Tool = "get_shelter_context",
                Input = new Dictionary<string, object?> { ["shelterIds"] = shelterContext.Keys.ToList() },
                Output = new Dictionary<string, object?> { ["shelters"] = shelterContext.Count },
                Succeeded = true,
                DurationMs = sw.ElapsedMilliseconds
            });

            // 1. Rule-based urgency score (0-100) for every report.
            var ruleScores = reportsToTriage.ToDictionary(
                r => r.Id,
                r =>
                {
                    var ctx = shelterContext[r.ShelterId];
                    return Math.Clamp((double)_urgencyScoringService.CalculateUrgencyScore(ctx.Shelter, r.NeedType, ctx.LastResupplyUtc), 0.0, 100.0);
                });

            // 2. Bounded LLM adjustments from the notes, or none (rule-based fallback).
            var adjustments = await AdjustWithLlmAsync(reportsToTriage, ruleScores, shelterContext, toolCalls, cancellationToken);

            var priorityItems = new List<TriagePriorityItem>();
            foreach (var report in reportsToTriage)
            {
                double ruleScore = ruleScores[report.Id];
                LlmAdjustment? adjustment = adjustments?.GetValueOrDefault(report.Id);
                double finalScore = Math.Clamp(ruleScore + (adjustment?.Adjustment ?? 0), 0.0, 100.0);

                // Update report urgency level and status in DB
                report.UrgencyLevel = (int)finalScore;
                if (report.Status == "New")
                {
                    report.Status = "Triaged";
                }

                priorityItems.Add(new TriagePriorityItem
                {
                    ReportId = report.Id,
                    ShelterId = report.ShelterId,
                    NeedType = report.NeedType,
                    Quantity = report.QuantityNeeded,
                    PriorityScore = finalScore,
                    RuleScore = ruleScore,
                    LlmAdjustment = adjustment?.Adjustment,
                    Justification = adjustment is not null
                        ? $"{adjustment.Justification} (rule score {ruleScore:F0}, LLM adjustment {adjustment.Adjustment:+0;-0;0})"
                        : BuildJustification(report, report.Shelter, finalScore)
                });
            }

            // Rank priority items by score descending
            var rankedPriorityItems = priorityItems
                .OrderByDescending(i => i.PriorityScore)
                .ThenByDescending(i => i.Quantity)
                .ToList();

            var triagePlan = new TriagePlan
            {
                WorkflowRunId = workflowRun?.Id ?? Guid.Empty,
                PriorityItems = rankedPriorityItems,
                ScoringMethod = adjustments is not null ? "llm-adjusted" : "rule-based"
            };

            // 3. Persist TriagePlanEntity row in DB. The orchestrator stores the plan in the run's
            //    PlanJson under its own key; the agent doesn't write PlanJson itself.
            string planJson = JsonSerializer.Serialize(triagePlan, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            _context.TriagePlans.Add(new TriagePlanEntity
            {
                GeneratedFromReportIds = reportsToTriage.Select(r => r.Id).ToList(),
                PriorityRank = 1,
                PlanSummaryJson = planJson,
                CreatedByAgentRunId = workflowRun?.Id,
                CreatedAt = DateTime.UtcNow
            });

            if (workflowRun is not null)
                workflowRun.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return AgentResult<TriagePlan>.Ok(triagePlan).WithToolCalls(toolCalls);
        }
        catch (Exception ex)
        {
            return AgentResult<TriagePlan>.Fail(
                "TRIAGE_EXECUTION_ERROR",
                $"An unexpected error occurred during triage execution: {ex.Message}")
                .WithToolCalls(toolCalls);
        }
    }

    private sealed record ShelterContext(int ShelterId, Shelter? Shelter, DateTime? LastResupplyUtc);

    /// <summary>An accepted LLM adjustment for one report.</summary>
    public sealed record LlmAdjustment(int Adjustment, string Justification);

    // Returns the accepted adjustments by report id, or null to use the rule scores unchanged.
    private async Task<Dictionary<int, LlmAdjustment>?> AdjustWithLlmAsync(
        List<Report> reports,
        Dictionary<int, double> ruleScores,
        Dictionary<int, ShelterContext> shelters,
        List<ToolCall> toolCalls,
        CancellationToken ct)
    {
        if (_llm is null)
            return null;

        var request = new LlmRequest(PromptVersion, Prompt.Value, BuildUserContent(reports, ruleScores, shelters), ResponseSchema);
        var result = await _llm.GenerateJsonAsync(request, ct);
        if (!result.Succeeded)
        {
            toolCalls.Add(LlmToolCall.From(request, result, usedFallback: true));
            return null;
        }

        var accepted = TryAcceptAdjustments(result.Json, reports.Select(r => r.Id).ToHashSet(), out var adjustments, out var reason);
        toolCalls.Add(LlmToolCall.From(request, result, usedFallback: !accepted, reason));
        return accepted ? adjustments : null;
    }

    // Only ids, need types, quantities, scores, shelter numbers and the fenced notes reach the model.
    private static string BuildUserContent(List<Report> reports, Dictionary<int, double> ruleScores, Dictionary<int, ShelterContext> shelters)
    {
        var shelterJson = new JsonArray(shelters.Values.Select(s => (JsonNode)new JsonObject
        {
            ["shelterId"] = s.ShelterId,
            ["capacity"] = s.Shelter?.Capacity,
            ["occupancy"] = s.Shelter?.CurrentOccupancy,
            ["occupancyPercent"] = s.Shelter is { Capacity: > 0 } sh ? (int)Math.Round(100.0 * sh.CurrentOccupancy / sh.Capacity) : null,
            ["daysSinceLastResupply"] = s.LastResupplyUtc is { } t ? (int)(DateTime.UtcNow - t).TotalDays : null
        }).ToArray());

        var sb = new StringBuilder();
        sb.Append("<shelter_context>").Append(shelterJson.ToJsonString()).Append("</shelter_context>\n");
        foreach (var r in reports)
        {
            var report = new JsonObject
            {
                ["reportId"] = r.Id,
                ["shelterId"] = r.ShelterId,
                ["needType"] = r.NeedType,
                ["quantity"] = r.QuantityNeeded,
                ["ruleScore"] = (int)ruleScores[r.Id]
            };
            sb.Append("<report>").Append(report.ToJsonString()).Append("</report>\n");
            sb.Append(LlmData.Fence("report_notes", r.Notes, MaxNotesLength, $"reportId=\"{r.Id}\"")).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>
    /// The deterministic check on the LLM output: exactly one entry per input report (no unknown
    /// or duplicate ids), integer adjustments within ±<see cref="MaxAdjustment"/>, and a non-empty
    /// justification of at most <see cref="MaxJustificationLength"/> characters. Any failure rejects
    /// the whole output.
    /// </summary>
    public static bool TryAcceptAdjustments(
        JsonNode? json, IReadOnlySet<int> reportIds, out Dictionary<int, LlmAdjustment> adjustments, out string? reason)
    {
        adjustments = new Dictionary<int, LlmAdjustment>();
        reason = null;
        if (json?["adjustments"] is not JsonArray items)
        {
            reason = "missing adjustments";
            return false;
        }

        foreach (var item in items)
        {
            if (item?["reportId"] is not JsonValue idValue || !idValue.TryGetValue<int>(out var reportId))
            {
                reason = "an entry has no integer reportId";
                return false;
            }
            if (!reportIds.Contains(reportId))
            {
                reason = $"unknown report id {reportId}";
                return false;
            }
            if (adjustments.ContainsKey(reportId))
            {
                reason = $"report {reportId} appears twice";
                return false;
            }
            if (item["adjustment"] is not JsonValue adjValue || !adjValue.TryGetValue<int>(out var adjustment)
                || adjustment is < -MaxAdjustment or > MaxAdjustment)
            {
                reason = $"report {reportId} adjustment is not an integer in -{MaxAdjustment}..{MaxAdjustment}";
                return false;
            }
            var justification = item["justification"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(justification) || justification.Trim().Length > MaxJustificationLength)
            {
                reason = $"report {reportId} justification is empty or longer than {MaxJustificationLength} characters";
                return false;
            }
            adjustments[reportId] = new LlmAdjustment(adjustment, LlmData.CleanOutput(justification, MaxJustificationLength));
        }

        if (adjustments.Count != reportIds.Count)
        {
            reason = $"expected {reportIds.Count} entries, got {adjustments.Count}";
            return false;
        }
        return true;
    }

    public static readonly JsonObject ResponseSchema = (JsonObject)JsonNode.Parse("""
    {
      "type": "OBJECT",
      "properties": {
        "adjustments": {
          "type": "ARRAY",
          "items": {
            "type": "OBJECT",
            "properties": {
              "reportId": { "type": "INTEGER" },
              "adjustment": { "type": "INTEGER", "minimum": -15, "maximum": 15 },
              "justification": { "type": "STRING" }
            },
            "required": ["reportId", "adjustment", "justification"],
            "propertyOrdering": ["reportId", "adjustment", "justification"]
          }
        }
      },
      "required": ["adjustments"]
    }
    """)!;

    private static string BuildJustification(Report report, Shelter? shelter, double score)
    {
        string shelterName = shelter?.Name ?? $"Shelter #{report.ShelterId}";
        int capacity = shelter?.Capacity ?? 0;
        int occupancy = shelter?.CurrentOccupancy ?? 0;
        double pct = capacity > 0 ? (occupancy / (double)capacity) * 100 : 100.0;

        string urgencyLabel = score >= 80 ? "Critical" : score >= 50 ? "High" : "Moderate";

        return $"[{urgencyLabel} Priority] {report.NeedType} requirement of {report.QuantityNeeded} units at {shelterName}. " +
               $"Shelter current occupancy is {occupancy}/{capacity} ({pct:F0}% capacity). Assigned priority score {score:F0}/100.";
    }
}
