using FloodLink.Contracts.Depots;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/depots")]
public class DepotsController : ControllerBase
{
    private readonly AppDbContext _db;

    public DepotsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDepotRequest request)
    {
        var depot = new Depot
        {
            Name = request.Name,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ManagerId = request.ManagerId,
            CreatedAt = DateTime.UtcNow
        };

        _db.Depots.Add(depot);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { id = depot.Id },
            depot);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var depot = await _db.Depots
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id);

        if (depot == null)
            return NotFound();

        return Ok(depot);
    }

    [HttpGet]
public async Task<IActionResult> GetAll(
    string? search,
    int page = 1,
    int pageSize = 10)
{
    var query = _db.Depots
        .AsNoTracking()
        .AsQueryable();

    if (!string.IsNullOrWhiteSpace(search))
    {
        query = query.Where(d =>
            d.Name.ToLower().Contains(search.ToLower()));
    }

    var total = await query.CountAsync();

    var depots = await query
        .OrderBy(d => d.Name)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return Ok(new
    {
        total,
        page,
        pageSize,
        data = depots
    });
}
}