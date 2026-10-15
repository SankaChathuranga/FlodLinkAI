using System.ComponentModel.DataAnnotations;

namespace FloodLink.Api.DTOs;

/// <summary>Optional filters for <c>GET /api/allocations</c>. All filters combine with AND.</summary>
public sealed class AllocationQueryDto
{
    /// <summary>Only allocations proposed for this workflow run.</summary>
    public Guid? WorkflowRunId { get; init; }

    /// <summary>Only allocations with this status (case-insensitive). Currently "Proposed".</summary>
    [StringLength(20)]
    public string? Status { get; init; }

    /// <summary>Only allocations destined for this shelter.</summary>
    [Range(1, int.MaxValue)]
    public int? ShelterId { get; init; }
}

/// <summary>One persisted allocation row produced by the Matching Agent, with display names.</summary>
public sealed record AllocationResponseDto(
    int Id,
    Guid WorkflowRunId,
    int DepotId,
    string DepotName,
    int ShelterId,
    string ShelterName,
    string ItemName,
    double Quantity,
    string Status,
    DateTime CreatedAt);
