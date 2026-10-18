using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using FloodLink.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>Supply depots (Member B): list, read, create, and edit including the assigned DepotManager.</summary>
[ApiController]
[Route("api/depots")]
[Authorize(Policy = Policies.StockReader)]
public sealed class DepotsController(AppDbContext context, ILogger<DepotsController> logger) : ControllerBase
{
    /// <summary>
    /// GET /api/depots?search=&amp;page=&amp;pageSize= — one page of depots by name; <c>search</c>
    /// matches the depot or manager name.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PaginatedResult<DepotDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<DepotDto>>> GetDepots(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        IQueryable<Depot> query = context.Depots.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(d => d.Name.ToLower().Contains(term)
                                     || (d.Manager != null && d.Manager.Name.ToLower().Contains(term)));
        }

        return Ok(await query
            .OrderBy(d => d.Name)
            .ThenBy(d => d.Id)
            .Select(DepotDto.Projection)
            .ToPageAsync(page, pageSize, ct));
    }

    /// <summary>A DepotManager account that can be assigned to a depot.</summary>
    public sealed record DepotManagerOption(int Id, string Name, string? Email);

    /// <summary>
    /// GET /api/depots/managers — the DepotManager accounts, for the "assign manager" picker
    /// (the full user list is Admin-only; this exposes only id, name and email).
    /// </summary>
    [HttpGet("managers")]
    [Authorize(Policy = Policies.DepotManager)]
    [ProducesResponseType<IReadOnlyList<DepotManagerOption>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DepotManagerOption>>> GetManagers(CancellationToken ct)
        => Ok(await context.Users.AsNoTracking()
            .Where(u => u.Role == Roles.DepotManager)
            .OrderBy(u => u.Name)
            .Select(u => new DepotManagerOption(u.Id, u.Name, u.Email))
            .ToListAsync(ct));

    /// <summary>GET /api/depots/{id}</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<DepotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepotDto>> GetDepot(int id, CancellationToken ct)
        => Ok(await FindDtoAsync(id, ct));

    /// <summary>POST /api/depots — adds a depot, optionally with its DepotManager.</summary>
    [HttpPost]
    [Authorize(Policy = Policies.DepotManager)]
    [ProducesResponseType<DepotDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DepotDto>> CreateDepot(CreateDepotDto dto, CancellationToken ct)
    {
        await EnsureManagerAsync(dto.ManagerId, ct);
        var depot = new Depot
        {
            Name = dto.Name.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            ManagerId = dto.ManagerId
        };

        context.Depots.Add(depot);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Depot {DepotId} created by user {UserId} (manager {ManagerId})",
            depot.Id, User.GetUserId(), depot.ManagerId);
        return CreatedAtAction(nameof(GetDepot), new { id = depot.Id }, await FindDtoAsync(depot.Id, ct));
    }

    /// <summary>
    /// PUT /api/depots/{id} — edits a depot's name and position and assigns (or with
    /// <c>managerId: null</c> clears) its DepotManager. The manager must be a DepotManager account.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.DepotManager)]
    [ProducesResponseType<DepotDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DepotDto>> UpdateDepot(int id, UpdateDepotDto dto, CancellationToken ct)
    {
        var depot = await context.Depots.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new NotFoundException($"Depot with ID {id} was not found.");
        await EnsureManagerAsync(dto.ManagerId, ct);

        depot.Name = dto.Name.Trim();
        depot.Latitude = dto.Latitude;
        depot.Longitude = dto.Longitude;
        depot.ManagerId = dto.ManagerId;
        await context.SaveChangesAsync(ct);

        logger.LogInformation("Depot {DepotId} updated by user {UserId} (manager {ManagerId})",
            depot.Id, User.GetUserId(), depot.ManagerId);
        return Ok(await FindDtoAsync(id, ct));
    }

    private async Task<DepotDto> FindDtoAsync(int id, CancellationToken ct)
        => await context.Depots.AsNoTracking().Where(d => d.Id == id).Select(DepotDto.Projection).FirstOrDefaultAsync(ct)
           ?? throw new NotFoundException($"Depot with ID {id} was not found.");

    private async Task EnsureManagerAsync(int? managerId, CancellationToken ct)
    {
        if (managerId is null)
            return;
        var role = await context.Users.Where(u => u.Id == managerId).Select(u => u.Role).FirstOrDefaultAsync(ct);
        if (role != Roles.DepotManager)
            throw new BadRequestException($"User {managerId} is not a DepotManager account.");
    }
}
