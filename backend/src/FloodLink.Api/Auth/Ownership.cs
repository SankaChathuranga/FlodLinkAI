using FloodLink.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.Auth;

/// <summary>Ownership queries behind the "Volunteer sees only their own" rules.</summary>
public static class Ownership
{
    /// <summary>
    /// True when <paramref name="userId"/> reported at least one report handled by the run, whether
    /// listed in the run's <c>ReportIds</c> or pointing at it through <c>Report.WorkflowRunId</c>.
    /// </summary>
    public static async Task<bool> UserOwnsRunAsync(this AppDbContext db, Guid runId, int? userId, CancellationToken ct)
    {
        if (userId is null)
        {
            return false;
        }

        var reportIds = await db.WorkflowRuns.AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => r.ReportIds)
            .FirstOrDefaultAsync(ct) ?? [];

        return await db.Reports.AsNoTracking().AnyAsync(
            r => r.ReportedBy == userId && (r.WorkflowRunId == runId || reportIds.Contains(r.Id)), ct);
    }
}
