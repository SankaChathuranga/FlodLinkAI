using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using System.Data;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InventoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public InventoryController(AppDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // POST: api/inventory
    // Add new inventory item
    // ============================================================
    [HttpPost]
    public async Task<IActionResult> AddInventory(
        [FromBody] InventoryItem item)
    {
        if (item.QuantityAvailable < 0)
        {
            return BadRequest(new
            {
                message = "Quantity available cannot be negative."
            });
        }

        if (item.QuantityReserved < 0)
        {
            return BadRequest(new
            {
                message = "Quantity reserved cannot be negative."
            });
        }

        if (item.QuantityReserved > item.QuantityAvailable)
        {
            return BadRequest(new
            {
                message = "Reserved quantity cannot exceed available quantity."
            });
        }

        item.UpdatedAt = DateTime.UtcNow;

        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetInventoryItem),
            new { id = item.Id },
            item);
    }


    // ============================================================
    // GET: api/inventory/{id}
    // Get one inventory item
    // ============================================================
    [HttpGet("{id}")]
    public async Task<IActionResult> GetInventoryItem(int id)
    {
        var item = await _context.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            return NotFound(new
            {
                message = "Inventory item not found."
            });
        }

        return Ok(item);
    }


    // ============================================================
    // GET: api/inventory
    // Supports:
    // ?depotId=1
    // ?lowStock=true
    // ?sort=quantity
    // ?sort=quantity_desc
    // ?sort=name
    // ?sort=name_desc
    // ============================================================
    [HttpGet]
    public async Task<IActionResult> GetInventory(
        [FromQuery] int? depotId,
        [FromQuery] bool lowStock = false,
        [FromQuery] string? sort = null)
    {
        var query = _context.InventoryItems
            .AsNoTracking()
            .AsQueryable();

        // Filter by depot
        if (depotId.HasValue)
        {
            query = query.Where(i =>
                i.DepotId == depotId.Value);
        }

        // Filter low-stock items
        if (lowStock)
        {
            query = query.Where(i =>
                i.QuantityAvailable - i.QuantityReserved
                <= i.ReorderThreshold);
        }

        // Sorting
        query = sort?.ToLower() switch
        {
            "quantity" =>
                query.OrderBy(i => i.QuantityAvailable),

            "quantity_desc" =>
                query.OrderByDescending(i => i.QuantityAvailable),

            "name" =>
                query.OrderBy(i => i.ItemName),

            "name_desc" =>
                query.OrderByDescending(i => i.ItemName),

            _ =>
                query.OrderBy(i => i.ItemName)
        };

        var items = await query.ToListAsync();

        return Ok(items);
    }


    // ============================================================
    // PUT: api/inventory/{id}/reserve
    // Reserve inventory quantity
    // ============================================================
    [HttpPut("{id}/reserve")]
    public async Task<IActionResult> ReserveStock(
        int id,
        [FromBody] ReserveInventoryRequest request)
    {
        // Validate request
        if (request.Quantity <= 0)
        {
            return BadRequest(new
            {
                message = "Reservation quantity must be greater than zero."
            });
        }

        // Start transaction with Serializable isolation
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            // Get inventory item
            var item = await _context.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound(new
                {
                    message = "Inventory item not found."
                });
            }

            // Calculate currently available stock
            var availableQuantity =
                item.QuantityAvailable - item.QuantityReserved;

            // Check whether enough stock exists
            if (request.Quantity > availableQuantity)
            {
                return Conflict(new
                {
                    message = "Insufficient available stock.",
                    availableQuantity = availableQuantity,
                    requestedQuantity = request.Quantity
                });
            }

            // Reserve the quantity
            item.QuantityReserved += request.Quantity;

            item.UpdatedAt = DateTime.UtcNow;

            // Save changes
            await _context.SaveChangesAsync();

            // Commit transaction
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Stock reserved successfully.",

                inventoryItemId = item.Id,

                itemName = item.ItemName,

                reservedQuantity = request.Quantity,

                totalReserved = item.QuantityReserved,

                remainingAvailable =
                    item.QuantityAvailable -
                    item.QuantityReserved
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }


    // ============================================================
    // PUT: api/inventory/{id}/release
    // Release previously reserved inventory
    // ============================================================
    [HttpPut("{id}/release")]
    public async Task<IActionResult> ReleaseStock(
        int id,
        [FromBody] ReserveInventoryRequest request)
    {
        // Validate request
        if (request.Quantity <= 0)
        {
            return BadRequest(new
            {
                message = "Release quantity must be greater than zero."
            });
        }

        // Start transaction
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            // Find inventory item
            var item = await _context.InventoryItems
                .FirstOrDefaultAsync(i => i.Id == id);

            if (item == null)
            {
                return NotFound(new
                {
                    message = "Inventory item not found."
                });
            }

            // Cannot release more than currently reserved
            if (request.Quantity > item.QuantityReserved)
            {
                return BadRequest(new
                {
                    message =
                        "Cannot release more than the reserved quantity.",

                    currentReserved =
                        item.QuantityReserved,

                    requestedRelease =
                        request.Quantity
                });
            }

            // Release reservation
            item.QuantityReserved -= request.Quantity;

            item.UpdatedAt = DateTime.UtcNow;

            // Save changes
            await _context.SaveChangesAsync();

            // Commit transaction
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Reservation released successfully.",

                inventoryItemId = item.Id,

                releasedQuantity = request.Quantity,

                remainingReserved =
                    item.QuantityReserved,

                remainingAvailable =
                    item.QuantityAvailable -
                    item.QuantityReserved
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}


// ================================================================
// Request DTO for Reserve / Release
// ================================================================
public class ReserveInventoryRequest
{
    public decimal Quantity { get; set; }
}