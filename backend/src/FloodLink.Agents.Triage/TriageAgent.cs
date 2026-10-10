using System.Diagnostics;
using System.Text.Json;
using FloodLink.Contracts;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Services;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Triage;

/// <summary>
/// Production implementation of <see cref="ITriageAgent"/> (Member A — Sharani).
/// Combines rule-based urgency scoring with structured triage reasoning to rank
/// shelter needs and produce a justified <see cref="TriagePlan"/>.
/// </summary>
public sealed class TriageAgent : ITriageAgent
{
    private readonly AppDbContext _context;
    private readonly IUrgencyScoringService _urgencyScoringService;

    public TriageAgent(AppDbContext context, IUrgencyScoringService urgencyScoringService)
    {
        _context = context;
        _urgencyScoringService = urgencyScoringService;
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

            var priorityItems = new List<TriagePriorityItem>();
            sw.Restart();

            foreach (var report in reportsToTriage)
            {
                Shelter? shelter = report.Shelter;

                // 1. Calculate Rule-Based Urgency Score (0-100)
                DateTime? lastResupplyUtc = await _context.Reports
                    .Where(r => r.ShelterId == report.ShelterId && r.Status == "Resolved")
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => (DateTime?)r.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                int autoScore = _urgencyScoringService.CalculateUrgencyScore(shelter, report.NeedType, lastResupplyUtc);
                double finalScore = Math.Clamp((double)autoScore, 0.0, 100.0);

                // Update report urgency level and status in DB
                report.UrgencyLevel = (int)finalScore;
                if (report.Status == "New")
                {
                    report.Status = "Triaged";
                }

                // 2. Generate Structured Justification
                string justification = BuildJustification(report, shelter, finalScore);

                priorityItems.Add(new TriagePriorityItem
                {
                    ReportId = report.Id,
                    ShelterId = report.ShelterId,
                    NeedType = report.NeedType,
                    Quantity = report.QuantityNeeded,
                    PriorityScore = finalScore,
                    Justification = justification
                });
            }

            toolCalls.Add(new ToolCall
            {
                Tool = "db.shelter_history.read",
                Input = new Dictionary<string, object?>
                {
                    ["shelterIds"] = reportsToTriage.Select(r => r.ShelterId).Distinct().ToList()
                },
                Output = new Dictionary<string, object?> { ["scored"] = priorityItems.Count },
                Succeeded = true,
                DurationMs = sw.ElapsedMilliseconds
            });

            // Rank priority items by score descending
            var rankedPriorityItems = priorityItems
                .OrderByDescending(i => i.PriorityScore)
                .ThenByDescending(i => i.Quantity)
                .ToList();

            var triagePlan = new TriagePlan
            {
                WorkflowRunId = workflowRun?.Id ?? Guid.Empty,
                PriorityItems = rankedPriorityItems
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
