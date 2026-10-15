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
        if (dto.QuantityReceived <= 0)
            throw new BadRequestException("Quantity received must be greater than zero.");

        var item = await context.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException($"Inventory item with ID {id} was not found.");

        item.QuantityAvailable += dto.QuantityReceived;
        item.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpPut("{id:int}/reserve")]
    [ProducesResponseType(typeof(StockReservation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StockReservation>> Reserve(int id, ReserveStockDto dto, CancellationToken ct)
    {
        if (dto.Quantity <= 0)
            throw new BadRequestException("Reservation quantity must be greater than zero.");

        // Concurrency Strategy:
        // For relational databases (PostgreSQL), we execute an atomic, conditional ExecuteUpdateAsync inside a transaction
        // with the predicate: WHERE Id == id AND (QuantityAvailable - QuantityReserved >= dto.Quantity).
        // Why this choice:
        // 1. PostgreSQL guarantees atomic evaluation and row-level locking during the single conditional UPDATE statement.
        // 2. Competing requests for the same free stock cannot cause an over-allocation race condition:
        //    exactly one request succeeds in updating the row count (rowsAffected = 1), while the losing request sees rowsAffected = 0.
        // 3. The StockReservation audit row is written within the exact same database transaction prior to commit.
        // 4. For non-relational providers (e.g. EF Core InMemory used by controller unit tests), we fall back to tracked entity evaluation.
        if (context.Database.IsRelational())
        {
            await using var tx = await context.Database.BeginTransactionAsync(ct);

            var rowsAffected = await context.InventoryItems
                .Where(i => i.Id == id && (i.QuantityAvailable - i.QuantityReserved >= dto.Quantity))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.QuantityReserved, i => i.QuantityReserved + dto.Quantity)
                    .SetProperty(i => i.UpdatedAt, DateTime.UtcNow), ct);

            if (rowsAffected == 0)
            {
                var exists = await context.InventoryItems.AnyAsync(i => i.Id == id, ct);
                if (!exists)
                    throw new NotFoundException($"Inventory item with ID {id} was not found.");

                throw new ConflictException("Insufficient available stock to fulfill reservation.");
            }

            var reservation = new StockReservation
            {
                InventoryItemId = id,
                AllocationProposalId = dto.AllocationProposalId,
                Quantity = dto.Quantity,
                Status = "Active",
                ReservedBy = dto.ReservedBy ?? string.Empty,
                ReservedAt = DateTime.UtcNow
            };

            context.StockReservations.Add(reservation);
            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Ok(reservation);
        }
        else
        {
            var item = await context.InventoryItems.FindAsync([id], ct)
                ?? throw new NotFoundException($"Inventory item with ID {id} was not found.");

            if (item.QuantityAvailable - item.QuantityReserved < dto.Quantity)
                throw new ConflictException("Insufficient available stock to fulfill reservation.");

            item.QuantityReserved += dto.Quantity;
            item.UpdatedAt = DateTime.UtcNow;

            var reservation = new StockReservation
            {
                InventoryItemId = id,
                AllocationProposalId = dto.AllocationProposalId,
                Quantity = dto.Quantity,
                Status = "Active",
                ReservedBy = dto.ReservedBy ?? string.Empty,
                ReservedAt = DateTime.UtcNow
            };

            context.StockReservations.Add(reservation);
            await context.SaveChangesAsync(ct);

            return Ok(reservation);
        }
    }

    [HttpPut("{id:int}/release")]
    [ProducesResponseType(typeof(StockReservation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StockReservation>> Release(int id, ReleaseStockDto dto, CancellationToken ct)
    {
        if (dto.Quantity <= 0)
            throw new BadRequestException("Release quantity must be greater than zero.");

        var item = await context.InventoryItems.FindAsync([id], ct)
            ?? throw new NotFoundException($"Inventory item with ID {id} was not found.");

        var reservation = await context.StockReservations.FindAsync([dto.ReservationId], ct)
            ?? throw new NotFoundException($"Stock reservation with ID {dto.ReservationId} was not found.");

        if (reservation.InventoryItemId != id)
            throw new BadRequestException($"Reservation {dto.ReservationId} does not belong to inventory item {id}.");

        if (reservation.Status != "Active")
            throw new BadRequestException("Reservation is not active or has already been released.");

        if (dto.Quantity > reservation.Quantity)
            throw new BadRequestException($"Cannot release {dto.Quantity} units because the reservation only holds {reservation.Quantity} units.");

        if (context.Database.IsRelational())
        {
            await using var tx = await context.Database.BeginTransactionAsync(ct);

            await context.InventoryItems
                .Where(i => i.Id == id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.QuantityReserved, i => Math.Max(0, i.QuantityReserved - dto.Quantity))
                    .SetProperty(i => i.UpdatedAt, DateTime.UtcNow), ct);

            if (dto.Quantity == reservation.Quantity)
            {
                reservation.Status = "Released";
                reservation.ReleasedAt = DateTime.UtcNow;
            }
            else
            {
                reservation.Quantity -= dto.Quantity;
            }

            await context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        else
        {
            item.QuantityReserved = Math.Max(0, item.QuantityReserved - dto.Quantity);
            item.UpdatedAt = DateTime.UtcNow;

            if (dto.Quantity == reservation.Quantity)
            {
                reservation.Status = "Released";
                reservation.ReleasedAt = DateTime.UtcNow;
            }
            else
            {
                reservation.Quantity -= dto.Quantity;
            }

            await context.SaveChangesAsync(ct);
        }

        return Ok(reservation);
    }

    [HttpGet("{id:int}/reservations")]
    [ProducesResponseType(typeof(IEnumerable<StockReservation>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<StockReservation>>> GetReservations(int id, CancellationToken ct)
    {
        if (!await context.InventoryItems.AnyAsync(i => i.Id == id, ct))
            throw new NotFoundException($"Inventory item with ID {id} was not found.");

        var reservations = await context.StockReservations
            .Where(r => r.InventoryItemId == id)
            .OrderByDescending(r => r.ReservedAt)
            .ToListAsync(ct);

        return Ok(reservations);
    }
}
