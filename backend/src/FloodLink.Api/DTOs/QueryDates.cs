namespace FloodLink.Api.DTOs;

/// <summary>Date filters from query strings, made safe to compare with UTC (timestamptz) columns.</summary>
public static class QueryDates
{
    /// <summary>A date without a zone (e.g. <c>2026-10-15</c>) is taken as UTC; one with a zone is converted.</summary>
    public static DateTime AsUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
