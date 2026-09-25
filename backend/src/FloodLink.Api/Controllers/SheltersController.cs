using FloodLink.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace FloodLink.Api.Controllers;

[ApiController]
[Route("api/shelters")]
public class SheltersController : ControllerBase
{
    /// <summary>
    /// GET /api/shelters
    /// Retrieves all shelters.
    /// </summary>
    [HttpGet]
    public IActionResult GetShelters()
    {
        return Ok();
    }

    /// <summary>
    /// GET /api/shelters/{id}
    /// Retrieves a specific shelter by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    public IActionResult GetShelterById(int id)
    {
        return Ok();
    }

    /// <summary>
    /// POST /api/shelters
    /// Creates a new shelter.
    /// </summary>
    [HttpPost]
    public IActionResult CreateShelter([FromBody] CreateShelterDto dto)
    {
        return CreatedAtAction(nameof(GetShelterById), new { id = 1 }, dto);
    }

    /// <summary>
    /// PUT /api/shelters/{id}
    /// Updates an existing shelter.
    /// </summary>
    [HttpPut("{id:int}")]
    public IActionResult UpdateShelter(int id, [FromBody] UpdateShelterDto dto)
    {
        return NoContent();
    }

    /// <summary>
    /// DELETE /api/shelters/{id}
    /// Deletes a shelter by ID.
    /// </summary>
    [HttpDelete("{id:int}")]
    public IActionResult DeleteShelter(int id)
    {
        return NoContent();
    }
}
