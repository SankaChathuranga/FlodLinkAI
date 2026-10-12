using FloodLink.Api.Auth;
using FloodLink.Api.DTOs;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>Login, self-registration (Volunteer only) and the current user.</summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, JwtTokenService tokens, AppDbContext db) : ControllerBase
{
    public const string LoginRateLimitPolicy = "login";

    /// <summary>Exchanges email + password for an 8-hour bearer token. Wrong email and wrong password both return 401.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await auth.LoginAsync(request.Email, request.Password, ct);
        if (user is null)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
        }
        var (token, expiresAt) = tokens.Issue(user);
        return Ok(new AuthResponse(token, expiresAt, UserDto.From(user)));
    }

    /// <summary>Creates a Volunteer account and signs it in. Any role sent in the body is ignored.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(LoginRateLimitPolicy)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var user = await auth.CreateAccountAsync(
            request.Name, request.Email, request.Password, request.Phone, Roles.Volunteer, ct);
        var (token, expiresAt) = tokens.Issue(user);
        return CreatedAtAction(nameof(Me), null, new AuthResponse(token, expiresAt, UserDto.From(user)));
    }

    /// <summary>The signed-in user, read fresh from the database.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = userId is null ? null : await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? Unauthorized() : Ok(UserDto.From(user));
    }
}
