using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FloodLink.Infrastructure;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AllocationsController : ControllerBase
{
    private readonly AppDbContext _context;

    public AllocationsController(AppDbContext context)
    {
        _context = context;
    }


    // ============================================================
    // GET: api/allocations
    // Supports:
    // ?status=Proposed
    // ?shelterId=1
    // ?status=Proposed&shelterId=1
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> GetAllocations(
        [FromQuery] string? status,
        [FromQuery] int? shelterId)
    {
        var query = _context.AllocationProposals
            .AsNoTracking()
            .AsQueryable();

        // Filter by status
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a =>
                a.Status == status);
        }

        // Filter by shelter
        if (shelterId.HasValue)
        {
            query = query.Where(a =>
                a.ShelterId == shelterId.Value);
        }

        var allocations = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Ok(allocations);
    }


    // ============================================================
    // POST: api/allocations/{id}/trigger-matching
    // Trigger the Logistics / Matching Agent
    // ============================================================
    [HttpPost("{id}/trigger-matching")]
    public async Task<IActionResult> TriggerMatching(int id)
    {
        // Check whether allocation exists
        var allocation = await _context.AllocationProposals
            .FirstOrDefaultAsync(a => a.Id == id);

        if (allocation == null)
        {
            return NotFound(new
            {
                message = "Allocation proposal not found."
            });
        }

        // Only proposed allocations should trigger matching
        if (!string.Equals(
                allocation.Status,
                "Proposed",
                StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message =
                    "Matching can only be triggered for Proposed allocations.",

                currentStatus = allocation.Status
            });
        }

        // TODO:
        // Day 4 - Call Logistics / Matching Agent here.

        return Ok(new
        {
            message = "Matching triggered successfully.",

            allocationId = allocation.Id,

            shelterId = allocation.ShelterId,

            itemName = allocation.ItemName,

            requestedQuantity = allocation.Quantity,

            status = allocation.Status
        });
    }
}