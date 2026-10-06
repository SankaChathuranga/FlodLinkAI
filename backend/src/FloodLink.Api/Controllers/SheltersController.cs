using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/shelters")]
public class SheltersController : ControllerBase
{
    private readonly AppDbContext _context;

    public SheltersController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/shelters?status=&amp;search=&amp;page=&amp;pageSize=
    /// Searches, filters, and paginates emergency shelters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<Shelter>>> GetShelters(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        IQueryable<Shelter> query = _context.Shelters.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(s => s.Status.ToLower() == status.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string term = search.Trim().ToLower();
            query = query.Where(s => s.Name.ToLower().Contains(term));
        }

        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        List<Shelter> items = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PaginatedResult<Shelter>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };

        return Ok(result);
    }

    /// <summary>
    /// GET /api/shelters/{id}
    /// Retrieves a specific shelter along with its full report history.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Shelter>> GetShelterById(int id)
    {
        Shelter? shelter = await _context.Shelters
            .Include(s => s.Reports)
            .Include(s => s.ContactVolunteer)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id);

        if (shelter == null)
        {
            throw new NotFoundException($"Shelter with ID {id} was not found.");
        }

        return Ok(shelter);
    }

    /// <summary>
    /// POST /api/shelters
    /// Creates a new emergency shelter.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Shelter>> CreateShelter([FromBody] CreateShelterDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (dto.ContactVolunteerId.HasValue)
        {
            bool userExists = await _context.Users.AnyAsync(u => u.Id == dto.ContactVolunteerId.Value);
            if (!userExists)
            {
                throw new BadRequestException($"User with ID {dto.ContactVolunteerId.Value} does not exist.");
            }
        }

        var shelter = new Shelter
        {
            Name = dto.Name,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Capacity = dto.Capacity,
            CurrentOccupancy = dto.CurrentOccupancy,
            ContactVolunteerId = dto.ContactVolunteerId,
            Status = dto.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Shelters.Add(shelter);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetShelterById), new { id = shelter.Id }, shelter);
    }

    /// <summary>
    /// PUT /api/shelters/{id}
    /// Updates an existing shelter's details.
    /// </summary>
    [HttpPut("{id:int}")]
    public async Task<ActionResult<Shelter>> UpdateShelter(int id, [FromBody] UpdateShelterDto dto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        Shelter? shelter = await _context.Shelters.FindAsync(id);
        if (shelter == null)
        {
            throw new NotFoundException($"Shelter with ID {id} was not found.");
        }

        if (dto.ContactVolunteerId.HasValue)
        {
            bool userExists = await _context.Users.AnyAsync(u => u.Id == dto.ContactVolunteerId.Value);
            if (!userExists)
            {
                throw new BadRequestException($"User with ID {dto.ContactVolunteerId.Value} does not exist.");
            }
        }

        shelter.Name = dto.Name;
        shelter.Latitude = dto.Latitude;
        shelter.Longitude = dto.Longitude;
        shelter.Capacity = dto.Capacity;
        shelter.CurrentOccupancy = dto.CurrentOccupancy;
        shelter.ContactVolunteerId = dto.ContactVolunteerId;
        shelter.Status = dto.Status;
        shelter.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(shelter);
    }
}
