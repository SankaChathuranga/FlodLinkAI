using FloodLink.Contracts;

namespace FloodLink.Agents.Validation;

/// <summary>
/// Deterministic, rule-based safety checks for the Validation/Safety Agent (Member D).
/// Pure static logic — no I/O, no LLM — so every check is unit-testable in isolation.
/// </summary>
public static class SafetyRules
{
    /// <summary>Stable check name: allocated quantity must not exceed free stock.</summary>
    public const string StockAvailability = "StockAvailability";

    /// <summary>Stable check name: depot reorder floor must be preserved after allocation.</summary>
    public const string ReserveMinimumThreshold = "ReserveMinimumThreshold";

    /// <summary>Stable check name: total load must fit the truck's capacity.</summary>
    public const string VehicleCapacity = "VehicleCapacity";

    /// <summary>Stable check name: route/ETA values and coordinates must be sane.</summary>
    public const string CoordinateSanity = "CoordinateSanity";

    /// <summary>Stable check name: every depot → shelter pair in the allocation has a routed leg.</summary>
    public const string RouteCompleteness = "RouteCompleteness";

    /// <summary>Maximum plausible road distance for a single relief delivery, in km.</summary>
    public const double MaxPlausibleDistanceKm = 400;

    /// <summary>Maximum plausible travel time for a relief delivery, in minutes (24 hours).</summary>
    public const double MaxPlausibleEtaMinutes = 24 * 60;

    /// <summary>
    /// Runs every rule against the complete plan and returns one
    /// <see cref="ValidationCheck"/> per check. Every check is explicitly listed —
    /// none are silently skipped.
    /// </summary>
    /// <remarks>
    /// When the plan has <see cref="PlanDocument.Legs"/>, each leg (one truck) gets its own
    /// capacity and coordinate check, and route completeness is checked. Otherwise the plan's
    /// single origin/destination is checked.
    /// </remarks>
    public static IReadOnlyList<ValidationCheck> RunAll(PlanDocument plan, Route route)
    {
        var checks = new List<ValidationCheck>();

        foreach (var line in plan.Allocations)
        {
            checks.Add(CheckStockAvailability(line));
            checks.Add(CheckReserveMinimum(line));
        }

        if (plan.Legs.Count > 0)
        {
            checks.Add(CheckRouteCompleteness(plan));
            foreach (var leg in plan.Legs)
            {
                checks.Add(CheckLegCapacity(leg, plan.VehicleCapacity));
                checks.Add(CheckLegCoordinates(leg));
            }
        }
        else
        {
            checks.Add(CheckVehicleCapacity(plan));
            checks.Add(CheckCoordinates(plan, route));
        }

        return checks;
    }

    /// <summary>Rule: every depot → shelter pair that receives an allocation has a routed leg.</summary>
    public static ValidationCheck CheckRouteCompleteness(PlanDocument plan)
    {
        var missing = plan.Allocations
            .Select(a => (a.DepotId, a.ShelterId))
            .Distinct()
            .Where(pair => !plan.Legs.Any(l => l.DepotId == pair.DepotId && l.ShelterId == pair.ShelterId))
            .ToList();
        return new ValidationCheck
        {
            CheckName = RouteCompleteness,
            Passed = missing.Count == 0,
            ViolationDetail = missing.Count == 0
                ? null
                : "No route for: " + string.Join(", ", missing.Select(p => $"depot {p.DepotId} → shelter {p.ShelterId}")) + "."
        };
    }

    /// <summary>Rule: the load carried on one leg must fit one truck.</summary>
    public static ValidationCheck CheckLegCapacity(PlanRouteLeg leg, double vehicleCapacity)
    {
        var passed = leg.Load <= vehicleCapacity;
        return new ValidationCheck
        {
            CheckName = VehicleCapacity,
            Passed = passed,
            ViolationDetail = passed
                ? null
                : $"Load {leg.Load} on depot {leg.DepotId} → shelter {leg.ShelterId} exceeds vehicle capacity {vehicleCapacity}."
        };
    }

    /// <summary>Rule: one leg's coordinates, distance and ETA must be sane (see <see cref="CheckCoordinates"/>).</summary>
    public static ValidationCheck CheckLegCoordinates(PlanRouteLeg leg)
    {
        var violation = Violation(leg.OriginLat, leg.OriginLng, leg.DestLat, leg.DestLng, leg.DistanceKm, leg.EtaMinutes);
        return new ValidationCheck
        {
            CheckName = CoordinateSanity,
            Passed = violation is null,
            ViolationDetail = violation is null ? null : $"Depot {leg.DepotId} → shelter {leg.ShelterId}: {violation}"
        };
    }

