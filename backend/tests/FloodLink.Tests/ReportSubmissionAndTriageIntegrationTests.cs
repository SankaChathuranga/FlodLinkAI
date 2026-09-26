using FloodLink.Agents.Triage;
using FloodLink.Api.Controllers;
using FloodLink.Api.DTOs;
using FloodLink.Contracts;
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

public class ReportSubmissionAndTriageIntegrationTests
{
    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);

        // Seed default volunteer user
        context.Users.Add(new User
        {
            Id = 1,
            Name = "Field Reporter",
            Role = "Volunteer",
            Phone = "+94771112223",
            HashedPassword = "hashed_secret"
        });

        // Seed default shelter
        context.Shelters.Add(new Shelter
        {
            Id = 1,
            Name = "Relief Camp 1",
            Capacity = 100,
            CurrentOccupancy = 90,
            Status = "Active",
            Latitude = 6.9271,
            Longitude = 79.8612
        });

        context.SaveChanges();
        return context;
    }

    [Fact]
    public async Task CreateReport_NonExistentShelterId_ThrowsBadRequestException()
    {
        // Arrange
        using var context = CreateDbContext();
        var scoringService = new UrgencyScoringService();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var photoService = new PhotoStorageService(mockEnv.Object);
        var mockAgent = new Mock<ITriageAgent>();

        var controller = new ReportsController(context, scoringService, photoService, mockAgent.Object);

        var dto = new CreateReportDto
        {
            ShelterId = 9999, // Invalid shelter ID
            ReportedBy = 1,
            NeedType = "Water",
            QuantityNeeded = 50,
            Status = "New"
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(async () => await controller.CreateReport(dto));
    }

    [Fact]
    public async Task CreateReport_NonExistentReporterId_ThrowsBadRequestException()
    {
        // Arrange
        using var context = CreateDbContext();
        var scoringService = new UrgencyScoringService();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var photoService = new PhotoStorageService(mockEnv.Object);
        var mockAgent = new Mock<ITriageAgent>();

        var controller = new ReportsController(context, scoringService, photoService, mockAgent.Object);

        var dto = new CreateReportDto
        {
            ShelterId = 1,
            ReportedBy = 8888, // Invalid reporter user ID
            NeedType = "Food",
            QuantityNeeded = 100,
            Status = "New"
        };

        // Act & Assert
        await Assert.ThrowsAsync<BadRequestException>(async () => await controller.CreateReport(dto));
    }

    [Fact]
    public async Task CreateReport_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        using var context = CreateDbContext();
        var scoringService = new UrgencyScoringService();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var photoService = new PhotoStorageService(mockEnv.Object);
        var mockAgent = new Mock<ITriageAgent>();

        var controller = new ReportsController(context, scoringService, photoService, mockAgent.Object);
        controller.ModelState.AddModelError("NeedType", "The NeedType field is required.");

        var dto = new CreateReportDto
        {
            ShelterId = 1,
            ReportedBy = 1,
            NeedType = "",
            QuantityNeeded = 10,
            Status = "New"
        };

        // Act
        var result = await controller.CreateReport(dto);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateReport_ValidDto_SuccessfullyCreatesReportWithAutoCalculatedUrgency()
    {
        // Arrange
        using var context = CreateDbContext();
        var scoringService = new UrgencyScoringService();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var photoService = new PhotoStorageService(mockEnv.Object);
        var mockAgent = new Mock<ITriageAgent>();

        var controller = new ReportsController(context, scoringService, photoService, mockAgent.Object);

        var dto = new CreateReportDto
        {
            ShelterId = 1,
            ReportedBy = 1,
            NeedType = "Medical",
            QuantityNeeded = 30,
            Status = "New",
            GpsLat = 6.9,
            GpsLng = 79.8
        };

        // Act
        var result = await controller.CreateReport(dto);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        var report = Assert.IsType<Report>(createdResult.Value);

        Assert.True(report.Id > 0);
        Assert.Equal(1, report.ShelterId);
        Assert.Equal("Medical", report.NeedType);
        Assert.Equal("New", report.Status);
        Assert.True(report.UrgencyLevel > 0, "UrgencyLevel should be automatically calculated.");
    }

    [Fact]
    public async Task TriggerTriage_EndToEnd_SubmitsReportAndProducesPersistedTriagePlan()
    {
        // Arrange
        using var context = CreateDbContext();
        var scoringService = new UrgencyScoringService();
        var mockEnv = new Mock<IWebHostEnvironment>();
        var photoService = new PhotoStorageService(mockEnv.Object);

        // Instantiate concrete TriageAgent
        var triageAgent = new TriageAgent(context, scoringService);
        var controller = new ReportsController(context, scoringService, photoService, triageAgent);

        // Step 1: Submit a report via controller
        var createDto = new CreateReportDto
        {
            ShelterId = 1,
            ReportedBy = 1,
            NeedType = "Water",
            QuantityNeeded = 250,
            Status = "New"
        };

        var createResult = await controller.CreateReport(createDto);
        var createdActionResult = Assert.IsType<CreatedAtActionResult>(createResult.Result);
        var createdReport = Assert.IsType<Report>(createdActionResult.Value);
        int reportId = createdReport.Id;

        // Step 2: Trigger triage via POST /api/reports/{id}/trigger-triage
        var triageActionResult = await controller.TriggerTriage(reportId);

        // Step 3: Assert HTTP 200 OK returned with TriagePlan contract
        var okResult = Assert.IsType<OkObjectResult>(triageActionResult.Result);
        var plan = Assert.IsType<TriagePlan>(okResult.Value);

        Assert.NotNull(plan);
        Assert.NotEqual(Guid.Empty, plan.WorkflowRunId);
        Assert.Single(plan.PriorityItems);

        var priorityItem = plan.PriorityItems[0];
        Assert.Equal(reportId, priorityItem.ReportId);
        Assert.Equal(1, priorityItem.ShelterId);
        Assert.Equal("Water", priorityItem.NeedType);
        Assert.Equal(250, priorityItem.Quantity);
        Assert.True(priorityItem.PriorityScore > 0);
        Assert.NotEmpty(priorityItem.Justification);

        // Step 4: Verify TriagePlanEntity row is persisted in DB
        var persistedEntity = await context.TriagePlans
            .FirstOrDefaultAsync(tp => tp.CreatedByAgentRunId == plan.WorkflowRunId);

        Assert.NotNull(persistedEntity);
        Assert.Contains(reportId.ToString(), persistedEntity.PlanSummaryJson);
        Assert.Equal(1, persistedEntity.PriorityRank);
    }
}
