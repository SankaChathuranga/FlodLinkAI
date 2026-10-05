namespace FloodLink.Api.Dtos;

/// <summary>
/// Response shape for GET /api/workflows/{id}.
/// Exposes only the fields React and Flutter need — not the raw entity.
/// </summary>
public sealed record WorkflowRunResponse(
    Guid Id,
    string Objective,
    string CurrentState,
    string? FailedAtState,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? PlanJson
);
