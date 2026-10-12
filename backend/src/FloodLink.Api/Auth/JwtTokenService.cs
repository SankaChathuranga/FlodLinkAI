using System.Security.Claims;
using System.Text;
using FloodLink.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FloodLink.Api.Auth;

/// <summary>Issuer, audience and signing key shared by token issuing and validation.</summary>
public sealed record JwtSettings(string Issuer, string Audience, string SigningKey, TimeSpan Lifetime)
{
    public SymmetricSecurityKey Key => new(Encoding.UTF8.GetBytes(SigningKey));

    public static JwtSettings FromConfiguration(IConfiguration configuration) => new(
        configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer configuration is required."),
        configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience configuration is required."),
        configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException(
            "Jwt:SigningKey configuration is required. Set it through user-secrets or Jwt__SigningKey."),
        TimeSpan.FromHours(configuration.GetValue("Jwt:LifetimeHours", 8)));
}

/// <summary>Issues signed access tokens carrying <c>sub</c>, <c>name</c>, <c>email</c> and <c>role</c>.</summary>
public sealed class JwtTokenService(JwtSettings settings, TimeProvider clock)
{
    public (string Token, DateTime ExpiresAt) Issue(User user)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.Add(settings.Lifetime);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.Name),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim("role", user.Role),
            ]),
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(settings.Key, SecurityAlgorithms.HmacSha256)
        });
        return (token, expiresAt);
    }
}
