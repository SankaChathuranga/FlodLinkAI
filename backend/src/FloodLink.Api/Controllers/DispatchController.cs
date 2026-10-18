using System.ComponentModel.DataAnnotations;
using FloodLink.Api.Auth;
using FloodLink.Api.DTOs;
using FloodLink.Contracts;
using FloodLink.Domain;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Domain.Security;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Route = FloodLink.Contracts.Route;

namespace FloodLink.Api.Controllers;

/// <summary>
/// Coordinator approval and dispatch endpoints (Member D — Ijini).
/// Enforces the human-in-the-loop checkpoint: a plan can only be dispatched after
/// validation passed and an authorized coordinator explicitly approves it.
/// Decisions and history are Coordinator/Admin only; dispatch status and delivery confirmation
/// are also open to the Volunteer who reported into the run.
/// </summary>
[ApiController]
[Route("api/dispatches")]
[Authorize]
public sealed class DispatchController : ControllerBase
{
    /// <summary>Body contract for POST /api/dispatches/{id}/approve.</summary>
    public sealed record ApproveRequest([StringLength(2000)] string? Notes);

    /// <summary>Body contract for POST /api/dispatches/{id}/reject (Reason required).</summary>
    public sealed record RejectRequest([StringLength(2000)] string Reason);

    /// <summary>Body contract for POST /api/dispatches/{id}/request-revision (Notes required).</summary>
    public sealed record RevisionRequest([StringLength(2000)] string Notes);

    /// <summary>Body contract for POST /api/dispatches/{id}/confirm-delivery (Notes and PhotoReference optional).</summary>
    public sealed record ConfirmDeliveryRequest(
        [StringLength(2000)] string? Notes,
        [StringLength(500)] string? PhotoReference);

    private readonly AppDbContext _db;
    private readonly DispatchService _dispatches;
    private readonly IPhotoStorageService _photos;

    public DispatchController(AppDbContext db, DispatchService dispatches, IPhotoStorageService photos)
    {
        _db = db;
        _dispatches = dispatches;
        _photos = photos;
    }