    /// <summary>
    /// Rule: <c>Quantity &lt;= QuantityAvailable - QuantityReserved</c> for each line.
    /// Guards against over-allocation of already-reserved or unavailable stock.
    /// </summary>
    public static ValidationCheck CheckStockAvailability(PlanAllocationLine line)
    {
        var freeStock = line.QuantityAvailable - line.QuantityReserved;
        var passed = line.Quantity <= freeStock;
        return new ValidationCheck
        {
            CheckName = StockAvailability,
            Passed = passed,
            ViolationDetail = passed
                ? null
                : $"Requested {line.Quantity} {line.ItemName} exceeds free stock {freeStock} at depot {line.DepotId}."
        };
    }

    /// <summary>
    /// Rule: <c>QuantityAvailable - QuantityReserved - Quantity &gt;= ReorderThreshold</c>.
    /// Prevents a single plan from emptying a depot below its reserve/reorder floor.
    /// </summary>
    public static ValidationCheck CheckReserveMinimum(PlanAllocationLine line)
    {
        var remainingAfter = line.QuantityAvailable - line.QuantityReserved - line.Quantity;
        var passed = remainingAfter >= line.ReorderThreshold;
        return new ValidationCheck
        {
            CheckName = ReserveMinimumThreshold,
            Passed = passed,
            ViolationDetail = passed
                ? null
                : $"After allocating {line.Quantity} {line.ItemName}, depot {line.DepotId} free stock would be {remainingAfter}, below the reserve floor {line.ReorderThreshold}."
        };
    }

    /// <summary>
    /// Rule: total quantity across all allocations must fit the vehicle capacity.
    /// </summary>
    public static ValidationCheck CheckVehicleCapacity(PlanDocument plan)
    {
        var totalLoad = plan.Allocations.Sum(a => a.Quantity);
        var passed = totalLoad <= plan.VehicleCapacity;
        return new ValidationCheck
        {
            CheckName = VehicleCapacity,
            Passed = passed,
            ViolationDetail = passed
                ? null
                : $"Total load {totalLoad} exceeds vehicle capacity {plan.VehicleCapacity}."
        };
    }

    /// <summary>
    /// Rule: coordinates must be within geographic bounds, origin must differ from
    /// destination, and distance/ETA from the routing agent must be positive and
    /// plausible. Flags routing-agent failures to a safe state rather than a crash.
    /// </summary>
    public static ValidationCheck CheckCoordinates(PlanDocument plan, Route route)
    {
        var violation = Violation(plan.OriginLat, plan.OriginLng, plan.DestLat, plan.DestLng, route.DistanceKm, route.EtaMinutes);

        return new ValidationCheck
        {
            CheckName = CoordinateSanity,
            Passed = violation is null,
            ViolationDetail = violation
        };
    }

    private static string? Violation(double originLat, double originLng, double destLat, double destLng,
        double distanceKm, double etaMinutes) => true switch
    {
        _ when IsInvalidLat(originLat) || IsInvalidLat(destLat) => "Origin/destination latitude out of range [-90, 90].",
        _ when IsInvalidLng(originLng) || IsInvalidLng(destLng) => "Origin/destination longitude out of range [-180, 180].",
        _ when originLat == destLat && originLng == destLng => "Origin and destination are identical; no journey to deliver.",
        _ when double.IsNaN(distanceKm) || distanceKm <= 0 => "Routing distance must be positive.",
        _ when distanceKm > MaxPlausibleDistanceKm => $"Routing distance {distanceKm} km exceeds the plausible maximum {MaxPlausibleDistanceKm} km.",
        _ when double.IsNaN(etaMinutes) || etaMinutes <= 0 => "Routing ETA must be positive.",
        _ when etaMinutes > MaxPlausibleEtaMinutes => $"Routing ETA {etaMinutes} minutes exceeds the plausible maximum {MaxPlausibleEtaMinutes} minutes.",
        _ => null
    };

    private static bool IsInvalidLat(double value) => double.IsNaN(value) || value is < -90 or > 90;

    private static bool IsInvalidLng(double value) => double.IsNaN(value) || value is < -180 or > 180;
}