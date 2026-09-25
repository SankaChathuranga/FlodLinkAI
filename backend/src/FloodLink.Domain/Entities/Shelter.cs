using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>
/// Represents an emergency shelter location (Member A component).
/// </summary>
public class Shelter
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int Capacity { get; set; }

    public int CurrentOccupancy { get; set; }

    public int? ContactVolunteerId { get; set; }

    public User? ContactVolunteer { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Report> Reports { get; set; } = new List<Report>();
}
