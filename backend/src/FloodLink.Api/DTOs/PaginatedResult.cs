using Microsoft.EntityFrameworkCore;

namespace FloodLink.Api.DTOs;

public class PaginatedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>Paging shared by every list endpoint: page from 1, page size 1–100 (out of range → default).</summary>
public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Clamps <paramref name="page"/> to ≥ 1 and resets an out-of-range page size to the default.</summary>
    public static (int Page, int PageSize) Normalize(int page, int pageSize, int defaultPageSize = DefaultPageSize)
        => (Math.Max(1, page), pageSize is < 1 or > MaxPageSize ? defaultPageSize : pageSize);

    /// <summary>Counts <paramref name="query"/>, then reads one page of it (the query must already be ordered).</summary>
    public static async Task<PaginatedResult<T>> ToPageAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PaginatedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalItems = total,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        };
    }
}
