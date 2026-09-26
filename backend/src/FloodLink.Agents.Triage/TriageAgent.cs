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
    public async Task<AgentResult<TriagePlan>> ExecuteAsync(
        Guid workflowRunId,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteInternalAsync(workflowRunId, targetReportId: null, cancellationToken);
    }

    /// <summary>
    /// Executes triage for a specific report ID or all pending reports.
    /// </summary>
    public async Task<AgentResult<TriagePlan>> ExecuteForReportAsync(
        int reportId,
        Guid? workflowRunId = null,
        CancellationToken cancellationToken = default)
    {
        Guid effectiveWorkflowId = workflowRunId ?? Guid.NewGuid();
        return await ExecuteInternalAsync(effectiveWorkflowId, targetReportId: reportId, cancellationToken);
    }

    private async Task<AgentResult<TriagePlan>> ExecuteInternalAsync(
        Guid workflowRunId,
        int? targetReportId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Ensure WorkflowRun exists in DB
            WorkflowRun? workflowRun = await _context.WorkflowRuns
                .FirstOrDefaultAsync(w => w.Id == workflowRunId, cancellationToken);

            if (workflowRun == null)
            {
                workflowRun = new WorkflowRun
                {
                    Id = workflowRunId,
                    Objective = $"Triage Execution for Workflow {workflowRunId:N}",
                    CurrentState = Domain.Enums.WorkflowState.Triage,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.WorkflowRuns.Add(workflowRun);
            }

            // Load reports to triage
            IQueryable<Report> reportsQuery = _context.Reports.Include(r => r.Shelter);

            if (targetReportId.HasValue)
            {
                reportsQuery = reportsQuery.Where(r => r.Id == targetReportId.Value);
            }
            else
            {
                reportsQuery = reportsQuery.Where(r => r.Status != "Resolved");
            }

            List<Report> reportsToTriage = await reportsQuery.ToListAsync(cancellationToken);

            if (reportsToTriage.Count == 0)
            {
                return AgentResult<TriagePlan>.Fail(
                    "NO_REPORTS_FOUND",
                    $"No active reports found for triage (TargetReportId: {targetReportId?.ToString() ?? "ALL"}).");
            }

            var priorityItems = new List<TriagePriorityItem>();
            var reportIds = new List<int>();

            foreach (var report in reportsToTriage)
            {
                reportIds.Add(report.Id);
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

            // Rank priority items by score descending
            var rankedPriorityItems = priorityItems
                .OrderByDescending(i => i.PriorityScore)
                .ThenByDescending(i => i.Quantity)
                .ToList();

            var triagePlan = new TriagePlan
            {
                WorkflowRunId = workflowRunId,
                PriorityItems = rankedPriorityItems
            };

            // 3. Persist TriagePlanEntity row in DB
            string planJson = JsonSerializer.Serialize(triagePlan, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            var triagePlanEntity = new TriagePlanEntity
            {
                GeneratedFromReportIds = reportIds,
                PriorityRank = 1,
                PlanSummaryJson = planJson,
                CreatedByAgentRunId = workflowRunId,
                CreatedAt = DateTime.UtcNow
            };

            _context.TriagePlans.Add(triagePlanEntity);
            workflowRun.PlanJson = planJson;
            workflowRun.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return AgentResult<TriagePlan>.Ok(triagePlan);
        }
        catch (Exception ex)
        {
            return AgentResult<TriagePlan>.Fail(
                "TRIAGE_EXECUTION_ERROR",
                $"An unexpected error occurred during triage execution: {ex.Message}");
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
