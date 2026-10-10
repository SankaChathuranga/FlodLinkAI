using FloodLink.Contracts;

namespace FloodLink.Domain;

/// <summary>
/// Moves a run's allocation proposals through the stock lifecycle:
/// Proposed → Reserved (plan passed validation) → Committed (coordinator approved),
/// or Reserved/Proposed → Released (rejected, sent for revision, or failed).
/// Implementations must be safe under concurrent requests (no over-allocation).
/// </summary>
public interface IStockReservationService
{
    /// <summary>Reserves free stock for every proposed allocation of the run, or none of it.</summary>
    Task<StockOperationResult> ReserveForRunAsync(Guid workflowRunId, CancellationToken cancellationToken = default);

    /// <summary>Returns the run's reserved (and still-proposed) stock to free stock.</summary>
    Task<StockOperationResult> ReleaseForRunAsync(Guid workflowRunId, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of a stock operation, with the tool calls made, for the execution log.</summary>
public sealed record StockOperationResult(
    bool Succeeded,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    IReadOnlyList<ToolCall>? ToolCalls = null)
{
    public static StockOperationResult Ok(IReadOnlyList<ToolCall>? toolCalls = null) => new(true, ToolCalls: toolCalls);

    public static StockOperationResult Fail(string code, string message, IReadOnlyList<ToolCall>? toolCalls = null) =>
        new(false, code, message, toolCalls);
}
