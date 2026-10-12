using FloodLink.Api.Auth;
using FloodLink.Api.DTOs;
using FloodLink.Domain.Exceptions;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>Admin-only account management: list accounts and create Coordinator / DepotManager staff.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = Policies.Admin)]
public sealed class UsersController(AuthService auth, AppDbContext db) : ControllerBase
{
    /// <summary>GET /api/users?role=&amp;search= — every account, newest first. Password hashes are never returned.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> List(
        [FromQuery] string? role, [FromQuery] string? search, CancellationToken ct)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role.Trim());
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.Name, term) || EF.Functions.ILike(u.Email!, term));
        }

        var users = await query.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).ToListAsync(ct);
        return Ok(users.Select(UserDto.From));
    }

    /// <summary>POST /api/users — creates a Coordinator or DepotManager account.</summary>
    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (!Roles.StaffCreatable.Contains(request.Role))
        {
            throw new BadRequestException($"Role must be one of: {string.Join(", ", Roles.StaffCreatable)}.");
        }
        var user = await auth.CreateAccountAsync(
            request.Name, request.Email, request.Password, request.Phone, request.Role, ct);
        return Created($"/api/users/{user.Id}", UserDto.From(user));
    }
}
