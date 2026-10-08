using FloodLink.Api.Controllers;
using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FloodLink.Tests;

public class InventoryDepotControllerTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task CreateDepot_WithValidData_CreatesDepot()
    {
        await using var context = CreateContext();
        var controller = new DepotsController(context);

        var dto = new CreateDepotDto
        {
            Name = "Test Relief Depot",
            Latitude = 6.9271,
            Longitude = 79.8612
        };

        var result = await controller.CreateDepot(dto, CancellationToken.None);

        var depot = await context.Depots
            .SingleAsync(d => d.Name == "Test Relief Depot");

        Assert.NotNull(depot);
        Assert.Equal(6.9271, depot.Latitude);
        Assert.Equal(79.8612, depot.Longitude);
    }

    [Fact]
    public async Task GetDepots_ReturnsDepotsOrderedByName()
    {
        await using var context = CreateContext();

        context.Depots.AddRange(
            new Depot
            {
                Name = "Zeta Depot",
                Latitude = 7.0,
                Longitude = 80.0
            },
            new Depot
            {
                Name = "Alpha Depot",
                Latitude = 6.9,
                Longitude = 79.8
            });

        await context.SaveChangesAsync();

        var controller = new DepotsController(context);

        var result = await controller.GetDepots(CancellationToken.None);

        var actionResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);

        var depots = Assert.IsAssignableFrom<IEnumerable<Depot>>(actionResult.Value);

        Assert.Equal(
            new[] { "Alpha Depot", "Zeta Depot" },
            depots.Select(d => d.Name));
    }

    [Fact]
    public async Task CreateInventory_WithValidDepot_CreatesInventoryItem()
    {
        await using var context = CreateContext();

        context.Depots.Add(new Depot
        {
            Id = 1,
            Name = "Main Depot",
            Latitude = 6.9271,
            Longitude = 79.8612
        });

        await context.SaveChangesAsync();

        var controller = new InventoryController(context);

        var dto = new CreateInventoryItemDto
        {
            DepotId = 1,
            ItemName = "Water",
            Unit = "bottles",
            QuantityAvailable = 100
        };

        await controller.CreateItem(dto, CancellationToken.None);

        var item = await context.InventoryItems.SingleAsync();

        Assert.Equal(1, item.DepotId);
        Assert.Equal("Water", item.ItemName);
        Assert.Equal("bottles", item.Unit);
        Assert.Equal(100, item.QuantityAvailable);
    }

    [Fact]
    public async Task CreateInventory_WithNonExistingDepot_ThrowsBadRequestException()
    {
        await using var context = CreateContext();
        var controller = new InventoryController(context);

        var dto = new CreateInventoryItemDto
        {
            DepotId = 999,
            ItemName = "Water",
            Unit = "bottles",
            QuantityAvailable = 100
        };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            controller.CreateItem(dto, CancellationToken.None));
    }

    [Fact]
    public async Task GetInventory_ReturnsAllInventoryItems()
    {
        await using var context = CreateContext();

        context.Depots.AddRange(
            new Depot
            {
                Id = 1,
                Name = "Depot A",
                Latitude = 6.9,
                Longitude = 79.8
            },
            new Depot
            {
                Id = 2,
                Name = "Depot B",
                Latitude = 7.0,
                Longitude = 79.9
            });

        context.InventoryItems.AddRange(
            new InventoryItem
            {
                Id = 1,
                DepotId = 1,
                ItemName = "Water",
                Unit = "bottles",
                QuantityAvailable = 100
            },
            new InventoryItem
            {
                Id = 2,
                DepotId = 2,
                ItemName = "Food",
                Unit = "packs",
                QuantityAvailable = 50
            });

        await context.SaveChangesAsync();

        var controller = new InventoryController(context);

        var result = await controller.GetInventory(
            null,
            CancellationToken.None);

        var actionResult =
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);

        var items =
            Assert.IsAssignableFrom<IEnumerable<InventoryItem>>(actionResult.Value);

        Assert.Equal(2, items.Count());
    }

    [Fact]
    public async Task GetInventory_WithDepotId_ReturnsOnlyMatchingDepotItems()
    {
        await using var context = CreateContext();

        context.Depots.AddRange(
            new Depot
            {
                Id = 1,
                Name = "Depot A",
                Latitude = 6.9,
                Longitude = 79.8
            },
            new Depot
            {
                Id = 2,
                Name = "Depot B",
                Latitude = 7.0,
                Longitude = 79.9
            });

        context.InventoryItems.AddRange(
            new InventoryItem
            {
                Id = 1,
                DepotId = 1,
                ItemName = "Water",
                Unit = "bottles",
                QuantityAvailable = 100
            },
            new InventoryItem
            {
                Id = 2,
                DepotId = 2,
                ItemName = "Food",
                Unit = "packs",
                QuantityAvailable = 50
            });

        await context.SaveChangesAsync();

        var controller = new InventoryController(context);

        var result = await controller.GetInventory(
            1,
            CancellationToken.None);

        var actionResult =
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result.Result);

        var items =
            Assert.IsAssignableFrom<IEnumerable<InventoryItem>>(actionResult.Value);

        Assert.Single(items);
        Assert.Equal("Water", items.Single().ItemName);
        Assert.Equal(1, items.Single().DepotId);
    }

    [Fact]
    public async Task CheckIn_WithValidQuantity_IncreasesAvailableStock()
    {
        await using var context = CreateContext();

        context.Depots.Add(new Depot
        {
            Id = 1,
            Name = "Main Depot",
            Latitude = 6.9,
            Longitude = 79.8
        });

        context.InventoryItems.Add(new InventoryItem
        {
            Id = 1,
            DepotId = 1,
            ItemName = "Water",
            Unit = "bottles",
            QuantityAvailable = 100
        });

        await context.SaveChangesAsync();

        var controller = new InventoryController(context);

        var dto = new StockCheckInDto
        {
            QuantityReceived = 25
        };

        await controller.CheckIn(
            1,
            dto,
            CancellationToken.None);

        var item = await context.InventoryItems.FindAsync(1);

        Assert.NotNull(item);
        Assert.Equal(125, item.QuantityAvailable);
    }

    [Fact]
    public async Task CheckIn_WhenItemDoesNotExist_ThrowsNotFoundException()
    {
        await using var context = CreateContext();
        var controller = new InventoryController(context);

        var dto = new StockCheckInDto
        {
            QuantityReceived = 25
        };

        await Assert.ThrowsAsync<NotFoundException>(() =>
            controller.CheckIn(
                999,
                dto,
                CancellationToken.None));
    }

    [Fact]
    public async Task CheckIn_WithZeroQuantity_DoesNotChangeStock()
    {
        await using var context = CreateContext();

        context.Depots.Add(new Depot
        {
            Id = 1,
            Name = "Main Depot",
            Latitude = 6.9,
            Longitude = 79.8
        });

        context.InventoryItems.Add(new InventoryItem
        {
            Id = 1,
            DepotId = 1,
            ItemName = "Water",
            Unit = "bottles",
            QuantityAvailable = 100
        });

        await context.SaveChangesAsync();

        var controller = new InventoryController(context);

        var dto = new StockCheckInDto
        {
            QuantityReceived = 0
        };

        await controller.CheckIn(
            1,
            dto,
            CancellationToken.None);

        var item = await context.InventoryItems.FindAsync(1);

        Assert.NotNull(item);
        Assert.Equal(100, item.QuantityAvailable);
    }
    [Fact]
public async Task CheckIn_WithNegativeQuantity_DecreasesStock()
{
    await using var context = CreateContext();

    var depot = new Depot
    {
        Name = "Main Depot",
        Latitude = 6.9271,
        Longitude = 79.8612
    };

    context.Depots.Add(depot);
    await context.SaveChangesAsync();

    var item = new InventoryItem
    {
        DepotId = depot.Id,
        ItemName = "Water",
        Unit = "bottles",
        QuantityAvailable = 100
    };

    context.InventoryItems.Add(item);
    await context.SaveChangesAsync();

    var controller = new InventoryController(context);

    var result = await controller.CheckIn(
        item.Id,
        new StockCheckInDto { QuantityReceived = -10 },
        CancellationToken.None);

    var updatedItem = await context.InventoryItems.FindAsync(item.Id);

    Assert.Equal(90, updatedItem!.QuantityAvailable);
}
}