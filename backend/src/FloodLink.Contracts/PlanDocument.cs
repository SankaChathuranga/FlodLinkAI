namespace FloodLink.Contracts;

/// <summary>
/// The accumulated plan document produced by the orchestration pipeline and stored
/// in <c>WorkflowRun.PlanJson</c>. Owned by Member D (Ijini) for the Validation/Safety
/// Agent's input contract.
/// </summary>
/// <remarks>
/// Each agent contributes a section as the run progresses: Member A's TriagePlan,
/// Member B's AllocationProposal (expanded with live per-line inventory context),
/// Member C's Route/ETA plus origin/destination coordinates. Serialized with
/// PascalCase property names (System.Text.Json default) — deserialization here is
/// case-insensitive to tolerate both. Do not rename fields without updating the
/// Validation Agent consumers.
/// </remarks>
public record PlanDocument
{
    /// <summary>The workflow run this plan belongs to.</summary>
    public required Guid WorkflowRunId { get; init; }

    /// <summary>Allocation proposal id this plan was built from (Member B/C wiring).</summary>
    public required int AllocationProposalId { get; init; }

    /// <summary>Total vehicle/truck capacity in the same unit as allocation quantities.</summary>
    public required double VehicleCapacity { get; init; }

    /// <summary>Road distance in kilometres as returned by the Route/ETA agent.</summary>
    public required double DistanceKm { get; init; }

    /// <summary>Estimated travel time in minutes as returned by the Route/ETA agent.</summary>
    public required double EtaMinutes { get; init; }

    /// <summary>Encoded route polyline for map display.</summary>
    public required string Polyline { get; init; }

    /// <summary>Depot (origin) latitude. Set by Member B's proposal routing context.</summary>
    public required double OriginLat { get; init; }

    /// <summary>Depot (origin) longitude.</summary>
    public required double OriginLng { get; init; }

    /// <summary>Shelter (destination) latitude. Set by Member B's proposal routing context.</summary>
    public required double DestLat { get; init; }

    /// <summary>Shelter (destination) longitude.</summary>
    public required double DestLng { get; init; }

    /// <summary>
    /// The concrete allocations proposed by the Logistics/Matching Agent (Member B),
    /// each enriched with the live inventory snapshot used to decide the allocation.
    /// </summary>
    public required IReadOnlyList<PlanAllocationLine> Allocations { get; init; }
}

/// <summary>
/// A single allocation line enriched with the inventory state it was decided against,
/// so the Validation/Safety Agent can re-verify stock deterministically without a
/// second live query.
/// </summary>
public record PlanAllocationLine
{
    /// <summary>The depot supplying this item.</summary>
    public required int DepotId { get; init; }

    /// <summary>The shelter receiving this item.</summary>
    public required int ShelterId { get; init; }

    /// <summary>The name of the item being allocated.</summary>
    public required string ItemName { get; init; }

    /// <summary>The quantity being allocated.</summary>
    public required double Quantity { get; init; }

    /// <summary>Quantity on hand of this item at the depot at allocation time.</summary>
    public required double QuantityAvailable { get; init; }

    /// <summary>Quantity already committed to other reservations at this depot.</summary>
    public required double QuantityReserved { get; init; }

    /// <summary>
    /// The depot's reorder/reserve floor — after allocation, remaining free stock
    /// must not drop below this value.
    /// </summary>
    public required double ReorderThreshold { get; init; }
}