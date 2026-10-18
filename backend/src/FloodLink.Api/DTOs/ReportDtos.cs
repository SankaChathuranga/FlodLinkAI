using System.ComponentModel.DataAnnotations;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace FloodLink.Api.DTOs;

public class CreateReportDto
{
    /// <summary>
    /// Optional id the app generates per report (e.g. a UUID). Re-sending a report with the same value
    /// returns the report already saved instead of creating a second one (offline queue, 8.09).
    /// </summary>
    [StringLength(64, MinimumLength = 8, ErrorMessage = "ClientReference must be 8–64 characters.")]
    [RegularExpression("^[A-Za-z0-9-]+$", ErrorMessage = "ClientReference may contain only letters, digits and '-'.")]
    public string? ClientReference { get; set; }

    [Required(ErrorMessage = "ShelterId is required.")]
    public int ShelterId { get; set; }

    [Required(ErrorMessage = "NeedType is required.")]
    [RegularExpression("^(Water|Food|Medical|Shelter-Repair|Other)$", ErrorMessage = "NeedType must be Water, Food, Medical, Shelter-Repair, or Other.")]
    public string NeedType { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "QuantityNeeded must be at least 1.")]
    public int QuantityNeeded { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    [Range(0, 100, ErrorMessage = "UrgencyLevel must be between 0 and 100.")]
    public int? UrgencyLevel { get; set; }

    [Url(ErrorMessage = "PhotoUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "PhotoUrl cannot exceed 500 characters.")]
    public string? PhotoUrl { get; set; }

    public IFormFile? Photo { get; set; }

    [Range(-90.0, 90.0, ErrorMessage = "GpsLat must be between -90 and 90.")]
    public double? GpsLat { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "GpsLng must be between -180 and 180.")]
    public double? GpsLng { get; set; }

    [RegularExpression("^(New|Triaged|InPlan|Resolved)$", ErrorMessage = "Status must be New, Triaged, InPlan, or Resolved.")]
    public string Status { get; set; } = "New";
}

public class UpdateReportDto
{
    [Required(ErrorMessage = "NeedType is required.")]
    [RegularExpression("^(Water|Food|Medical|Shelter-Repair|Other)$", ErrorMessage = "NeedType must be Water, Food, Medical, Shelter-Repair, or Other.")]
    public string NeedType { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "QuantityNeeded must be at least 1.")]
    public int QuantityNeeded { get; set; }

    [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    public string? Notes { get; set; }

    [Range(0, 100, ErrorMessage = "UrgencyLevel must be between 0 and 100.")]
    public int UrgencyLevel { get; set; }

    [Url(ErrorMessage = "PhotoUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "PhotoUrl cannot exceed 500 characters.")]
    public string? PhotoUrl { get; set; }

    public IFormFile? Photo { get; set; }

    [Range(-90.0, 90.0, ErrorMessage = "GpsLat must be between -90 and 90.")]
    public double? GpsLat { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "GpsLng must be between -180 and 180.")]
    public double? GpsLng { get; set; }

    [RegularExpression("^(New|Triaged|InPlan|Resolved)$", ErrorMessage = "Status must be New, Triaged, InPlan, or Resolved.")]
    public string Status { get; set; } = "New";
}

/// <summary>A volunteer's own report plus the stage of the run handling it (GET /api/reports/mine).</summary>
public sealed record MyReportDto(
    int Id,
    int ShelterId,
    string? ShelterName,
    string NeedType,
    int QuantityNeeded,
    int UrgencyLevel,
    string? Notes,
    string Status,
    string? PhotoUrl,
    double? GpsLat,
    double? GpsLng,
    DateTime CreatedAt,
    Guid? WorkflowRunId,
    string? WorkflowState,
    string? Decision)
{
    public static MyReportDto From(Report r, string? shelterName, WorkflowState? state, DispatchDecision? decision) => new(
        r.Id, r.ShelterId, shelterName, r.NeedType, r.QuantityNeeded, r.UrgencyLevel, r.Notes, r.Status, r.PhotoUrl,
        r.GpsLat, r.GpsLng, r.CreatedAt, r.WorkflowRunId, state?.ToString(), decision?.ToString());
}
