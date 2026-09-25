using System.ComponentModel.DataAnnotations;

namespace FloodLink.Api.DTOs;

public class CreateReportDto
{
    [Required(ErrorMessage = "ShelterId is required.")]
    public int ShelterId { get; set; }

    [Required(ErrorMessage = "ReportedBy user ID is required.")]
    public int ReportedBy { get; set; }

    [Required(ErrorMessage = "NeedType is required.")]
    [RegularExpression("^(Water|Food|Medical|Shelter-Repair|Other)$", ErrorMessage = "NeedType must be Water, Food, Medical, Shelter-Repair, or Other.")]
    public string NeedType { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "QuantityNeeded must be at least 1.")]
    public int QuantityNeeded { get; set; }

    [Range(1, 5, ErrorMessage = "UrgencyLevel must be between 1 and 5.")]
    public int UrgencyLevel { get; set; }

    [Url(ErrorMessage = "PhotoUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "PhotoUrl cannot exceed 500 characters.")]
    public string? PhotoUrl { get; set; }

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

    [Range(1, 5, ErrorMessage = "UrgencyLevel must be between 1 and 5.")]
    public int UrgencyLevel { get; set; }

    [Url(ErrorMessage = "PhotoUrl must be a valid URL.")]
    [StringLength(500, ErrorMessage = "PhotoUrl cannot exceed 500 characters.")]
    public string? PhotoUrl { get; set; }

    [Range(-90.0, 90.0, ErrorMessage = "GpsLat must be between -90 and 90.")]
    public double? GpsLat { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "GpsLng must be between -180 and 180.")]
    public double? GpsLng { get; set; }

    [RegularExpression("^(New|Triaged|InPlan|Resolved)$", ErrorMessage = "Status must be New, Triaged, InPlan, or Resolved.")]
    public string Status { get; set; } = "New";
}
