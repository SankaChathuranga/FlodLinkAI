using FloodLink.Api.DTOs;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using FloodLink.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Controllers;

/// <summary>Depot stock (Member B). Stock changes go through <see cref="StockReservationService"/>.</summary>
[ApiController]
[Route("api/inventory")]
[Authorize(Policy = Policies.StockReader)]
public sealed class InventoryController(AppDbContext context, StockReservationService stock) : ControllerBase
{
    /// <summary>
    /// GET /api/inventory?depotId=&amp;managedByMe=&amp;search=&amp;lowStock=&amp;sort=&amp;page=&amp;pageSize=
    /// One page of stock lines. <c>search</c> matches item or depot name; <c>lowStock=true</c> keeps
    /// lines whose free stock (available − reserved) is at or below the reorder threshold.
    /// <c>sort</c>: depot (default), item, quantity_asc, quantity_desc, updated. <c>managedByMe=true</c>
    /// keeps the depots whose manager is the signed-in user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PaginatedResult<InventoryItem>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginatedResult<InventoryItem>>> GetInventory(
        [FromQuery] int? depotId,
        [FromQuery] bool? managedByMe,
        [FromQuery] string? search,
        [FromQuery] bool? lowStock,
        [FromQuery] string? sort,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        IQueryable<InventoryItem> query = context.InventoryItems
            .Include(item => item.Depot)
            .AsNoTracking();

        if (depotId.HasValue)
            query = query.Where(item => item.DepotId == depotId.Value);

        if (managedByMe == true)
        {
            var userId = User.GetUserId();
            query = query.Where(item => item.Depot!.ManagerId == userId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(item => item.ItemName.ToLower().Contains(term) || item.Depot!.Name.ToLower().Contains(term));
        }

        if (lowStock == true)
            query = query.Where(item => item.QuantityAvailable - item.QuantityReserved <= item.ReorderThreshold);

        query = (sort?.Trim().ToLower()) switch
        {
            "item" => query.OrderBy(item => item.ItemName).ThenBy(item => item.Depot!.Name),
            "quantity_asc" => query.OrderBy(item => item.QuantityAvailable - item.QuantityReserved).ThenBy(item => item.Id),
            "quantity_desc" => query.OrderByDescending(item => item.QuantityAvailable - item.QuantityReserved).ThenBy(item => item.Id),
            "updated" => query.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Id),
            _ => query.OrderBy(item => item.Depot!.Name).ThenBy(item => item.ItemName)
        };

