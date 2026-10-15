using FloodLink.Api.DTOs;
using FloodLink.Domain.Entities;
using FloodLink.Domain.Enums;
using FloodLink.Domain.Exceptions;
using FloodLink.Domain.Services;
using FloodLink.Infrastructure;
using FloodLink.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Services;

/// <summary>Filters, search, sort and paging for GET /api/reports.</summary>
public sealed record ReportQuery(
    int? ShelterId = null,
    string? Status = null,
    int? MinUrgency = null,
    string? Search = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize);

/// <summary>
/// Field reports (Member A's business operation): submission with rule-based urgency scoring,
/// coordinator edits, and the report queries behind <c>ReportsController</c>.
/// </summary>
/// <remarks>Throws the domain exceptions (<see cref="NotFoundException"/> etc.), which the global middleware maps to HTTP.</remarks>
public sealed class ReportService(
    AppDbContext db,
    IUrgencyScoringService urgencyScoring,
    IPhotoStorageService photoStorage,
    ILogger<ReportService> logger)
{
    /// <summary>Sort keys accepted by <see cref="SearchAsync"/>; anything else sorts newest first.</summary>
    public static readonly IReadOnlyList<string> SortKeys =
        ["newest", "oldest", "urgency_desc", "urgency_asc", "quantity_desc", "quantity_asc"];

    /// <summary>
    /// One page of reports. <see cref="ReportQuery.Search"/> matches the need type, notes or shelter
    /// name (case-insensitive) or an exact report id.
    /// </summary>
    public async Task<PaginatedResult<Report>> SearchAsync(ReportQuery q, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(q.Page, q.PageSize);
        IQueryable<Report> query = db.Reports
            .Include(r => r.Shelter)
            .Include(r => r.Reporter)
            .AsNoTracking();

        if (q.ShelterId.HasValue)
            query = query.Where(r => r.ShelterId == q.ShelterId.Value);

        if (!string.IsNullOrWhiteSpace(q.Status))
        {
            var status = q.Status.Trim().ToLower();
            query = query.Where(r => r.Status.ToLower() == status);
        }

        if (q.MinUrgency.HasValue)
            query = query.Where(r => r.UrgencyLevel >= q.MinUrgency.Value);

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLower();
            var id = int.TryParse(term.TrimStart('#', 'r', '-'), out var parsed) ? parsed : (int?)null;
            query = query.Where(r =>
                r.NeedType.ToLower().Contains(term) ||
                (r.Notes != null && r.Notes.ToLower().Contains(term)) ||
                (r.Shelter != null && r.Shelter.Name.ToLower().Contains(term)) ||
                (id != null && r.Id == id));
        }

        query = (q.Sort?.Trim().ToLower()) switch
        {
            "urgency" or "urgency_desc" => query.OrderByDescending(r => r.UrgencyLevel).ThenByDescending(r => r.CreatedAt),
            "urgency_asc" => query.OrderBy(r => r.UrgencyLevel).ThenByDescending(r => r.CreatedAt),
            "quantity_desc" => query.OrderByDescending(r => r.QuantityNeeded).ThenByDescending(r => r.CreatedAt),
            "quantity_asc" => query.OrderBy(r => r.QuantityNeeded).ThenByDescending(r => r.CreatedAt),
            "oldest" or "created_asc" => query.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id),
            _ => query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
        };

        return await query.ToPageAsync(page, pageSize, ct);
    }

    /// <summary>Every persisted triage plan, newest first.</summary>
    public async Task<List<TriagePlanEntity>> GetTriagePlansAsync(CancellationToken ct = default)
        => await db.TriagePlans.OrderByDescending(t => t.CreatedAt).AsNoTracking().ToListAsync(ct);

    /// <summary>A volunteer's own reports, newest first, with the stage and latest decision of the run handling each.</summary>
    public async Task<List<MyReportDto>> GetMineAsync(int userId, CancellationToken ct = default)
    {
        var rows = await db.Reports.AsNoTracking()
            .Where(r => r.ReportedBy == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                Report = r,
                ShelterName = r.Shelter != null ? r.Shelter.Name : null,
                WorkflowState = r.WorkflowRun != null ? (WorkflowState?)r.WorkflowRun.CurrentState : null,
                Decision = db.Dispatches
                    .Where(d => d.WorkflowRunId == r.WorkflowRunId)
                    .OrderByDescending(d => d.CreatedAt)
                    .Select(d => (DispatchDecision?)d.Decision)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(x => MyReportDto.From(x.Report, x.ShelterName, x.WorkflowState, x.Decision)).ToList();
    }

    /// <summary>
    /// One report with its shelter and reporter. When <paramref name="ownerId"/> is set, only that
    /// user's report is returned; anyone else's is reported as not found.
    /// </summary>
    public async Task<Report> GetAsync(int id, int? ownerId = null, CancellationToken ct = default)
    {
        var report = await db.Reports
            .Include(r => r.Shelter)
            .Include(r => r.Reporter)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        if (report is null || (ownerId.HasValue && report.ReportedBy != ownerId.Value))
            throw new NotFoundException($"Report with ID {id} was not found.");
        return report;
    }

    /// <summary>True when a report with this id exists.</summary>
    public Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => db.Reports.AnyAsync(r => r.Id == id, ct);

    /// <summary>
    /// Submits a field report for <paramref name="reporterId"/> (always the signed-in user). Stores the
    /// photo, scores urgency with the rule-based scorer (unless an explicit level is given) and
    /// defaults the GPS position to the shelter's.
    /// </summary>
    public async Task<Report> SubmitAsync(CreateReportDto dto, int reporterId, CancellationToken ct = default)
    {
        var shelter = await db.Shelters.FirstOrDefaultAsync(s => s.Id == dto.ShelterId, ct)
            ?? throw new BadRequestException($"Shelter with ID {dto.ShelterId} does not exist.");

        if (!await db.Users.AnyAsync(u => u.Id == reporterId, ct))
            throw new UnauthorizedException("The signed-in user no longer exists.");

        var photoUrl = dto.PhotoUrl;
        if (dto.Photo != null)
        {
            var uploadedPath = await photoStorage.SavePhotoAsync(dto.Photo);
            if (!string.IsNullOrEmpty(uploadedPath))
                photoUrl = uploadedPath;
        }

        // Time since the shelter was last resupplied raises urgency.
        var lastResupplyUtc = await db.Reports
            .Where(r => r.ShelterId == dto.ShelterId && r.Status == "Resolved")
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => (DateTime?)r.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var autoUrgency = urgencyScoring.CalculateUrgencyScore(shelter, dto.NeedType, lastResupplyUtc);
        var urgency = dto.UrgencyLevel.HasValue ? Math.Clamp(dto.UrgencyLevel.Value, 0, 100) : autoUrgency;

        var report = new Report
        {
            ShelterId = dto.ShelterId,
            ReportedBy = reporterId,
            NeedType = dto.NeedType,
            QuantityNeeded = dto.QuantityNeeded,
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            UrgencyLevel = urgency,
            PhotoUrl = photoUrl,
            GpsLat = dto.GpsLat ?? shelter.Latitude,
            GpsLng = dto.GpsLng ?? shelter.Longitude,
            Status = dto.Status,
            CreatedAt = DateTime.UtcNow
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Report {ReportId} submitted by user {UserId} for shelter {ShelterId}: {NeedType} x{Quantity}, urgency {Urgency} ({UrgencySource})",
            report.Id, reporterId, report.ShelterId, report.NeedType, report.QuantityNeeded, report.UrgencyLevel,
            dto.UrgencyLevel.HasValue ? "given" : "rule-based");
        return report;
    }

    /// <summary>Coordinator edit of a report's need, quantity, urgency, photo, position or status.</summary>
    public async Task<Report> UpdateAsync(int id, UpdateReportDto dto, int? editorId, CancellationToken ct = default)
    {
        var report = await db.Reports.FindAsync([id], ct)
            ?? throw new NotFoundException($"Report with ID {id} was not found.");

        if (dto.Photo != null)
        {
            var uploadedPath = await photoStorage.SavePhotoAsync(dto.Photo);
            if (!string.IsNullOrEmpty(uploadedPath))
                report.PhotoUrl = uploadedPath;
        }
        else if (!string.IsNullOrEmpty(dto.PhotoUrl))
        {
            report.PhotoUrl = dto.PhotoUrl;
        }

        report.NeedType = dto.NeedType;
        report.QuantityNeeded = dto.QuantityNeeded;
        report.Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim();
        report.UrgencyLevel = dto.UrgencyLevel;
        report.GpsLat = dto.GpsLat ?? report.GpsLat;
        report.GpsLng = dto.GpsLng ?? report.GpsLng;
        report.Status = dto.Status;

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Report {ReportId} updated by user {UserId}: status {Status}, urgency {Urgency}",
            report.Id, editorId, report.Status, report.UrgencyLevel);
        return report;
    }
}
