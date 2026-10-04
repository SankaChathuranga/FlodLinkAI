namespace FloodLink.Contracts.Depots;

public class CreateDepotRequest
{
    public string Name { get; set; } = string.Empty;

    public decimal Latitude { get; set; }

    public decimal Longitude { get; set; }

    public int? ManagerId { get; set; }
}