    /// <summary>Approves a validated plan: commits its reserved stock, records the dispatch and audit entry.</summary>
    [HttpPost("{workflowRunId:guid}/approve")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApproveAsync(Guid workflowRunId, [FromBody] ApproveRequest? request, CancellationToken ct)
    {
        var outcome = await _dispatches.ApproveAsync(workflowRunId, request?.Notes, User.GetUserId(), ct);
        return outcome.Status == DispatchOutcomeStatus.Ok ? Ok(ToResponse(outcome)) : Error(outcome);
    }

    /// <summary>Rejects a plan with a mandatory reason; releases its stock and records the rejection.</summary>
    [HttpPost("{workflowRunId:guid}/reject")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAsync(Guid workflowRunId, [FromBody] RejectRequest? request, CancellationToken ct)
    {
        var outcome = await _dispatches.RejectAsync(workflowRunId, request?.Reason, User.GetUserId(), ct);
        return outcome.Status == DispatchOutcomeStatus.Ok ? Ok(ToResponse(outcome)) : Error(outcome);
    }

    /// <summary>
    /// Sends a plan back for re-matching with mandatory coordinator notes. Its stock is released
    /// and the run is queued; it returns to PendingApproval with a new proposal.
    /// </summary>
    [HttpPost("{workflowRunId:guid}/request-revision")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RequestRevisionAsync(Guid workflowRunId, [FromBody] RevisionRequest? request, CancellationToken ct)
    {
        var outcome = await _dispatches.RequestRevisionAsync(workflowRunId, request?.Notes, User.GetUserId(), ct);
        if (outcome.Status != DispatchOutcomeStatus.Ok)
            return Error(outcome);

        return Ok(new
        {
            dispatchId = outcome.Dispatch!.Id,
            workflowRunId,
            decision = outcome.Dispatch.Decision,
            notes = outcome.Dispatch.ApprovalNotes,
            state = WorkflowState.RevisionRequested,
            loopsBackTo = WorkflowState.Matching
        });
    }

    /// <summary>
    /// Returns the current dispatch status for a workflow run (decision, reason, state).
    /// </summary>
    [HttpGet("by-workflow/{workflowRunId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByWorkflowAsync(Guid workflowRunId, CancellationToken ct)
    {
        var denied = await CheckRunAccessAsync(workflowRunId, ct);
        if (denied is not null)
            return denied;

        var run = await _db.WorkflowRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == workflowRunId, ct);

        if (run is null)
        {
            return NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        }

        // Every coordinator decision on the run, newest first (a revision adds one each time).
        var decisions = await _db.Dispatches.AsNoTracking()
            .Where(d => d.WorkflowRunId == workflowRunId)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new
            {
                dispatchId = d.Id,
                decision = d.Decision,
                notes = d.ApprovalNotes,
                decidedById = d.ApprovedById,
                decidedBy = d.ApprovedBy != null ? d.ApprovedBy.Name : null,
                decidedAt = d.CreatedAt,
                dispatchedAt = d.DispatchedAt
            })
            .ToListAsync(ct);
        var dispatch = decisions.FirstOrDefault();

        var deliveryEvent = await _db.AuditTrail.AsNoTracking()
            .Where(a => a.EventType == "DeliveryConfirmed" && a.Dispatch != null && a.Dispatch.WorkflowRunId == workflowRunId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new { a.CreatedAt })
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            workflowRunId,
            state = run.CurrentState,
            dispatchId = dispatch?.dispatchId,
            decision = dispatch?.decision,
            notes = dispatch?.notes,
            decidedBy = dispatch?.decidedBy,
            decidedAt = dispatch?.decidedAt,
            dispatchedAt = dispatch?.dispatchedAt,
            isDelivered = deliveryEvent is not null,
            deliveredAt = deliveryEvent?.CreatedAt,
            decisions
        });
    }

    /// <summary>
    /// Field-worker delivery confirmation. Only an Approved dispatch can be
    /// confirmed. The spec fixes exactly 9 workflow states, so delivery does not
    /// advance the state machine — it is recorded as an append-only audit event
    /// ("DeliveryConfirmed") that the dispatch-status endpoint surfaces as
    /// isDelivered / deliveredAt, and the run's reports become Resolved. State stays Approved (terminal).
    /// </summary>
    [HttpPost("{workflowRunId:guid}/confirm-delivery")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmDeliveryAsync(Guid workflowRunId, [FromBody] ConfirmDeliveryRequest? request, CancellationToken ct)
    {
        var denied = await CheckRunAccessAsync(workflowRunId, ct);
        if (denied is not null)
            return denied;

        var outcome = await _dispatches.ConfirmDeliveryAsync(
            workflowRunId, request?.Notes, request?.PhotoReference, User.GetUserId(), ct);
        if (outcome.Status != DispatchOutcomeStatus.Ok)
            return Error(outcome);

        return Ok(new { workflowRunId, state = WorkflowState.Approved, isDelivered = true, notes = request?.Notes });
    }

    /// <summary>
    /// POST /api/dispatches/{workflowRunId}/delivery-photo — uploads a photo proving delivery (form field
    /// <c>photo</c>; JPEG, PNG or WebP up to 10 MB) and returns its URL. Send that URL as
    /// <c>photoReference</c> to confirm-delivery. Only for an Approved run, by whoever may confirm it.
    /// </summary>
    [HttpPost("{workflowRunId:guid}/delivery-photo")]
    [RequestSizeLimit(PhotoStorageService.MaxBytes + 64 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadDeliveryPhotoAsync(Guid workflowRunId, IFormFile? photo, CancellationToken ct)
    {
        var denied = await CheckRunAccessAsync(workflowRunId, ct);
        if (denied is not null)
            return denied;

        var state = await _db.WorkflowRuns.AsNoTracking()
            .Where(r => r.Id == workflowRunId)
            .Select(r => (WorkflowState?)r.CurrentState)
            .FirstOrDefaultAsync(ct);
        if (state is null)
            return NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
        if (state != WorkflowState.Approved)
            return Conflict(new { error = "INVALID_STATE", message = $"A delivery photo needs an Approved run; current state is {state}." });
        if (photo is null || photo.Length == 0)
            return BadRequest(new { error = "PHOTO_REQUIRED", message = "Attach the photo as the form field 'photo'." });

        var url = await _photos.SavePhotoAsync(photo, PhotoStorageService.DeliveriesFolder);
        return Created(url, new { workflowRunId, photoUrl = url });
    }

    /// <summary>
    /// Coordinators and Admins may act on any run; a Volunteer only on a run they reported into
    /// (404 otherwise, so other runs' existence is not revealed); every other role gets 403.
    /// </summary>
    private async Task<IActionResult?> CheckRunAccessAsync(Guid workflowRunId, CancellationToken ct)
    {
        if (User.IsCoordinatorOrAdmin())
            return null;
        if (!User.IsInRole(Roles.Volunteer))
            return Forbid();
        return await _db.UserOwnsRunAsync(workflowRunId, User.GetUserId(), ct)
            ? null
            : NotFound(new { error = "WORKFLOW_NOT_FOUND", message = $"No workflow run with id {workflowRunId}." });
    }

    /// <summary>
    /// Returns the approval queue: workflow runs awaiting a coordinator decision.
    /// This powers the coordinator's "Approval Queue" screen (Member D — React).
    /// Only runs in PendingApproval appear — they have already passed validation.
    /// </summary>
    [HttpGet("approval-queue")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApprovalQueueAsync(CancellationToken ct)
    {
        var runs = await _db.WorkflowRuns.AsNoTracking()
            .Where(r => r.CurrentState == WorkflowState.PendingApproval)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        // A one-line summary of each plan so the coordinator can compare before opening one.
        var items = runs.Select(r =>
        {
            var plan = WorkflowPlan.TryRead<ExecutionPlan>(r.PlanJson, WorkflowPlan.ExecutionPlanKey);
            var triage = WorkflowPlan.TryRead<TriagePlan>(r.PlanJson, WorkflowPlan.TriagePlanKey);
            var proposal = WorkflowPlan.TryRead<AllocationProposal>(r.PlanJson, WorkflowPlan.AllocationProposalKey);
            var route = WorkflowPlan.TryRead<Route>(r.PlanJson, WorkflowPlan.RouteKey);
            var validation = WorkflowPlan.TryRead<ValidationResults>(r.PlanJson, WorkflowPlan.ValidationResultsKey);
            return new
            {
                workflowRunId = r.Id,
                objective = r.Objective,
                createdAt = r.CreatedAt,
                state = r.CurrentState,
                reportCount = r.ReportIds?.Count ?? 0,
                shelterCount = proposal?.Allocations.Select(a => a.ShelterId).Distinct().Count() ?? 0,
                allocationCount = proposal?.Allocations.Count ?? 0,
                totalQuantity = proposal?.Allocations.Sum(a => a.Quantity) ?? 0,
                unfulfillableCount = proposal?.Unfulfillable.Count ?? 0,
                distanceKm = route?.DistanceKm,
                etaMinutes = route?.EtaMinutes,
                routeLegs = route?.Legs.Count ?? 0,
                validationPassed = validation?.OverallPassed,
                failedChecks = validation?.Checks.Count(c => !c.Passed) ?? 0,
                planSource = plan?.Source,
                scoringMethod = triage?.ScoringMethod,
                isRevision = WorkflowPlan.TryRead<DispatchService.RevisionRequest>(r.PlanJson, WorkflowPlan.RevisionKey) is not null
            };
        }).ToList();

        return Ok(new { items });
    }

    /// <summary>Lists, filters and paginates dispatch history.</summary>
    [HttpGet]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? status,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        IQueryable<Dispatch> query = _db.Dispatches.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<DispatchDecision>(status, ignoreCase: true, out var decision))
            {
                return BadRequest(new { error = "INVALID_STATUS", message = $"Unknown dispatch status '{status}'." });
            }
            query = query.Where(d => d.Decision == decision);
        }

        if (dateFrom.HasValue) query = query.Where(d => d.CreatedAt >= dateFrom.Value.AsUtc());
        if (dateTo.HasValue) query = query.Where(d => d.CreatedAt <= dateTo.Value.AsUtc());

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new
            {
                d.Id,
                d.WorkflowRunId,
                d.Decision,
                d.ApprovalNotes,
                d.ApprovedById,
                d.DispatchedAt,
                d.CreatedAt
            })
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }

    /// <summary>
    /// Reporting/analytics: decision totals, approval turnaround (workflow start → coordinator decision),
    /// top rejection reasons, and decisions per shelter.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Policy = Policies.Coordinator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SummaryAsync(CancellationToken ct)
    {
        var dispatches = await _db.Dispatches.AsNoTracking()
            .Select(d => new { d.WorkflowRunId, d.Decision, d.ApprovalNotes, d.CreatedAt, RunCreatedAt = d.WorkflowRun!.CreatedAt })
            .ToListAsync(ct);

        // Turnaround: how long from starting the workflow until the coordinator approved it.
        var approvedMinutes = dispatches
            .Where(d => d.Decision == DispatchDecision.Approved)
            .Select(d => Math.Max(0, (d.CreatedAt - d.RunCreatedAt).TotalMinutes))
            .OrderBy(m => m)
            .ToList();

        var rejectionReasons = dispatches
            .Where(d => d.Decision == DispatchDecision.Rejected && d.ApprovalNotes is not null)
            .GroupBy(d => d.ApprovalNotes!)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new { reason = g.Key, count = g.Count() })
            .ToList();

        // A run's shelters are the shelters its allocations deliver to (any revision of the plan).
        var runShelters = await _db.AllocationProposals.AsNoTracking()
            .Select(a => new { a.WorkflowRunId, a.ShelterId, ShelterName = a.Shelter!.Name })
            .Distinct()
            .ToListAsync(ct);
        var perShelter = runShelters
            .GroupBy(s => new { s.ShelterId, s.ShelterName })
            .Select(g =>
            {
                var runs = g.Select(s => s.WorkflowRunId).ToHashSet();
                var decisions = dispatches.Where(d => runs.Contains(d.WorkflowRunId)).ToList();
                return new
                {
                    shelterId = g.Key.ShelterId,
                    shelterName = g.Key.ShelterName,
                    approved = decisions.Count(d => d.Decision == DispatchDecision.Approved),
                    rejected = decisions.Count(d => d.Decision == DispatchDecision.Rejected),
                    revisionsRequested = decisions.Count(d => d.Decision == DispatchDecision.RevisionRequested)
                };
            })
            .Where(s => s.approved + s.rejected + s.revisionsRequested > 0)
            .OrderByDescending(s => s.approved).ThenBy(s => s.shelterName)
            .ToList();

        return Ok(new
        {
            total = dispatches.Count,
            approved = dispatches.Count(d => d.Decision == DispatchDecision.Approved),
            rejected = dispatches.Count(d => d.Decision == DispatchDecision.Rejected),
            revisionsRequested = dispatches.Count(d => d.Decision == DispatchDecision.RevisionRequested),
            avgApprovalMinutes = approvedMinutes.Count == 0 ? null : (double?)approvedMinutes.Average(),
            medianApprovalMinutes = Median(approvedMinutes),
            topRejectionReasons = rejectionReasons,
            dispatchesPerShelter = perShelter
        });
    }

    private static double? Median(IReadOnlyList<double> sorted) => sorted.Count switch
    {
        0 => null,
        var n when n % 2 == 1 => sorted[n / 2],
        var n => (sorted[n / 2 - 1] + sorted[n / 2]) / 2
    };

    private static DispatchResponse ToResponse(DispatchOutcome outcome) => new(
        outcome.Dispatch!.Id,
        outcome.Dispatch.WorkflowRunId,
        outcome.Dispatch.Decision,
        outcome.Dispatch.ApprovalNotes,
        outcome.Dispatch.ApprovedById,
        outcome.Dispatch.DispatchedAt,
        outcome.State!.Value);

    private IActionResult Error(DispatchOutcome outcome)
    {
        var body = new { error = outcome.ErrorCode, message = outcome.Message };
        return outcome.Status switch
        {
            DispatchOutcomeStatus.NotFound => NotFound(body),
            DispatchOutcomeStatus.BadRequest => BadRequest(body),
            _ => Conflict(body)
        };
    }

    private sealed record DispatchResponse(
        Guid DispatchId,
        Guid WorkflowRunId,
        DispatchDecision Decision,
        string? ApprovalNotes,
        int? ApprovedById,
        DateTime? DispatchedAt,
        WorkflowState State);
}