using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace FloodLink.Api.Dtos;

/// <summary>Body for POST /api/workflows.</summary>
public sealed record CreateWorkflowRequest
{
    /// <summary>What the run should achieve, e.g. "Water and food for Kaduwela shelters".</summary>
    [Required, StringLength(1000, MinimumLength = 1)]
    public string Objective { get; init; } = string.Empty;

    /// <summary>
    /// The field reports to plan for. When omitted, every New or Triaged report that isn't
    /// already in an active run is used.
    /// </summary>
    public List<int>? ReportIds { get; init; }
}

/// <summary>
/// Response shape for GET /api/workflows/{id}.
/// Exposes only the fields React and Flutter need — not the raw entity.
/// </summary>
public sealed record WorkflowRunResponse(
    Guid Id,
    string Objective,
    string CurrentState,
    string? FailedAtState,
    string? FailureReason,
    IReadOnlyList<int> ReportIds,
    int RetryCount,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? PlanJson
);

/// <summary>One row of GET /api/workflows.</summary>
public sealed record WorkflowRunSummary(
    Guid Id,
    string Objective,
    string CurrentState,
    int ProgressPercent,
    string? FailedAtState,
    string? FailureReason,
    int ReportCount,
    int RetryCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>One entry of GET /api/workflows/{id}/logs: an agent or orchestration step.</summary>
public sealed record WorkflowLogEntry(
    Guid Id,
    string AgentName,
    string Status,
    bool IsRetry,
    long DurationMs,
    DateTime CreatedAt,
    string? ErrorMessage,
    JsonElement? ToolCalls,
    JsonElement? Output,
    JsonElement? Input
);

/// <summary>
/// Lightweight status for polling (Flutter status tracker, React monitor):
/// GET /api/workflows/{id}/status.
/// </summary>
public sealed record WorkflowStatusResponse(
    Guid Id,
    string State,
    string StageLabel,
    int ProgressPercent,
    bool IsFinished,
    bool AwaitingApproval,
    string? FailedAtState,
    string? FailureReason,
    string? Decision,
    string? CoordinatorNote,
    DateTime? DecidedAt,
    double? DistanceKm,
    double? EtaMinutes,
    bool IsDelivered,
    DateTime UpdatedAt
);
