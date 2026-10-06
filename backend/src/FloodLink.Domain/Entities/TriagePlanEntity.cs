using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>
/// Represents a generated triage plan stored in the database (Member A component).
/// </summary>
public class TriagePlanEntity
{
    public int Id { get; set; }

    /// <summary>
    /// JSON array of report IDs from which this triage plan was generated.
    /// </summary>
    public List<int> GeneratedFromReportIds { get; set; } = new();

    public int PriorityRank { get; set; }

    /// <summary>
    /// Detailed JSON summary payload of the triage plan.
    /// </summary>
    public string PlanSummaryJson { get; set; } = "{}";

    public Guid? CreatedByAgentRunId { get; set; }

    public WorkflowRun? CreatedByAgentRun { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
