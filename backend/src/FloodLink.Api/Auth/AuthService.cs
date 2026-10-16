using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Auth;

/// <summary>Account creation and password checks. Passwords are hashed with <see cref="PasswordHasher{TUser}"/>.</summary>
public sealed class AuthService(AppDbContext db, IPasswordHasher<User> hasher, ILogger<AuthService> logger)
{
    public const int MinPasswordLength = 8;

    // Verified when the email is unknown, so a miss costs the same as a wrong password.
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<User>().HashPassword(new User(), "dummy-password-for-timing"));

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    /// <summary>Returns the user when the email and password match, otherwise null (never says which was wrong).</summary>
    public async Task<User?> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalized = NormalizeEmail(email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null)
        {
            hasher.VerifyHashedPassword(new User(), DummyHash.Value, password);
            logger.LogInformation("Login failed: unknown email");
            return null;
        }

        var result = Verify(user, password);
        if (result == PasswordVerificationResult.Failed)
        {
            logger.LogInformation("Login failed for user {UserId}", user.Id);
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.HashedPassword = hasher.HashPassword(user, password);
            await db.SaveChangesAsync(ct);
        }

        logger.LogInformation("Login succeeded for user {UserId} ({Role})", user.Id, user.Role);
        return user;
    }

    /// <summary>Creates an account with the given role. Throws <see cref="ConflictException"/> for a taken email.</summary>
    public async Task<User> CreateAccountAsync(
        string name, string email, string password, string phone, string role, CancellationToken ct)
    {
        if (!Roles.All.Contains(role))
        {
            throw new BadRequestException($"Unknown role '{role}'.");
        }
        if (password.Length < MinPasswordLength)
        {
            throw new BadRequestException($"Password must be at least {MinPasswordLength} characters.");
        }

        var normalized = NormalizeEmail(email);
        if (await db.Users.AnyAsync(u => u.Email == normalized, ct))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            Name = name.Trim(),
            Email = normalized,
            Phone = phone.Trim(),
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        user.HashedPassword = hasher.HashPassword(user, password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Created user {UserId} with role {Role}", user.Id, user.Role);
        return user;
    }

    private PasswordVerificationResult Verify(User user, string password)
    {
        // Locked or legacy placeholder values are not valid hashes and must never log in.
        if (string.IsNullOrEmpty(user.HashedPassword) || user.HashedPassword == User.LockedPassword)
        {
            return PasswordVerificationResult.Failed;
        }
        try
        {
            return hasher.VerifyHashedPassword(user, user.HashedPassword, password);
        }
        catch (FormatException)
        {
            return PasswordVerificationResult.Failed;
        }
    }
}
