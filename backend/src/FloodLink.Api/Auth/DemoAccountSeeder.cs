using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Auth;

/// <summary>
/// Gives the four <see cref="DemoAccounts"/> a real password hash from <c>Seed:DemoPassword</c>.
/// Idempotent: existing rows are matched by email and re-hashed; missing rows are inserted.
/// Does nothing when no demo password is configured, so the accounts stay locked.
/// </summary>
public static class DemoAccountSeeder
{
    public static async Task SeedAsync(IServiceProvider services, string? demoPassword, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoAccountSeeder));
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Seed:DemoPassword is not set; demo accounts stay locked");
            return;
        }
        if (demoPassword.Length < AuthService.MinPasswordLength)
        {
            throw new InvalidOperationException(
                $"Seed:DemoPassword must be at least {AuthService.MinPasswordLength} characters.");
        }

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

        foreach (var account in DemoAccounts.All)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == account.Email, ct);
            if (user is null)
            {
                user = new User
                {
                    Name = account.Name,
                    Email = account.Email,
                    Role = account.Role,
                    Phone = account.Phone,
                    CreatedAt = DateTime.UtcNow
                };
                db.Users.Add(user);
            }

            // Only re-hash when the stored hash does not already match, so restarts don't churn rows.
            var matches = user.HashedPassword != User.LockedPassword
                && !string.IsNullOrEmpty(user.HashedPassword)
                && SafeVerify(hasher, user, demoPassword);
            if (!matches)
            {
                user.HashedPassword = hasher.HashPassword(user, demoPassword);
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Demo accounts ready: {Emails}", string.Join(", ", DemoAccounts.All.Select(a => a.Email)));
    }

    private static bool SafeVerify(IPasswordHasher<User> hasher, User user, string password)
    {
        try
        {
            return hasher.VerifyHashedPassword(user, user.HashedPassword, password) != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
