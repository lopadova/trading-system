using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Domain;

namespace TradingSupervisorService.Repositories;

/// <summary>
/// Repository implementation for IVTS monitoring data.
/// Stores snapshots in ivts_snapshots table and alerts in alert_history table.
/// </summary>
public sealed class IvtsRepository : IIvtsRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<IvtsRepository> _logger;

    public IvtsRepository(IDbConnectionFactory db, ILogger<IvtsRepository> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InsertSnapshotAsync(IvtsSnapshot snapshot, CancellationToken ct = default)
    {
        // Negative-first: validate input
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        if (string.IsNullOrWhiteSpace(snapshot.SnapshotId))
        {
            throw new ArgumentException("SnapshotId cannot be empty", nameof(snapshot));
        }

        if (string.IsNullOrWhiteSpace(snapshot.Symbol))
        {
            throw new ArgumentException("Symbol cannot be empty", nameof(snapshot));
        }

        const string sql = """
            INSERT INTO ivts_snapshots (
                snapshot_id, symbol, timestamp_utc,
                iv_30d, iv_60d, iv_90d, iv_120d,
                ivr_percentile, term_structure_slope, is_inverted,
                iv_min_52w, iv_max_52w, created_at
            )
            VALUES (
                @SnapshotId, @Symbol, @TimestampUtc,
                @Iv30d, @Iv60d, @Iv90d, @Iv120d,
                @IvrPercentile, @TermStructureSlope, @IsInverted,
                @IvMin52Week, @IvMax52Week, @CreatedAt
            )
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                commandText: sql,
                parameters: new
                {
                    snapshot.SnapshotId,
                    snapshot.Symbol,
                    snapshot.TimestampUtc,
                    snapshot.Iv30d,
                    snapshot.Iv60d,
                    snapshot.Iv90d,
                    snapshot.Iv120d,
                    snapshot.IvrPercentile,
                    snapshot.TermStructureSlope,
                    IsInverted = snapshot.IsInverted ? 1 : 0,  // SQLite uses INTEGER for boolean
                    snapshot.IvMin52Week,
                    snapshot.IvMax52Week,
                    snapshot.CreatedAt
                },
                cancellationToken: ct
            ));

            _logger.LogDebug(
                "IVTS snapshot inserted: {Symbol} IVR={Ivr:P0} Slope={Slope:F4} Inverted={Inverted}",
                snapshot.Symbol, snapshot.IvrPercentile, snapshot.TermStructureSlope, snapshot.IsInverted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert IVTS snapshot for {Symbol}", snapshot.Symbol);
            throw;
        }
    }

    public async Task<IvtsSnapshot?> GetLatestSnapshotAsync(string symbol, CancellationToken ct = default)
    {
        // Negative-first: validate input
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        }

        const string sql = """
            SELECT
                snapshot_id AS SnapshotId,
                symbol AS Symbol,
                timestamp_utc AS TimestampUtc,
                iv_30d AS Iv30d,
                iv_60d AS Iv60d,
                iv_90d AS Iv90d,
                iv_120d AS Iv120d,
                ivr_percentile AS IvrPercentile,
                term_structure_slope AS TermStructureSlope,
                is_inverted AS IsInverted,
                iv_min_52w AS IvMin52Week,
                iv_max_52w AS IvMax52Week,
                created_at AS CreatedAt
            FROM ivts_snapshots
            WHERE symbol = @Symbol
            ORDER BY timestamp_utc DESC
            LIMIT 1
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            IvtsSnapshot? snapshot = await conn.QueryFirstOrDefaultAsync<IvtsSnapshot>(
                new CommandDefinition(
                    commandText: sql,
                    parameters: new { Symbol = symbol },
                    cancellationToken: ct
                ));

            return snapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get latest IVTS snapshot for {Symbol}", symbol);
            throw;
        }
    }

    public async Task<(double MinIv, double MaxIv)?> Get52WeekIvRangeAsync(string symbol, CancellationToken ct = default)
    {
        // Negative-first: validate input
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        }

        // Calculate average of IV30d and IV60d as "current IV" for each snapshot
        // Then find min/max over the past 52 weeks (365 days)
        const string sql = """
            SELECT
                MIN((iv_30d + iv_60d) / 2.0) AS MinIv,
                MAX((iv_30d + iv_60d) / 2.0) AS MaxIv
            FROM ivts_snapshots
            WHERE symbol = @Symbol
              AND timestamp_utc >= datetime('now', '-365 days')
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            dynamic? result = await conn.QueryFirstOrDefaultAsync(
                new CommandDefinition(
                    commandText: sql,
                    parameters: new { Symbol = symbol },
                    cancellationToken: ct
                ));

            // Check if we got valid results
            if (result is null || result.MinIv is null || result.MaxIv is null)
            {
                return null;  // Insufficient data
            }

            double minIv = (double)result.MinIv;
            double maxIv = (double)result.MaxIv;

            // Sanity check: min should be less than max
            if (minIv >= maxIv)
            {
                _logger.LogWarning(
                    "Invalid IV range for {Symbol}: Min={Min} Max={Max}",
                    symbol, minIv, maxIv);
                return null;
            }

            return (minIv, maxIv);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get 52-week IV range for {Symbol}", symbol);
            throw;
        }
    }

    public async Task InsertAlertAsync(IvtsAlert alert, CancellationToken ct = default)
    {
        // Negative-first: validate input
        if (alert is null)
        {
            throw new ArgumentNullException(nameof(alert));
        }

        if (string.IsNullOrWhiteSpace(alert.AlertId))
        {
            throw new ArgumentException("AlertId cannot be empty", nameof(alert));
        }

        // Insert into alert_history table (shared with other alert types)
        const string sql = """
            INSERT INTO alert_history (
                alert_id, alert_type, severity, message,
                details_json, source_service, created_at,
                resolved_at, resolved_by
            )
            VALUES (
                @AlertId, @AlertType, @Severity, @Message,
                @DetailsJson, @SourceService, @CreatedAt,
                @ResolvedAt, @ResolvedBy
            )
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            await conn.ExecuteAsync(new CommandDefinition(
                commandText: sql,
                parameters: new
                {
                    alert.AlertId,
                    alert.AlertType,
                    alert.Severity,
                    alert.Message,
                    alert.DetailsJson,
                    alert.SourceService,
                    alert.CreatedAt,
                    alert.ResolvedAt,
                    alert.ResolvedBy
                },
                cancellationToken: ct
            ));

            _logger.LogInformation(
                "IVTS alert created: {Type} for {Symbol} - {Message}",
                alert.AlertType, alert.Symbol, alert.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert IVTS alert {AlertId}", alert.AlertId);
            throw;
        }
    }

    public async Task<List<IvtsAlert>> GetActiveAlertsAsync(string symbol, CancellationToken ct = default)
    {
        // Negative-first: validate input
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        }

        // Query alert_history for unresolved IVTS alerts
        // Note: We store Symbol in details_json, so we need to parse it
        // For simplicity, we'll match on alert_type starting with "Ivts" or "Ivr"
        const string sql = """
            SELECT
                alert_id AS AlertId,
                alert_type AS AlertType,
                severity AS Severity,
                message AS Message,
                details_json AS DetailsJson,
                source_service AS SourceService,
                created_at AS CreatedAt,
                resolved_at AS ResolvedAt,
                resolved_by AS ResolvedBy
            FROM alert_history
            WHERE resolved_at IS NULL
              AND (alert_type LIKE 'Ivts%' OR alert_type LIKE 'Ivr%' OR alert_type LIKE 'Inverted%')
              AND (details_json LIKE '%"symbol":"' || @Symbol || '"%' OR message LIKE '%' || @Symbol || '%')
            ORDER BY created_at DESC
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            IEnumerable<dynamic> rows = await conn.QueryAsync(
                new CommandDefinition(
                    commandText: sql,
                    parameters: new { Symbol = symbol },
                    cancellationToken: ct
                ));

            // Map dynamic rows to IvtsAlert records
            List<IvtsAlert> alerts = rows.Select(row => new IvtsAlert
            {
                AlertId = row.AlertId,
                AlertType = row.AlertType,
                Severity = row.Severity,
                Symbol = symbol,  // We know the symbol from the query parameter
                Message = row.Message,
                SnapshotId = string.Empty,  // Not stored in alert_history directly
                DetailsJson = row.DetailsJson,
                SourceService = row.SourceService,
                CreatedAt = row.CreatedAt,
                ResolvedAt = row.ResolvedAt,
                ResolvedBy = row.ResolvedBy
            }).ToList();

            return alerts;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active IVTS alerts for {Symbol}", symbol);
            throw;
        }
    }

    public async Task ResolveAlertAsync(string alertId, string resolvedBy, CancellationToken ct = default)
    {
        // Negative-first: validate input
        if (string.IsNullOrWhiteSpace(alertId))
        {
            throw new ArgumentException("AlertId cannot be empty", nameof(alertId));
        }

        if (string.IsNullOrWhiteSpace(resolvedBy))
        {
            throw new ArgumentException("ResolvedBy cannot be empty", nameof(resolvedBy));
        }

        const string sql = """
            UPDATE alert_history
            SET resolved_at = @ResolvedAt,
                resolved_by = @ResolvedBy
            WHERE alert_id = @AlertId
              AND resolved_at IS NULL
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            int rowsAffected = await conn.ExecuteAsync(new CommandDefinition(
                commandText: sql,
                parameters: new
                {
                    AlertId = alertId,
                    ResolvedAt = DateTime.UtcNow.ToString("O"),
                    ResolvedBy = resolvedBy
                },
                cancellationToken: ct
            ));

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Alert {AlertId} not found or already resolved", alertId);
                return;
            }

            _logger.LogInformation("Alert {AlertId} resolved by {ResolvedBy}", alertId, resolvedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve alert {AlertId}", alertId);
            throw;
        }
    }
}
