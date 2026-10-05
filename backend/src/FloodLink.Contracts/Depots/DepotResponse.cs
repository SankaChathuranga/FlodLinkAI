namespace FloodLink.Contracts.Depots;

public class DepotResponse
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public int? ManagerId { get; set; }

    public DateTime CreatedAt { get; set; }
}