using FloodLink.Agents.Validation;
using FloodLink.Contracts;
using Xunit;

namespace FloodLink.Tests;

/// <summary>
/// Unit tests for the Validation/Safety Agent's deterministic rule engine (Member D).
/// Each rule is tested in isolation (pass + fail), plus golden-case evaluations
/// that confirm an unsafe plan is blocked and a safe plan passes.
/// </summary>
public class SafetyRulesTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static PlanAllocationLine Line(double quantity, double available, double reserved, double threshold) =>
        new()
        {
            DepotId = 1,
            ShelterId = 2,
            ItemName = "Water",
            Quantity = quantity,
            QuantityAvailable = available,
            QuantityReserved = reserved,
            ReorderThreshold = threshold
        };

    private static Route Route(string workflowRunId = "11111111-1111-1111-1111-111111111111") =>
        new()
        {
            WorkflowRunId = Guid.Parse(workflowRunId),
            AllocationProposalId = 1,
            DistanceKm = 25,
            EtaMinutes = 45,
            Polyline = "encoded"
        };

    private static PlanDocument Plan(
        Guid workflowRunId,
        params PlanAllocationLine[] lines) =>
        new()
        {
            WorkflowRunId = workflowRunId,
            AllocationProposalId = 1,
            VehicleCapacity = double.MaxValue,
            DistanceKm = 25,
            EtaMinutes = 45,
            Polyline = "encoded",
            OriginLat = 6.9271,
            OriginLng = 79.8612,
            DestLat = 7.2906,
            DestLng = 80.6337,
            Allocations = lines
        };

    // ── CheckStockAvailability ────────────────────────────────────────────────

    [Fact]
    public void StockAvailability_Passes_WhenQuantityWithinFreeStock()
    {
        var check = SafetyRules.CheckStockAvailability(Line(quantity: 50, available: 200, reserved: 40, threshold: 20));

        Assert.True(check.Passed);
        Assert.Equal(SafetyRules.StockAvailability, check.CheckName);
        Assert.Null(check.ViolationDetail);
    }

    [Fact]
    public void StockAvailability_Fails_WhenQuantityExceedsFreeStock()
    {
        var check = SafetyRules.CheckStockAvailability(Line(quantity: 200, available: 200, reserved: 40, threshold: 20));

        Assert.False(check.Passed);
        Assert.NotNull(check.ViolationDetail);
    }

    [Fact]
    public void StockAvailability_Fails_WhenQuantityEqualsZeroStock()
    {
        var check = SafetyRules.CheckStockAvailability(Line(quantity: 5, available: 0, reserved: 0, threshold: 0));

        Assert.False(check.Passed);
    }

    // ── CheckReserveMinimum ───────────────────────────────────────────────────

    [Fact]
    public void ReserveMinimum_Passes_WhenRemainingStockAboveFloor()
    {
        var check = SafetyRules.CheckReserveMinimum(Line(quantity: 50, available: 200, reserved: 40, threshold: 20));

        Assert.True(check.Passed);
    }

    [Fact]
    public void ReserveMinimum_Fails_WhenRemainingStockDropsBelowFloor()
    {
        var check = SafetyRules.CheckReserveMinimum(Line(quantity: 150, available: 200, reserved: 40, threshold: 20));

        Assert.False(check.Passed);
    }

    [Fact]
    public void ReserveMinimum_Fails_WhenStockFullyExhausted()
    {
        var check = SafetyRules.CheckReserveMinimum(Line(quantity: double.MaxValue, available: 100, reserved: 0, threshold: 10));

        Assert.False(check.Passed);
    }

    // ── CheckVehicleCapacity ──────────────────────────────────────────────────

    [Fact]
    public void VehicleCapacity_Passes_WhenTotalLoadFits()
    {
        var plan = new PlanDocument
        {
            WorkflowRunId = Guid.NewGuid(),
            AllocationProposalId = 1,
            VehicleCapacity = 500,
            DistanceKm = 25,
            EtaMinutes = 45,
            Polyline = "encoded",
            OriginLat = 0,
            OriginLng = 0,
            DestLat = 1,
            DestLng = 1,
            Allocations = new[] { Line(100, 200, 0, 10), Line(50, 200, 0, 10) }
        };

        var check = SafetyRules.CheckVehicleCapacity(plan);

        Assert.True(check.Passed);
    }

    [Fact]
    public void VehicleCapacity_Fails_WhenTotalLoadExceedsTruck()
    {
        var plan = new PlanDocument
        {
            WorkflowRunId = Guid.NewGuid(),
            AllocationProposalId = 1,
            VehicleCapacity = 100,
            DistanceKm = 25,
            EtaMinutes = 45,
            Polyline = "encoded",
            OriginLat = 0,
            OriginLng = 0,
            DestLat = 1,
            DestLng = 1,
            Allocations = new[] { Line(80, 200, 0, 10), Line(50, 200, 0, 10) }
        };

        var check = SafetyRules.CheckVehicleCapacity(plan);

        Assert.False(check.Passed);
        Assert.NotNull(check.ViolationDetail);
    }

    // ── CheckCoordinates ──────────────────────────────────────────────────────

    [Fact]
    public void Coordinates_Pass_ForPlausibleRoute()
    {
        var plan = Plan(Guid.NewGuid());

        var check = SafetyRules.CheckCoordinates(plan, Route());

        Assert.True(check.Passed);
    }

    [Fact]
    public void Coordinates_Fail_WhenCoordinatesOutOfBounds()
    {
        var plan = new PlanDocument
        {
            WorkflowRunId = Guid.NewGuid(),
            AllocationProposalId = 1,
            VehicleCapacity = 100,
            DistanceKm = 25,
            EtaMinutes = 45,
            Polyline = "encoded",
            OriginLat = 95,
            OriginLng = 79.8612,
            DestLat = 7.2906,
            DestLng = 80.6337,
            Allocations = Array.Empty<PlanAllocationLine>()
        };

        var check = SafetyRules.CheckCoordinates(plan, Route());

        Assert.False(check.Passed);
        Assert.Equal(SafetyRules.CoordinateSanity, check.CheckName);
    }

    [Fact]
    public void Coordinates_Fail_WhenOriginEqualsDestination()
    {
        var plan = Plan(Guid.NewGuid());

        var check = SafetyRules.CheckCoordinates(plan with { DestLat = plan.OriginLat, DestLng = plan.OriginLng }, Route());

        Assert.False(check.Passed);
    }

    [Fact]
    public void Coordinates_Fail_WhenDistanceZero()
    {
        var plan = Plan(Guid.NewGuid());

        var check = SafetyRules.CheckCoordinates(plan, Route() with { DistanceKm = 0 });

        Assert.False(check.Passed);
    }

    [Fact]
    public void Coordinates_Fail_WhenEtaNegative()
    {
        var plan = Plan(Guid.NewGuid());

        var check = SafetyRules.CheckCoordinates(plan, Route() with { EtaMinutes = -5 });

        Assert.False(check.Passed);
    }

    [Fact]
    public void Coordinates_Fail_WhenDistanceImplausiblyLarge()
    {
        var plan = Plan(Guid.NewGuid());

        var check = SafetyRules.CheckCoordinates(plan, Route() with { DistanceKm = SafetyRules.MaxPlausibleDistanceKm + 1 });

        Assert.False(check.Passed);
    }

    // ── Golden cases (agent evaluation) ───────────────────────────────────────

    [Fact]
    public void RunAll_SafePlanPasses()
    {
        var runId = Guid.NewGuid();
        var plan = Plan(runId, Line(50, 200, 20, 10), Line(30, 100, 0, 10));

        var checks = SafetyRules.RunAll(plan, Route(runId.ToString()));

        Assert.NotEmpty(checks);
        Assert.All(checks, c => Assert.True(c.Passed));
    }

    [Fact]
    public void RunAll_UnsafePlanIsBlocked_OverAllocatedStock()
    {
        // Deliberately unsafe: quantity exceeds free stock AND trips the reserve floor.
        var runId = Guid.NewGuid();
        var plan = Plan(runId, Line(quantity: 180, available: 200, reserved: 40, threshold: 20));

        var checks = SafetyRules.RunAll(plan, Route(runId.ToString()));

        Assert.Contains(checks, c => c.CheckName == SafetyRules.StockAvailability && !c.Passed);
        Assert.Contains(checks, c => c.CheckName == SafetyRules.ReserveMinimumThreshold && !c.Passed);
        Assert.Contains(checks, c => c.CheckName == SafetyRules.CoordinateSanity && c.Passed);
    }

    [Fact]
    public void RunAll_UnsafePlanIsBlocked_OverloadedTruck()
    {
        var thousandsOfLines = Enumerable.Range(1, 10)
            .Select(i => Line(quantity: 1000, available: 5000, reserved: 0, threshold: 100))
            .ToArray();
        var plan = new PlanDocument
        {
            WorkflowRunId = Guid.NewGuid(),
            AllocationProposalId = 1,
            VehicleCapacity = 5000,
            DistanceKm = 25,
            EtaMinutes = 45,
            Polyline = "encoded",
            OriginLat = 6.9,
            OriginLng = 79.9,
            DestLat = 7.1,
            DestLng = 80.0,
            Allocations = thousandsOfLines
        };

        var checks = SafetyRules.RunAll(plan, Route());

        Assert.Contains(checks, c => c.CheckName == SafetyRules.VehicleCapacity && !c.Passed);
    }

    [Fact]
    public void RunAll_SafePlanProducesOverallPass()
    {
        var runId = Guid.NewGuid();
        var plan = Plan(runId, Line(50, 200, 20, 10));

        var checks = SafetyRules.RunAll(plan, Route(runId.ToString()));

        Assert.True(checks.All(c => c.Passed));
        Assert.Equal(4, checks.Count);
    }
}