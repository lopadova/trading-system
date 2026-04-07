namespace TradingSupervisorService.Repositories;

/// <summary>
/// Data model for active_positions table records (read-only view from options.db).
/// Used by TradingSupervisorService to monitor positions and Greeks.
/// </summary>
public sealed record ActivePositionRecord
{
    public string PositionId { get; init; } = string.Empty;
    public string CampaignId { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string ContractSymbol { get; init; } = string.Empty;
    public string StrategyName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public double EntryPrice { get; init; }
    public double? CurrentPrice { get; init; }
    public double? UnrealizedPnl { get; init; }
    public double? Delta { get; init; }
    public double? Gamma { get; init; }
    public double? Theta { get; init; }
    public double? Vega { get; init; }
    public double? ImpliedVolatility { get; init; }
    public string? GreeksUpdatedAt { get; init; }  // ISO8601
    public double? UnderlyingPrice { get; init; }
    public string OpenedAt { get; init; } = string.Empty;  // ISO8601
    public string UpdatedAt { get; init; } = string.Empty;  // ISO8601
}

/// <summary>
/// Repository interface for reading active_positions from options.db.
/// TradingSupervisorService uses this to monitor positions for risk alerts.
/// This is a READ-ONLY view - positions are managed by OptionsExecutionService.
/// </summary>
public interface IPositionsRepository
{
    /// <summary>
    /// Gets all active positions with Greeks data.
    /// Filters out positions where Greeks are null (not yet calculated).
    /// </summary>
    Task<IReadOnlyList<ActivePositionRecord>> GetActivePositionsWithGreeksAsync(CancellationToken ct);

    /// <summary>
    /// Gets all active positions for a specific campaign.
    /// </summary>
    Task<IReadOnlyList<ActivePositionRecord>> GetPositionsByCampaignAsync(string campaignId, CancellationToken ct);

    /// <summary>
    /// Gets a single position by ID.
    /// Returns null if not found.
    /// </summary>
    Task<ActivePositionRecord?> GetPositionByIdAsync(string positionId, CancellationToken ct);

    /// <summary>
    /// Gets count of active positions grouped by symbol.
    /// Used for dashboard summary widgets.
    /// </summary>
    Task<Dictionary<string, int>> GetPositionCountsBySymbolAsync(CancellationToken ct);
}
