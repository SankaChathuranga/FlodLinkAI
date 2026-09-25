using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>
/// Represents a field report submitted by a volunteer (Member A component).
/// </summary>
public class Report
{
    public int Id { get; set; }

    public int ShelterId { get; set; }

    public Shelter? Shelter { get; set; }

    public int ReportedBy { get; set; }

    public User? Reporter { get; set; }

    [Required]
    [MaxLength(50)]
    public string NeedType { get; set; } = string.Empty;

    public int QuantityNeeded { get; set; }

    public int UrgencyLevel { get; set; }

    [MaxLength(500)]
    public string? PhotoUrl { get; set; }

    public double? GpsLat { get; set; }

    public double? GpsLng { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "New";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
