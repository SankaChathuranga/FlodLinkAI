using FloodLink.Contracts.Agents;
using FloodLink.Domain.Entities;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Services;

public class MatchingAgentService : IMatchingAgentService
{
    private const double EarthRadiusKilometres = 6371.0;
    private readonly AppDbContext _context;
    private readonly ILogger<MatchingAgentService> _logger;

    public MatchingAgentService(AppDbContext context, ILogger<MatchingAgentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<MatchingResult> MatchAsync(TriagePlan triagePlan)
    {
        ArgumentNullException.ThrowIfNull(triagePlan);

        var inventory = await _context.InventoryItems
            .Include(item => item.Depot)
            .ToListAsync();
        var availableStock = inventory.ToDictionary(
            item => item.Id,
            item => Math.Max(0m, item.QuantityAvailable - item.QuantityReserved));
        var result = new MatchingResult();

        foreach (var need in triagePlan.Needs.OrderByDescending(need => need.Priority))
        {
            if (need.QuantityRequired <= 0)
                continue;

            var candidates = inventory
                .Where(item => string.Equals(item.ItemName, need.ItemName, StringComparison.OrdinalIgnoreCase))
                .Where(item => availableStock[item.Id] > 0)
                .Select(item => new
                {
                    Item = item,
                    Available = availableStock[item.Id],
                    Distance = DistanceToShelter(item.Depot, need)
                })
                .ToList();

            if (candidates.Count == 0)
            {
                result.UnfulfillableNeeds.Add(CreateUnfulfillableNeed(need, need.QuantityRequired));
                _logger.LogWarning("No available stock for {ItemName} at shelter {ShelterId}", need.ItemName, need.ShelterId);
                continue;
            }

            var selected = candidates
                .Where(candidate => candidate.Available >= need.QuantityRequired)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Item.DepotId)
                .FirstOrDefault()
                ?? candidates
                    .OrderByDescending(candidate => candidate.Available)
                    .ThenBy(candidate => candidate.Distance)
                    .ThenBy(candidate => candidate.Item.DepotId)
                    .First();

            var allocatedQuantity = Math.Min(need.QuantityRequired, selected.Available);
            availableStock[selected.Item.Id] -= allocatedQuantity;
            result.Proposals.Add(new AllocationProposalDto
            {
                DepotId = selected.Item.DepotId,
                ShelterId = need.ShelterId,
                ItemName = need.ItemName,
                Quantity = allocatedQuantity,
                Status = allocatedQuantity < need.QuantityRequired ? "Partial" : "Proposed"
            });

            if (allocatedQuantity < need.QuantityRequired)
                result.UnfulfillableNeeds.Add(CreateUnfulfillableNeed(need, need.QuantityRequired - allocatedQuantity));

            _context.AllocationProposals.Add(new AllocationProposal
            {
                DepotId = selected.Item.DepotId,
                ShelterId = need.ShelterId,
                ItemName = need.ItemName,
                Quantity = allocatedQuantity,
                Status = "Proposed",
                CreatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return result;
    }

    private static UnfulfillableNeed CreateUnfulfillableNeed(TriageNeed need, decimal quantity) => new()
    {
        ShelterId = need.ShelterId,
        ItemName = need.ItemName,
        RequestedQuantity = need.QuantityRequired,
        UnfulfilledQuantity = quantity,
        Reason = "Insufficient stock across all depots"
    };

    private static double DistanceToShelter(Depot depot, TriageNeed need)
    {
        if (!need.ShelterLatitude.HasValue || !need.ShelterLongitude.HasValue)
            return depot.Id;

        var latitudeDelta = DegreesToRadians((double)need.ShelterLatitude.Value - (double)depot.Latitude);
        var longitudeDelta = DegreesToRadians((double)need.ShelterLongitude.Value - (double)depot.Longitude);
        var shelterLatitude = DegreesToRadians((double)need.ShelterLatitude.Value);
        var depotLatitude = DegreesToRadians((double)depot.Latitude);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(shelterLatitude) * Math.Cos(depotLatitude) * Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return EarthRadiusKilometres * 2 * Math.Asin(Math.Sqrt(haversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
