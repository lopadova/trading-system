using SharedKernel.Domain;

namespace TradingSupervisorService.Repositories;

/// <summary>
/// Repository for IVTS (Implied Volatility Term Structure) monitoring data.
/// Stores snapshots, calculates IVR, and manages IVTS alerts.
/// </summary>
public interface IIvtsRepository
{
    /// <summary>
    /// Insert a new IVTS snapshot into the database.
    /// </summary>
    /// <param name="snapshot">IVTS snapshot to store</param>
    /// <param name="ct">Cancellation token</param>
    Task InsertSnapshotAsync(IvtsSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Get the most recent IVTS snapshot for a given symbol.
    /// </summary>
    /// <param name="symbol">Underlying symbol (e.g., "SPX")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Most recent snapshot or null if no data exists</returns>
    Task<IvtsSnapshot?> GetLatestSnapshotAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Get 52-week min/max IV for IVR calculation.
    /// Uses the average of 30d and 60d IV as the "current IV" for each snapshot.
    /// </summary>
    /// <param name="symbol">Underlying symbol</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Tuple (minIv, maxIv) or null if insufficient data</returns>
    Task<(double MinIv, double MaxIv)?> Get52WeekIvRangeAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Insert an IVTS alert into alert_history table.
    /// </summary>
    /// <param name="alert">Alert to store</param>
    /// <param name="ct">Cancellation token</param>
    Task InsertAlertAsync(IvtsAlert alert, CancellationToken ct = default);

    /// <summary>
    /// Get all active (unresolved) IVTS alerts for a symbol.
    /// </summary>
    /// <param name="symbol">Underlying symbol</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of active alerts</returns>
    Task<List<IvtsAlert>> GetActiveAlertsAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Resolve an IVTS alert by ID.
    /// Sets resolved_at to current UTC time and resolved_by to the provided reason.
    /// </summary>
    /// <param name="alertId">Alert ID to resolve</param>
    /// <param name="resolvedBy">Reason for resolution (e.g., "auto", "manual")</param>
    /// <param name="ct">Cancellation token</param>
    Task ResolveAlertAsync(string alertId, string resolvedBy, CancellationToken ct = default);
}
