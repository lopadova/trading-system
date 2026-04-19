using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;

namespace TradingSupervisorService.Repositories;

/// <summary>
/// SQLite implementation of IPositionsRepository using Dapper.
/// Reads from options.db positions table (read-only).
/// All queries use explicit SQL (no ORM).
/// All IO operations have try/catch with logging.
/// </summary>
public sealed class PositionsRepository : IPositionsRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<PositionsRepository> _logger;

    public PositionsRepository(IDbConnectionFactory db, ILogger<PositionsRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all active positions that have Greeks data available.
    /// Filters WHERE delta IS NOT NULL (indicates Greeks have been calculated).
    /// Ordered by symbol ASC for consistent display.
    /// </summary>
    public async Task<IReadOnlyList<ActivePositionRecord>> GetActivePositionsWithGreeksAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT
                position_id AS PositionId,
                campaign_id AS CampaignId,
                symbol AS Symbol,
                contract_symbol AS ContractSymbol,
                strategy_name AS StrategyName,
                quantity AS Quantity,
                entry_price AS EntryPrice,
                current_price AS CurrentPrice,
                unrealized_pnl AS UnrealizedPnl,
                delta AS Delta,
                gamma AS Gamma,
                theta AS Theta,
                vega AS Vega,
                implied_volatility AS ImpliedVolatility,
                greeks_updated_at AS GreeksUpdatedAt,
                underlying_price AS UnderlyingPrice,
                opened_at AS OpenedAt,
                updated_at AS UpdatedAt
            FROM positions
            WHERE delta IS NOT NULL
            ORDER BY symbol ASC
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, cancellationToken: ct);
            IEnumerable<ActivePositionRecord> results = await conn.QueryAsync<ActivePositionRecord>(cmd);

            IReadOnlyList<ActivePositionRecord> positions = results.ToList();

            _logger.LogDebug("Retrieved {Count} active positions with Greeks data", positions.Count);

            return positions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active positions with Greeks");
            throw;
        }
    }

    /// <summary>
    /// Gets all active positions for a specific campaign.
    /// Includes positions both with and without Greeks data.
    /// </summary>
    public async Task<IReadOnlyList<ActivePositionRecord>> GetPositionsByCampaignAsync(string campaignId, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("CampaignId cannot be null or empty", nameof(campaignId));
        }

        const string sql = """
            SELECT
                position_id AS PositionId,
                campaign_id AS CampaignId,
                symbol AS Symbol,
                contract_symbol AS ContractSymbol,
                strategy_name AS StrategyName,
                quantity AS Quantity,
                entry_price AS EntryPrice,
                current_price AS CurrentPrice,
                unrealized_pnl AS UnrealizedPnl,
                delta AS Delta,
                gamma AS Gamma,
                theta AS Theta,
                vega AS Vega,
                implied_volatility AS ImpliedVolatility,
                greeks_updated_at AS GreeksUpdatedAt,
                underlying_price AS UnderlyingPrice,
                opened_at AS OpenedAt,
                updated_at AS UpdatedAt
            FROM positions
            WHERE campaign_id = @CampaignId
            ORDER BY opened_at DESC
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new { CampaignId = campaignId }, cancellationToken: ct);
            IEnumerable<ActivePositionRecord> results = await conn.QueryAsync<ActivePositionRecord>(cmd);

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get positions for campaign {CampaignId}", campaignId);
            throw;
        }
    }

    /// <summary>
    /// Gets a single position by its ID.
    /// Returns null if position not found.
    /// </summary>
    public async Task<ActivePositionRecord?> GetPositionByIdAsync(string positionId, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (string.IsNullOrWhiteSpace(positionId))
        {
            throw new ArgumentException("PositionId cannot be null or empty", nameof(positionId));
        }

        const string sql = """
            SELECT
                position_id AS PositionId,
                campaign_id AS CampaignId,
                symbol AS Symbol,
                contract_symbol AS ContractSymbol,
                strategy_name AS StrategyName,
                quantity AS Quantity,
                entry_price AS EntryPrice,
                current_price AS CurrentPrice,
                unrealized_pnl AS UnrealizedPnl,
                delta AS Delta,
                gamma AS Gamma,
                theta AS Theta,
                vega AS Vega,
                implied_volatility AS ImpliedVolatility,
                greeks_updated_at AS GreeksUpdatedAt,
                underlying_price AS UnderlyingPrice,
                opened_at AS OpenedAt,
                updated_at AS UpdatedAt
            FROM positions
            WHERE position_id = @PositionId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new { PositionId = positionId }, cancellationToken: ct);
            ActivePositionRecord? result = await conn.QuerySingleOrDefaultAsync<ActivePositionRecord>(cmd);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get position {PositionId}", positionId);
            throw;
        }
    }

    /// <summary>
    /// Gets count of active positions grouped by underlying symbol.
    /// Returns a dictionary with symbol as key and count as value.
    /// Useful for dashboard summary widgets showing position distribution.
    /// </summary>
    public async Task<Dictionary<string, int>> GetPositionCountsBySymbolAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT
                symbol AS Symbol,
                COUNT(*) AS Count
            FROM positions
            GROUP BY symbol
            ORDER BY Count DESC
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, cancellationToken: ct);

            // Query returns rows with symbol (string) and count (long from SQLite)
            var results = await conn.QueryAsync<(string Symbol, long Count)>(cmd);

            // Convert to dictionary
            Dictionary<string, int> counts = results.ToDictionary(
                row => row.Symbol,
                row => (int)row.Count);

            _logger.LogDebug("Retrieved position counts for {SymbolCount} symbols", counts.Count);

            return counts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get position counts by symbol");
            throw;
        }
    }
}
