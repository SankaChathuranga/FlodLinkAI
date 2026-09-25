using FloodLink.Api.Controllers;
using FloodLink.Api.DTOs;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace FloodLink.Tests;

public class ShelterAndReportControllerTests
{
    [Fact]
    public void SheltersController_GetShelters_ReturnsOk()
    {
        var controller = new SheltersController();
        var result = controller.GetShelters();
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void SheltersController_GetShelterById_ReturnsOk()
    {
        var controller = new SheltersController();
        var result = controller.GetShelterById(1);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void SheltersController_CreateShelter_ReturnsCreatedAtAction()
    {
        var controller = new SheltersController();
        var dto = new CreateShelterDto
        {
            Name = "Test Shelter",
            Latitude = 6.9,
            Longitude = 79.8,
            Capacity = 100,
            CurrentOccupancy = 20,
            Status = "Active"
        };
        var result = controller.CreateShelter(dto);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(SheltersController.GetShelterById), createdResult.ActionName);
    }

    [Fact]
    public void SheltersController_UpdateShelter_ReturnsNoContent()
    {
        var controller = new SheltersController();
        var dto = new UpdateShelterDto
        {
            Name = "Updated Shelter",
            Latitude = 6.9,
            Longitude = 79.8,
            Capacity = 150,
            CurrentOccupancy = 50,
            Status = "Active"
        };
        var result = controller.UpdateShelter(1, dto);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void SheltersController_DeleteShelter_ReturnsNoContent()
    {
        var controller = new SheltersController();
        var result = controller.DeleteShelter(1);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void ReportsController_GetReports_ReturnsOk()
    {
        var controller = new ReportsController();
        var result = controller.GetReports();
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void ReportsController_GetReportById_ReturnsOk()
    {
        var controller = new ReportsController();
        var result = controller.GetReportById(1);
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public void ReportsController_CreateReport_ReturnsCreatedAtAction()
    {
        var controller = new ReportsController();
        var dto = new CreateReportDto
        {
            ShelterId = 1,
            ReportedBy = 1,
            NeedType = "Water",
            QuantityNeeded = 100,
            UrgencyLevel = 4,
            Status = "New"
        };
        var result = controller.CreateReport(dto);
        var createdResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(ReportsController.GetReportById), createdResult.ActionName);
    }

    [Fact]
    public void ReportsController_UpdateReport_ReturnsNoContent()
    {
        var controller = new ReportsController();
        var dto = new UpdateReportDto
        {
            NeedType = "Food",
            QuantityNeeded = 200,
            UrgencyLevel = 3,
            Status = "Triaged"
        };
        var result = controller.UpdateReport(1, dto);
        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public void ReportsController_DeleteReport_ReturnsNoContent()
    {
        var controller = new ReportsController();
        var result = controller.DeleteReport(1);
        Assert.IsType<NoContentResult>(result);
    }
}
