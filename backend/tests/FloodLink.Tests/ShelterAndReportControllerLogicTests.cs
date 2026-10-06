using FloodLink.Agents.Triage;
using FloodLink.Api.Controllers;
using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Exceptions;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace FloodLink.Tests;

public class ShelterAndReportControllerLogicTests
{
    private AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        // Seed basic user
        context.Users.Add(new User
        {
            Id = 1,
            Name = "Test Volunteer",
            Role = "Volunteer",
            Phone = "+94770000000",
            HashedPassword = "pw"
        });

        // Seed basic shelter
        context.Shelters.Add(new Shelter
        {
            Id = 1,
            Name = "Colombo Central Shelter",
            Capacity = 100,
            CurrentOccupancy = 50,
            Status = "Active",
            Latitude = 6.9271,
            Longitude = 79.8612
        });

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task GetShelters_AppliesSearchFilterAndPagination()
    {
        using var context = GetInMemoryDbContext();
        context.Shelters.Add(new Shelter { Id = 2, Name = "Kaduwela Hall", Capacity = 50, CurrentOccupancy = 10, Status = "Active" });
        context.Shelters.Add(new Shelter { Id = 3, Name = "Gampaha Primary", Capacity = 80, CurrentOccupancy = 20, Status = "Closed" });
        await context.SaveChangesAsync();

        var controller = new SheltersController(context);

        // Search by "colombo"
        var actionResult = await controller.GetShelters(status: null, search: "colombo", page: 1, pageSize: 10);
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var paginated = Assert.IsType<PaginatedResult<Shelter>>(okResult.Value);

        Assert.Single(paginated.Items);
        Assert.Equal("Colombo Central Shelter", paginated.Items.First().Name);

        // Filter by status "Closed"
        var closedResult = await controller.GetShelters(status: "Closed", search: null, page: 1, pageSize: 10);
        var closedOk = Assert.IsType<OkObjectResult>(closedResult.Result);
        var closedPaginated = Assert.IsType<PaginatedResult<Shelter>>(closedOk.Value);

        Assert.Single(closedPaginated.Items);
        Assert.Equal("Gampaha Primary", closedPaginated.Items.First().Name);
    }

    [Fact]
    public async Task GetShelterById_NotFound_ThrowsNotFoundException()
    {
        using var context = GetInMemoryDbContext();
        var controller = new SheltersController(context);

        await Assert.ThrowsAsync<NotFoundException>(async () => await controller.GetShelterById(999));
    }

    [Fact]
    public async Task CreateReport_CalculatesAutoUrgencyAndSaves()
    {
        using var context = GetInMemoryDbContext();
        var urgencyService = new UrgencyScoringService();

        var mockEnv = new Mock<IWebHostEnvironment>();
        mockEnv.Setup(e => e.WebRootPath).Returns(Path.GetTempPath());
        var photoService = new PhotoStorageService(mockEnv.Object);
        var mockAgent = new Mock<ITriageAgent>();

        var controller = new ReportsController(context, urgencyService, photoService, mockAgent.Object);

        var dto = new CreateReportDto
        {
            ShelterId = 1,
            ReportedBy = 1,
            NeedType = "Water",
            QuantityNeeded = 200,
            Status = "New"
        };

        var actionResult = await controller.CreateReport(dto);
        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var report = Assert.IsType<Report>(createdResult.Value);

        Assert.Equal(1, report.ShelterId);
        Assert.Equal("Water", report.NeedType);
        Assert.True(report.UrgencyLevel > 0);
    }

    [Fact]
    public async Task GetReports_AppliesFiltersAndSorting()
    {
        using var context = GetInMemoryDbContext();

        context.Reports.Add(new Report { Id = 1, ShelterId = 1, ReportedBy = 1, NeedType = "Water", UrgencyLevel = 90, Status = "New", CreatedAt = DateTime.UtcNow.AddHours(-2) });
        context.Reports.Add(new Report { Id = 2, ShelterId = 1, ReportedBy = 1, NeedType = "Food", UrgencyLevel = 40, Status = "Triaged", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var urgencyService = new UrgencyScoringService();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var photoService = new PhotoStorageService(mockEnv.Object);
        var mockAgent = new Mock<ITriageAgent>();
        var controller = new ReportsController(context, urgencyService, photoService, mockAgent.Object);

        // Filter by urgency >= 50 and sort by urgency descending
        var actionResult = await controller.GetReports(shelterId: 1, status: null, urgency: 50, sort: "urgency_desc");
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var reports = Assert.IsAssignableFrom<IEnumerable<Report>>(okResult.Value);

        Assert.Single(reports);
        Assert.Equal(90, reports.First().UrgencyLevel);
    }
}
