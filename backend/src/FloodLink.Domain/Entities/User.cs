using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace FloodLink.Domain.Entities;

/// <summary>
/// A FloodLink account. <see cref="Role"/> holds one of the names in <c>FloodLink.Domain.Security.Roles</c>;
/// <see cref="HashedPassword"/> holds an ASP.NET Core Identity <c>PasswordHasher</c> hash.
/// </summary>
public class User : IHasUpdatedAt
{
    /// <summary>
    /// <see cref="HashedPassword"/> value for an account nobody can log in to (not a valid hash, so
    /// verification always fails). Seeded demo rows carry it until the startup seeder sets a real hash.
    /// </summary>
    public const string LockedPassword = "!";

    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Login name, stored lower-case. Unique (nullable only for legacy rows).</summary>
    [MaxLength(255)]
    public string? Email { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Never serialized: entities that include a User (e.g. a report's Reporter) must not leak it.</summary>
    [Required]
    [MaxLength(255)]
    [JsonIgnore]
    public string HashedPassword { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC time of the last change; stamped automatically on save.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<Shelter> ContactShelters { get; set; } = new List<Shelter>();
    public ICollection<Report> SubmittedReports { get; set; } = new List<Report>();
}