        return Ok(await query.ToPageAsync(page, pageSize, ct));
    }

    [HttpPost]
    [Authorize(Policy = Policies.DepotManager)]
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
        // The opening quantity is the line's first movement (saved together with the line).
        context.StockMovements.Add(new StockMovement
        {
            InventoryItem = item,
            Kind = StockMovementKinds.Opening,
            Quantity = item.QuantityAvailable,
            AvailableAfter = item.QuantityAvailable,
            UserId = User.GetUserId()
        });
        await context.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
    }

    /// <summary>
    /// GET /api/inventory/{id} — one stock line with its depot. Used by the app after scanning a stock
    /// label's QR code (8.06), whose text is <c>FLI:{id}</c>.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<InventoryItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryItem>> GetItem(int id, CancellationToken ct)
        => await context.InventoryItems.Include(i => i.Depot).AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct)
           ?? throw new NotFoundException($"Inventory item with ID {id} was not found.");

    /// <summary>One stock movement as the history view shows it.</summary>
    public sealed record StockMovementDto(
        long Id,
        int InventoryItemId,
        string ItemName,
        string Unit,
        int DepotId,
        string? DepotName,
        string Kind,
        double Quantity,
        double AvailableAfter,
        double ReservedAfter,
        Guid? WorkflowRunId,
        int? UserId,
        string? UserName,
        string? Note,
        DateTime CreatedAt);

    /// <summary>
    /// GET /api/inventory/movements?inventoryItemId=&amp;depotId=&amp;kind=&amp;workflowRunId=&amp;dateFrom=&amp;dateTo=&amp;page=&amp;pageSize=
    /// The stock history (8.03), newest first: every check-in, hold, release, plan reserve/commit/release
    /// and physical count, with the line's stock after each change and who or which plan caused it.
    /// </summary>
    [HttpGet("movements")]
    [ProducesResponseType<PaginatedResult<StockMovementDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMovements(
        [FromQuery] int? inventoryItemId,
        [FromQuery] int? depotId,
        [FromQuery] string? kind,
        [FromQuery] Guid? workflowRunId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        IQueryable<StockMovement> query = context.StockMovements.AsNoTracking();

        if (inventoryItemId.HasValue)
            query = query.Where(m => m.InventoryItemId == inventoryItemId.Value);
        if (depotId.HasValue)
            query = query.Where(m => m.InventoryItem!.DepotId == depotId.Value);
        if (!string.IsNullOrWhiteSpace(kind))
        {
            var match = StockMovementKinds.All.FirstOrDefault(k => string.Equals(k, kind.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null)
                return BadRequest(new { error = "INVALID_KIND", message = $"Kind must be one of: {string.Join(", ", StockMovementKinds.All)}." });
            query = query.Where(m => m.Kind == match);
        }
        if (workflowRunId.HasValue)
            query = query.Where(m => m.WorkflowRunId == workflowRunId.Value);
        if (dateFrom.HasValue)
            query = query.Where(m => m.CreatedAt >= dateFrom.Value.AsUtc());
        if (dateTo.HasValue)
            query = query.Where(m => m.CreatedAt <= dateTo.Value.AsUtc());

        return Ok(await query
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Select(m => new StockMovementDto(
                m.Id, m.InventoryItemId, m.InventoryItem!.ItemName, m.InventoryItem.Unit, m.InventoryItem.DepotId,
                m.InventoryItem.Depot!.Name, m.Kind, m.Quantity, m.AvailableAfter, m.ReservedAfter, m.WorkflowRunId,
                m.UserId, m.User != null ? m.User.Name : null, m.Note, m.CreatedAt))
            .ToPageAsync(page, pageSize, ct));
    }

    /// <summary>Adds a delivery of stock to an item (depot staff check-in).</summary>
    [HttpPut("{id:int}/check-in")]
    [Authorize(Policy = Policies.DepotManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(int id, StockCheckInDto dto, CancellationToken ct)
        => ToResult(await stock.CheckInAsync(id, dto.QuantityReceived, User.GetUserId(), ct));

    /// <summary>
    /// Holds stock of an item so plans can't allocate it (e.g. kept back for a known need).
    /// Fails with 409 when not enough stock is free, or when another request changed the item first.
    /// </summary>
    [HttpPut("{id:int}/reserve")]
    [Authorize(Policy = Policies.DepotManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reserve(int id, StockQuantityDto dto, CancellationToken ct)
        => ToResult(await stock.ReserveItemAsync(id, dto.Quantity, User.GetUserId(), ct));

    /// <summary>Releases previously held stock of an item back to free stock.</summary>
    [HttpPut("{id:int}/release")]
    [Authorize(Policy = Policies.DepotManager)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Release(int id, StockQuantityDto dto, CancellationToken ct)
        => ToResult(await stock.ReleaseItemAsync(id, dto.Quantity, User.GetUserId(), ct));

    private IActionResult ToResult((StockOperationResult Result, InventoryItem? Item) outcome)
    {
        if (outcome.Result.Succeeded)
            return Ok(outcome.Item);

        var body = new { error = outcome.Result.ErrorCode, message = outcome.Result.ErrorMessage };
        return outcome.Result.ErrorCode == "NOT_FOUND" ? NotFound(body) : Conflict(body);
    }
}
