using System.ComponentModel.DataAnnotations;

namespace FloodLink.Api.DTOs;

public class CreateShelterDto
{
    [Required(ErrorMessage = "Shelter name is required.")]
    [StringLength(150, ErrorMessage = "Shelter name cannot exceed 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Capacity cannot be negative.")]
    public int Capacity { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Current occupancy cannot be negative.")]
    public int CurrentOccupancy { get; set; }

    public int? ContactVolunteerId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Active|Closed)$", ErrorMessage = "Status must be either 'Active' or 'Closed'.")]
    public string Status { get; set; } = "Active";
}

public class UpdateShelterDto
{
    [Required(ErrorMessage = "Shelter name is required.")]
    [StringLength(150, ErrorMessage = "Shelter name cannot exceed 150 characters.")]
    public string Name { get; set; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "Latitude must be between -90 and 90.")]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Longitude must be between -180 and 180.")]
    public double Longitude { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Capacity cannot be negative.")]
    public int Capacity { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Current occupancy cannot be negative.")]
    public int CurrentOccupancy { get; set; }

    public int? ContactVolunteerId { get; set; }

    [Required(ErrorMessage = "Status is required.")]
    [RegularExpression("^(Active|Closed)$", ErrorMessage = "Status must be either 'Active' or 'Closed'.")]
    public string Status { get; set; } = "Active";
}
