using System.Diagnostics;
using System.Text.Json;
using FloodLink.Contracts;
using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Agents.Validation;

/// <summary>
/// Deterministic rule-based implementation of <see cref="IValidationAgent"/>.
/// Owned by Member D (Ijini). No LLM call — pure <see cref="SafetyRules"/> checks
/// against the accumulated plan stored in the workflow run.
/// </summary>
public sealed class ValidationAgent : IValidationAgent
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Capacity of the single relief truck assumed per dispatch until vehicles are modelled.
    /// </summary>
    public const double DefaultVehicleCapacity = 1000;

    private readonly AppDbContext _db;

    public ValidationAgent(AppDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<AgentResult<ValidationResults>> ExecuteAsync(
        Route route,
        CancellationToken cancellationToken = default)
    {
        var run = await _db.WorkflowRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.Id == route.WorkflowRunId, cancellationToken);

        if (run is null)
        {
            return AgentResult<ValidationResults>.Fail(
                "WORKFLOW_NOT_FOUND",
                $"No workflow run with id {route.WorkflowRunId}.");
        }

        if (string.IsNullOrWhiteSpace(run.PlanJson))
        {
            return AgentResult<ValidationResults>.Fail(
                "PLAN_NOT_FOUND",
                $"Workflow run {route.WorkflowRunId} has no PlanJson to validate.");
        }

        var toolCalls = new List<ToolCall>();
        PlanDocument plan;
        try
        {
            // The orchestrator stores agent outputs under keys (triagePlan, allocationProposal, route).
            // Build the validation input from that; otherwise accept an already-flattened PlanDocument.
            plan = await TryBuildFromOrchestratorPlanAsync(run.PlanJson, route, toolCalls, cancellationToken)
                ?? JsonSerializer.Deserialize<PlanDocument>(run.PlanJson, JsonOptions)
                ?? throw new JsonException("Plan parsed to null.");
        }
        catch (JsonException ex)
        {
            return AgentResult<ValidationResults>.Fail(
                "INVALID_PLAN",
                $"Workflow run {route.WorkflowRunId} PlanJson is not a valid plan document: {ex.Message}")
                .WithToolCalls(toolCalls);
        }

        if (plan.WorkflowRunId != route.WorkflowRunId)
        {
            return AgentResult<ValidationResults>.Fail(
                "INVALID_PLAN",
                $"Plan document targets workflow {plan.WorkflowRunId}, expected {route.WorkflowRunId}.");
        }

        var checks = SafetyRules.RunAll(plan, route);
        var overallPassed = checks.All(c => c.Passed);
        toolCalls.Add(new ToolCall
        {
            Tool = "rules.safety",
            Input = new Dictionary<string, object?> { ["allocationLines"] = plan.Allocations.Count, ["legs"] = plan.Legs.Count },
            Output = new Dictionary<string, object?>
            {
                ["checks"] = checks.Count,
                ["failed"] = checks.Count(c => !c.Passed),
                ["overallPassed"] = overallPassed
            },
            Succeeded = true
        });

        return AgentResult<ValidationResults>.Ok(new ValidationResults
        {
            WorkflowRunId = route.WorkflowRunId,
            Checks = checks,
            OverallPassed = overallPassed
        }).WithToolCalls(toolCalls);
    }

    /// <summary>
    /// Builds a <see cref="PlanDocument"/> from the orchestrator's keyed PlanJson, re-reading the
    /// live inventory (available, reserved by other plans, reserve floor) and the coordinates of
    /// every depot → shelter leg. Returns null when PlanJson has no <c>allocationProposal</c> key
    /// (i.e. it is already a flattened PlanDocument).
    /// </summary>
    private async Task<PlanDocument?> TryBuildFromOrchestratorPlanAsync(
        string planJson, Route route, List<ToolCall> toolCalls, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(planJson);
        if (!doc.RootElement.TryGetProperty("allocationProposal", out var proposalJson))
            return null;

        var proposal = proposalJson.Deserialize<AllocationProposal>(JsonOptions)
            ?? throw new JsonException("allocationProposal parsed to null.");
        if (proposal.Allocations.Count == 0)
            throw new JsonException("allocationProposal contains no allocations.");

        // Tool: read-only inventory and location lookup.
        var sw = Stopwatch.StartNew();
        var depotIds = proposal.Allocations.Select(a => a.DepotId).Distinct().ToList();
        var shelterIds = proposal.Allocations.Select(a => a.ShelterId).Distinct().ToList();
        var stock = await _db.InventoryItems.AsNoTracking()
            .Where(i => depotIds.Contains(i.DepotId))
            .ToListAsync(ct);
        var depots = await _db.Depots.AsNoTracking()
            .Where(d => depotIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, ct);
        var shelters = await _db.Shelters.AsNoTracking()
            .Where(s => shelterIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);
        toolCalls.Add(new ToolCall
        {
            Tool = "db.inventory.read",
            Input = new Dictionary<string, object?> { ["depotIds"] = depotIds, ["shelterIds"] = shelterIds },
            Output = new Dictionary<string, object?> { ["stockLines"] = stock.Count },
            Succeeded = true,
            DurationMs = sw.ElapsedMilliseconds
        });

        var lines = proposal.Allocations.Select(a =>
        {
            var item = stock.FirstOrDefault(i => i.DepotId == a.DepotId && i.ItemName == a.ItemName);
            return new PlanAllocationLine
            {
                DepotId = a.DepotId,
                ShelterId = a.ShelterId,
                ItemName = a.ItemName,
                Quantity = a.Quantity,
                QuantityAvailable = item?.QuantityAvailable ?? 0,
                QuantityReserved = item?.QuantityReserved ?? 0,
                ReorderThreshold = item?.ReorderThreshold ?? 0
            };
        }).ToList();

        // Legs come from the route; routes from before multi-leg routing describe the first pair only.
        var routeLegs = route.Legs.Count > 0
            ? route.Legs
            :
            [
                new RouteLeg
                {
                    DepotId = proposal.Allocations[0].DepotId,
                    ShelterId = proposal.Allocations[0].ShelterId,
                    DistanceKm = route.DistanceKm,
                    EtaMinutes = route.EtaMinutes,
                    Polyline = route.Polyline
                }
            ];

        var legs = new List<PlanRouteLeg>();
        foreach (var leg in routeLegs)
        {
            if (!depots.TryGetValue(leg.DepotId, out var depot) || !shelters.TryGetValue(leg.ShelterId, out var shelter))
                throw new JsonException($"Depot {leg.DepotId} or shelter {leg.ShelterId} in the route does not exist.");

            legs.Add(new PlanRouteLeg
            {
                DepotId = leg.DepotId,
                ShelterId = leg.ShelterId,
                OriginLat = depot.Latitude,
                OriginLng = depot.Longitude,
                DestLat = shelter.Latitude,
                DestLng = shelter.Longitude,
                DistanceKm = leg.DistanceKm,
                EtaMinutes = leg.EtaMinutes,
                Load = lines.Where(l => l.DepotId == leg.DepotId && l.ShelterId == leg.ShelterId).Sum(l => l.Quantity)
            });
        }

        var first = legs[0];
        return new PlanDocument
        {
            WorkflowRunId = route.WorkflowRunId,
            AllocationProposalId = proposal.AllocationProposalId,
            VehicleCapacity = DefaultVehicleCapacity,
            DistanceKm = route.DistanceKm,
            EtaMinutes = route.EtaMinutes,
            Polyline = route.Polyline,
            OriginLat = first.OriginLat,
            OriginLng = first.OriginLng,
            DestLat = first.DestLat,
            DestLng = first.DestLng,
            Allocations = lines,
            Legs = route.Legs.Count > 0 ? legs : []
        };
    }
}
