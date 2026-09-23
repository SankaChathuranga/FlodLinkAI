using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DepotsController : ControllerBase
{
    private readonly AppDbContext _context;

    public DepotsController(AppDbContext context)
    {
        _context = context;
    }

    // POST: api/depots
    [HttpPost]
    public async Task<IActionResult> CreateDepot([FromBody] Depot depot)
    {
        depot.CreatedAt = DateTime.UtcNow;

        _context.Depots.Add(depot);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetDepot),
            new { id = depot.Id },
            depot);
    }

    // GET: api/depots/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetDepot(int id)
    {
        var depot = await _context.Depots
            .FirstOrDefaultAsync(d => d.Id == id);

        if (depot == null)
            return NotFound(new { message = "Depot not found." });

        return Ok(depot);
    }

    // GET: api/depots
    [HttpGet]
    public async Task<IActionResult> GetDepots(
        [FromQuery] string? search,
        [FromQuery] int page = 1)
    {
        const int pageSize = 10;

        if (page < 1)
            page = 1;

        var query = _context.Depots.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(d =>
                d.Name.ToLower().Contains(search.ToLower()));
        }

        var totalCount = await query.CountAsync();

        var depots = await query
            .OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
            data = depots
        });
    }
}