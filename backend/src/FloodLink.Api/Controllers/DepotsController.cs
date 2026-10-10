using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/depots")]
public sealed class DepotsController(AppDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Depot>>> GetDepots(CancellationToken ct)
        => Ok(await context.Depots.AsNoTracking().OrderBy(depot => depot.Name).ToListAsync(ct));

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Depot>> CreateDepot(CreateDepotDto dto, CancellationToken ct)
    {
        var depot = new Depot
        {
            Name = dto.Name.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude
        };

        context.Depots.Add(depot);
        await context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetDepots), new { id = depot.Id }, depot);
    }
}
