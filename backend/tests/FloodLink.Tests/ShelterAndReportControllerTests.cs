using FloodLink.Api.Controllers;
using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FloodLink.Tests;

public class ShelterAndReportControllerTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        context.Users.Add(new User
        {
            Id = 1,
            Name = "Volunteer 1",
            Role = "Volunteer",
            Phone = "+94770000000",
            HashedPassword = "pass"
        });

        context.Shelters.Add(new Shelter
        {
            Id = 1,
            Name = "Shelter Alpha",
            Capacity = 100,
            CurrentOccupancy = 50,
            Status = "Active",
            Latitude = 6.9,
            Longitude = 79.8
        });

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task SheltersController_GetShelters_ReturnsOk()
    {
        using var context = CreateDbContext();
        var controller = new SheltersController(context);
        var result = await controller.GetShelters(status: null, search: null, page: 1, pageSize: 10);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task SheltersController_GetShelterById_ReturnsOk()
    {
        using var context = CreateDbContext();
        var controller = new SheltersController(context);
        var result = await controller.GetShelterById(1);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task SheltersController_CreateShelter_ReturnsCreatedAtAction()
    {
        using var context = CreateDbContext();
        var controller = new SheltersController(context);
        var dto = new CreateShelterDto
        {
            Name = "New Shelter",
            Latitude = 6.9,
            Longitude = 79.8,
            Capacity = 100,
            CurrentOccupancy = 20,
            Status = "Active"
        };
        var result = await controller.CreateShelter(dto);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(SheltersController.GetShelterById), createdResult.ActionName);
    }

    [Fact]
    public async Task SheltersController_UpdateShelter_ReturnsOk()
    {
        using var context = CreateDbContext();
        var controller = new SheltersController(context);
        var dto = new UpdateShelterDto
        {
            Name = "Updated Shelter",
            Latitude = 6.9,
            Longitude = 79.8,
            Capacity = 150,
            CurrentOccupancy = 50,
            Status = "Active"
        };
        var result = await controller.UpdateShelter(1, dto);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReportsController_GetReports_ReturnsOk()
    {
        using var context = CreateDbContext();
        var urgencyService = new UrgencyScoringService();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var photoService = new PhotoStorageService(mockEnv.Object);
        var controller = new ReportsController(context, urgencyService, photoService);

        var result = await controller.GetReports(shelterId: null, status: null, urgency: null, sort: null);
        Assert.IsType<OkObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReportsController_CreateReport_ReturnsCreatedAtAction()
    {
        using var context = CreateDbContext();
        var urgencyService = new UrgencyScoringService();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var photoService = new PhotoStorageService(mockEnv.Object);
        var controller = new ReportsController(context, urgencyService, photoService);

        var dto = new CreateReportDto
        {
            ShelterId = 1,
            ReportedBy = 1,
            NeedType = "Water",
            QuantityNeeded = 100,
            Status = "New"
        };
        var result = await controller.CreateReport(dto);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(ReportsController.GetReportById), createdResult.ActionName);
    }
}
