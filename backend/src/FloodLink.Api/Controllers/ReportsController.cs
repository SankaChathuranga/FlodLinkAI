using FloodLink.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    /// <summary>
    /// GET /api/reports
    /// Retrieves all field reports.
    /// </summary>
    [HttpGet]
    public IActionResult GetReports()
    {
        return Ok();
    }

    /// <summary>
    /// GET /api/reports/{id}
    /// Retrieves a specific report by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public IActionResult GetReportById(int id)
    {
        return Ok();
    }

    /// <summary>
    /// POST /api/reports
    /// Creates a new field report.
    /// </summary>
    [HttpPost]
    public IActionResult CreateReport([FromBody] CreateReportDto dto)
    {
        return CreatedAtAction(nameof(GetReportById), new { id = 1 }, dto);
    }

    /// <summary>
    /// PUT /api/reports/{id}
    /// Updates an existing report.
    /// </summary>
    [HttpPut("{id:int}")]
    public IActionResult UpdateReport(int id, [FromBody] UpdateReportDto dto)
    {
        return NoContent();
    }

    /// <summary>
    /// DELETE /api/reports/{id}
    /// Deletes a report by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    public IActionResult DeleteReport(int id)
    {
        return NoContent();
    }
}
