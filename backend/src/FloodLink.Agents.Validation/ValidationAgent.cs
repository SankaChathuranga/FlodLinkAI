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

        PlanDocument plan;
        try
        {
            // The orchestrator stores agent outputs under keys (triagePlan, allocationProposal, route).
            // Build the validation input from that; otherwise accept an already-flattened PlanDocument.
            plan = await TryBuildFromOrchestratorPlanAsync(run.PlanJson, route, cancellationToken)
                ?? JsonSerializer.Deserialize<PlanDocument>(run.PlanJson, JsonOptions)
                ?? throw new JsonException("Plan parsed to null.");
        }
        catch (JsonException ex)
        {
            return AgentResult<ValidationResults>.Fail(
                "INVALID_PLAN",
                $"Workflow run {route.WorkflowRunId} PlanJson is not a valid plan document: {ex.Message}");
        }

        if (plan.WorkflowRunId != route.WorkflowRunId)
        {
            return AgentResult<ValidationResults>.Fail(
                "INVALID_PLAN",
                $"Plan document targets workflow {plan.WorkflowRunId}, expected {route.WorkflowRunId}.");
        }

        var checks = SafetyRules.RunAll(plan, route);
        var overallPassed = checks.All(c => c.Passed);

        return AgentResult<ValidationResults>.Ok(new ValidationResults
        {
            WorkflowRunId = route.WorkflowRunId,
            Checks = checks,
            OverallPassed = overallPassed
        });
    }

    /// <summary>
    /// Builds a <see cref="PlanDocument"/> from the orchestrator's keyed PlanJson, re-reading the
    /// live inventory and depot/shelter coordinates. Returns null when PlanJson has no
    /// <c>allocationProposal</c> key (i.e. it is already a flattened PlanDocument).
    /// </summary>
    private async Task<PlanDocument?> TryBuildFromOrchestratorPlanAsync(
        string planJson, Route route, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(planJson);
        if (!doc.RootElement.TryGetProperty("allocationProposal", out var proposalJson))
            return null;

        var proposal = proposalJson.Deserialize<AllocationProposal>(JsonOptions)
            ?? throw new JsonException("allocationProposal parsed to null.");
        if (proposal.Allocations.Count == 0)
            throw new JsonException("allocationProposal contains no allocations.");

        var lines = new List<PlanAllocationLine>();
        foreach (var a in proposal.Allocations)
        {
            var available = await _db.InventoryItems.AsNoTracking()
                .Where(i => i.DepotId == a.DepotId && i.ItemName == a.ItemName)
                .SumAsync(i => i.QuantityAvailable, ct);
            lines.Add(new PlanAllocationLine
            {
                DepotId = a.DepotId,
                ShelterId = a.ShelterId,
                ItemName = a.ItemName,
                Quantity = a.Quantity,
                QuantityAvailable = available,
                QuantityReserved = 0,
                ReorderThreshold = 0
            });
        }

        var first = proposal.Allocations[0];
        var depot = await _db.Depots.AsNoTracking().FirstAsync(d => d.Id == first.DepotId, ct);
        var shelter = await _db.Shelters.AsNoTracking().FirstAsync(s => s.Id == first.ShelterId, ct);

        return new PlanDocument
        {
            WorkflowRunId = route.WorkflowRunId,
            AllocationProposalId = proposal.AllocationProposalId,
            VehicleCapacity = DefaultVehicleCapacity,
            DistanceKm = route.DistanceKm,
            EtaMinutes = route.EtaMinutes,
            Polyline = route.Polyline,
            OriginLat = depot.Latitude,
            OriginLng = depot.Longitude,
            DestLat = shelter.Latitude,
            DestLng = shelter.Longitude,
            Allocations = lines
        };
    }
}
