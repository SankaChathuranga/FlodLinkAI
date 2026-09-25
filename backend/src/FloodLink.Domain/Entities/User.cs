using System.ComponentModel.DataAnnotations;

namespace FloodLink.Domain.Entities;

/// <summary>
/// Minimal Users model required for foreign key relationships (Member A component).
/// </summary>
public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string HashedPassword { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Shelter> ContactShelters { get; set; } = new List<Shelter>();
    public ICollection<Report> SubmittedReports { get; set; } = new List<Report>();
}
