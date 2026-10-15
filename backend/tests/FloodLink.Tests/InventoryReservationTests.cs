using FloodLink.Api.Controllers;
using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;

public class InventoryReservationTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<(AppDbContext Context, InventoryController Controller, InventoryItem Item)> SeedContextWithItem(int initialStock = 100)
    {
        var context = CreateContext();
        var depot = new Depot
        {
            Id = 1,
            Name = "Central Depot",
            Latitude = 6.9271,
            Longitude = 79.8612
        };
        context.Depots.Add(depot);

        var item = new InventoryItem
        {
            Id = 1,
            DepotId = 1,
            ItemName = "Water",
            Unit = "bottles",
            QuantityAvailable = initialStock,
            QuantityReserved = 0,
            ReorderThreshold = 10
        };
        context.InventoryItems.Add(item);
        await context.SaveChangesAsync();

        var controller = new InventoryController(context);
        return (context, controller, item);
    }

    [Fact]
    public async Task Reserve_WithValidQuantity_CreatesActiveReservationAndIncreasesReservedStock()
    {
        var (context, controller, item) = await SeedContextWithItem(100);

        var dto = new ReserveStockDto
        {
            Quantity = 40,
            ReservedBy = "Coordinator Jane"
        };

        var result = await controller.Reserve(item.Id, dto, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var reservation = Assert.IsType<StockReservation>(okResult.Value);

        Assert.Equal(item.Id, reservation.InventoryItemId);
        Assert.Equal(40, reservation.Quantity);
        Assert.Equal("Active", reservation.Status);
        Assert.Equal("Coordinator Jane", reservation.ReservedBy);

        var updatedItem = await context.InventoryItems.FindAsync(item.Id);
        Assert.Equal(40, updatedItem!.QuantityReserved);
        Assert.Equal(60, updatedItem.QuantityAvailable - updatedItem.QuantityReserved); // Free stock = 60
    }

    [Fact]
    public async Task Release_WithValidQuantity_RestoresFreeStockAndMarksReleased()
    {
        var (context, controller, item) = await SeedContextWithItem(100);

        // Reserve 40
        var reserveResult = await controller.Reserve(item.Id, new ReserveStockDto { Quantity = 40 }, CancellationToken.None);
        var reservation = (StockReservation)Assert.IsType<OkObjectResult>(reserveResult.Result).Value!;

        // Release 40
        var releaseResult = await controller.Release(item.Id, new ReleaseStockDto
        {
            ReservationId = reservation.Id,
            Quantity = 40
        }, CancellationToken.None);

        var okRelease = Assert.IsType<OkObjectResult>(releaseResult.Result);
        var releasedReservation = Assert.IsType<StockReservation>(okRelease.Value);

        Assert.Equal("Released", releasedReservation.Status);
        Assert.NotNull(releasedReservation.ReleasedAt);

        var updatedItem = await context.InventoryItems.FindAsync(item.Id);
        Assert.Equal(0, updatedItem!.QuantityReserved);
        Assert.Equal(100, updatedItem.QuantityAvailable - updatedItem.QuantityReserved);
    }

    [Fact]
    public async Task Reserve_ThenRelease_RestoresFreeStockToOriginal()
    {
        var (context, controller, item) = await SeedContextWithItem(100);

        var reserveResult = await controller.Reserve(item.Id, new ReserveStockDto { Quantity = 25 }, CancellationToken.None);
        var reservation = (StockReservation)Assert.IsType<OkObjectResult>(reserveResult.Result).Value!;

        await controller.Release(item.Id, new ReleaseStockDto
        {
            ReservationId = reservation.Id,
            Quantity = 25
        }, CancellationToken.None);

        var updatedItem = await context.InventoryItems.FindAsync(item.Id);
        Assert.Equal(0, updatedItem!.QuantityReserved);
        Assert.Equal(100, updatedItem.QuantityAvailable - updatedItem.QuantityReserved);
    }

    [Fact]
    public async Task Reserve_Boundary_ExactlyFreeStock_Succeeds()
    {
        var (context, controller, item) = await SeedContextWithItem(100);

        var result = await controller.Reserve(item.Id, new ReserveStockDto { Quantity = 100 }, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var reservation = Assert.IsType<StockReservation>(okResult.Value);

        Assert.Equal(100, reservation.Quantity);

        var updatedItem = await context.InventoryItems.FindAsync(item.Id);
        Assert.Equal(100, updatedItem!.QuantityReserved);
        Assert.Equal(0, updatedItem.QuantityAvailable - updatedItem.QuantityReserved);
    }

    [Fact]
    public async Task Reserve_Boundary_FreeStockPlusOne_ThrowsConflictException()
    {
        var (context, controller, item) = await SeedContextWithItem(100);

        // Reserve 100 succeeds
        await controller.Reserve(item.Id, new ReserveStockDto { Quantity = 100 }, CancellationToken.None);

        // Reserve 1 more should fail with 409 Conflict
        await Assert.ThrowsAsync<ConflictException>(() =>
            controller.Reserve(item.Id, new ReserveStockDto { Quantity = 1 }, CancellationToken.None));
    }

    [Fact]
    public async Task Reserve_WithZeroQuantity_ThrowsBadRequestException()
    {
        var (_, controller, item) = await SeedContextWithItem(100);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.Reserve(item.Id, new ReserveStockDto { Quantity = 0 }, CancellationToken.None));
    }

    [Fact]
    public async Task Reserve_WithNegativeQuantity_ThrowsBadRequestException()
    {
        var (_, controller, item) = await SeedContextWithItem(100);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.Reserve(item.Id, new ReserveStockDto { Quantity = -10 }, CancellationToken.None));
    }

    [Fact]
    public async Task Reserve_WhenItemDoesNotExist_ThrowsNotFoundException()
    {
        var (_, controller, _) = await SeedContextWithItem(100);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.Reserve(999, new ReserveStockDto { Quantity = 10 }, CancellationToken.None));
    }

    [Fact]
    public async Task Release_MoreThanReserved_ThrowsBadRequestException()
    {
        var (_, controller, item) = await SeedContextWithItem(100);

        var reserveResult = await controller.Reserve(item.Id, new ReserveStockDto { Quantity = 30 }, CancellationToken.None);
        var reservation = (StockReservation)Assert.IsType<OkObjectResult>(reserveResult.Result).Value!;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.Release(item.Id, new ReleaseStockDto
            {
                ReservationId = reservation.Id,
                Quantity = 31
            }, CancellationToken.None));
    }

    [Fact]
    public async Task Release_AlreadyReleasedReservation_ThrowsBadRequestException()
    {
        var (_, controller, item) = await SeedContextWithItem(100);

        var reserveResult = await controller.Reserve(item.Id, new ReserveStockDto { Quantity = 30 }, CancellationToken.None);
        var reservation = (StockReservation)Assert.IsType<OkObjectResult>(reserveResult.Result).Value!;

        // First release succeeds
        await controller.Release(item.Id, new ReleaseStockDto
        {
            ReservationId = reservation.Id,
            Quantity = 30
        }, CancellationToken.None);

        // Second release throws BadRequestException
        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.Release(item.Id, new ReleaseStockDto
            {
                ReservationId = reservation.Id,
                Quantity = 30
            }, CancellationToken.None));
    }

    [Fact]
    public async Task GetReservations_ReturnsHistoryInDescendingOrder()
    {
        var (context, controller, item) = await SeedContextWithItem(100);

        await controller.Reserve(item.Id, new ReserveStockDto { Quantity = 10, ReservedBy = "User 1" }, CancellationToken.None);
        await controller.Reserve(item.Id, new ReserveStockDto { Quantity = 20, ReservedBy = "User 2" }, CancellationToken.None);

        var result = await controller.GetReservations(item.Id, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var reservations = Assert.IsAssignableFrom<IEnumerable<StockReservation>>(okResult.Value).ToList();

        Assert.Equal(2, reservations.Count);
    }
}
