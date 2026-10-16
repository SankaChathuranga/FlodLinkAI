using System.ComponentModel.DataAnnotations;
using FloodLink.Api.Auth;
using FloodLink.Domain.Entities;

namespace FloodLink.Api.DTOs;

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Self-registration. There is deliberately no Role field: every new account is a Volunteer.</summary>
public sealed class RegisterRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(AuthService.MinPasswordLength), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    [RegularExpression(@"^\+?[0-9 ]{7,20}$", ErrorMessage = "Phone must be 7–20 digits, optionally starting with +.")]
    public string Phone { get; set; } = string.Empty;
}

/// <summary>Admin-only staff account creation (Coordinator or DepotManager).</summary>
public sealed class CreateUserRequest
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(AuthService.MinPasswordLength), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    [RegularExpression(@"^\+?[0-9 ]{7,20}$", ErrorMessage = "Phone must be 7–20 digits, optionally starting with +.")]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = string.Empty;
}

public sealed record UserDto(int Id, string Name, string? Email, string Role, string Phone, DateTime CreatedAt)
{
    public static UserDto From(User u) => new(u.Id, u.Name, u.Email, u.Role, u.Phone, u.CreatedAt);
}

public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);
