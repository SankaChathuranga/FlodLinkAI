using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/inventory")]
public sealed class InventoryController(AppDbContext context) : ControllerBase
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
    public async Task<ActionResult<InventoryItem>> CreateItem(CreateInventoryItemDto dto, CancellationToken ct)
    {
        if (!await context.Depots.AnyAsync(depot => depot.Id == dto.DepotId, ct))
            throw new BadRequestException($"Depot with ID {dto.DepotId} does not exist.");

        var item = new InventoryItem
        {
            DepotId = dto.DepotId,
            ItemName = dto.ItemName.Trim(),
            Unit = dto.Unit.Trim(),
            QuantityAvailable = dto.QuantityAvailable
        };

        context.InventoryItems.Add(item);
        await context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetInventory), new { depotId = item.DepotId }, item);
    }

    [HttpPut("{id:int}/check-in")]
    public async Task<ActionResult<InventoryItem>> CheckIn(int id, StockCheckInDto dto, CancellationToken ct)
    {
        var item = await context.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException($"Inventory item with ID {id} was not found.");

        item.QuantityAvailable += dto.QuantityReceived;
        item.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Ok(item);
    }
}
