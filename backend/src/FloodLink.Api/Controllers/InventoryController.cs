using FloodLink.Api.DTOs;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(AppDbContext context, StockReservationService stock) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InventoryItem>>> GetInventory(
        [FromQuery] int? depotId,
        CancellationToken ct)
    {
        IQueryable<InventoryItem> query = context.InventoryItems
            .Include(item => item.Depot)
            .AsNoTracking();

        if (depotId.HasValue)
            query = query.Where(item => item.DepotId == depotId.Value);

        return Ok(await query
            .OrderBy(item => item.Depot!.Name)
            .ThenBy(item => item.ItemName)
            .ToListAsync(ct));
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<InventoryItem>> CreateItem(CreateInventoryItemDto dto, CancellationToken ct)
    {
        if (!await context.Depots.AnyAsync(depot => depot.Id == dto.DepotId, ct))
            throw new BadRequestException($"Depot with ID {dto.DepotId} does not exist.");

        var itemName = dto.ItemName.Trim();
        if (await context.InventoryItems.AnyAsync(i => i.DepotId == dto.DepotId && i.ItemName == itemName, ct))
            return Conflict(new
            {
                error = "DUPLICATE_ITEM",
                message = $"Depot {dto.DepotId} already stocks {itemName}; use check-in to add quantity."
            });

        var item = new InventoryItem
        {
            DepotId = dto.DepotId,
            ItemName = itemName,
            Unit = dto.Unit.Trim(),
            QuantityAvailable = dto.QuantityAvailable,
            ReorderThreshold = dto.ReorderThreshold
        };

        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetInventory), new { depotId = item.DepotId }, item);
    }

    [HttpPut("{id:int}/check-in")]
    [Authorize]
    public async Task<ActionResult<InventoryItem>> CheckIn(int id, StockCheckInDto dto, CancellationToken ct)
    {
        var item = await context.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException($"Inventory item with ID {id} was not found.");

        item.QuantityAvailable += dto.QuantityReceived;
        item.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Ok(item);
    }

    /// <summary>
    /// Holds stock of an item so plans can't allocate it (e.g. kept back for a known need).
    /// Fails with 409 when not enough stock is free, or when another request changed the item first.
    /// </summary>
    [HttpPut("{id:int}/reserve")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reserve(int id, StockQuantityDto dto, CancellationToken ct)
        => ToResult(await stock.ReserveItemAsync(id, dto.Quantity, ct));

    /// <summary>Releases previously held stock of an item back to free stock.</summary>
    [HttpPut("{id:int}/release")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Release(int id, StockQuantityDto dto, CancellationToken ct)
        => ToResult(await stock.ReleaseItemAsync(id, dto.Quantity, ct));

    private IActionResult ToResult((StockOperationResult Result, InventoryItem? Item) outcome)
    {
        if (outcome.Result.Succeeded)
            return Ok(outcome.Item);

        var body = new { error = outcome.Result.ErrorCode, message = outcome.Result.ErrorMessage };
        return outcome.Result.ErrorCode == "NOT_FOUND" ? NotFound(body) : Conflict(body);
    }
}
