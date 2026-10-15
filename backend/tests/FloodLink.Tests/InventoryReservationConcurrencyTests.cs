using System.Diagnostics;
using FloodLink.Api.Controllers;
using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace FloodLink.Tests;

public class InventoryReservationConcurrencyTests
{
    private static bool IsDockerAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (process == null)
                return false;

            var exited = process.WaitForExit(3000);
            return exited && process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    [Trait("Category", "PostgresIntegration")]
    public async Task TwoParallelReservations_ExceedingFreeStock_ExactlyOneSucceedsAndStockNeverOverallocated()
    {
        if (!IsDockerAvailable())
        {
            return;
        }

        await using var postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await postgres.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        // Apply migrations to test against the real PostgreSQL schema and constraints
        await using (var setupContext = new AppDbContext(options))
        {
            await setupContext.Database.MigrateAsync();

            var depot = new Depot
            {
                Id = 101,
                Name = "Concurrency Depot",
                Latitude = 6.9,
                Longitude = 79.8
            };
            setupContext.Depots.Add(depot);

            var item = new InventoryItem
            {
                Id = 101,
                DepotId = 101,
                ItemName = "HighDemandItem",
                Unit = "units",
                QuantityAvailable = 100,
                QuantityReserved = 0,
                ReorderThreshold = 10
            };
            setupContext.InventoryItems.Add(item);
            await setupContext.SaveChangesAsync();
        }

        // Two concurrent requests attempting to reserve 60 units each from a stock of 100 (total demand = 120)
        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await using var context = new AppDbContext(options);
            var controller = new InventoryController(context);
            try
            {
                var result = await controller.Reserve(101, new ReserveStockDto
                {
                    Quantity = 60,
                    ReservedBy = "Client 1"
                }, CancellationToken.None);

                return (Success: true, Exception: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Exception: ex);
            }
        });

        var task2 = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await using var context = new AppDbContext(options);
            var controller = new InventoryController(context);
            try
            {
                var result = await controller.Reserve(101, new ReserveStockDto
                {
                    Quantity = 60,
                    ReservedBy = "Client 2"
                }, CancellationToken.None);

                return (Success: true, Exception: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Exception: ex);
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        // Exactly one reservation must succeed, and exactly one must fail with ConflictException
        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var failedResult = results.Single(r => !r.Success);
        Assert.IsType<ConflictException>(failedResult.Exception);

        // Verify database state: total reserved never exceeds available stock
        await using (var verifyContext = new AppDbContext(options))
        {
            var verifiedItem = await verifyContext.InventoryItems.SingleAsync(i => i.Id == 101);
            Assert.Equal(60, verifiedItem.QuantityReserved);
            Assert.True(verifiedItem.QuantityReserved <= verifiedItem.QuantityAvailable);

            var reservations = await verifyContext.StockReservations.Where(r => r.InventoryItemId == 101).ToListAsync();
            Assert.Single(reservations);
            Assert.Equal(60, reservations.Single().Quantity);
            Assert.Equal("Active", reservations.Single().Status);
        }
    }
}
