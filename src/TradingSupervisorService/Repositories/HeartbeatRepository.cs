using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;

namespace TradingSupervisorService.Repositories;

/// <summary>
/// Data model for service heartbeat records.
/// Immutable record for type safety and value equality.
/// </summary>
public sealed record ServiceHeartbeat
{
    public string ServiceName { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string LastSeenAt { get; init; } = string.Empty;  // ISO8601
    public long UptimeSeconds { get; init; }
    public double CpuPercent { get; init; }
    public double RamPercent { get; init; }
    public double DiskFreeGb { get; init; }
    public string TradingMode { get; init; } = "paper";  // Default to safe mode
    public string Version { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;  // ISO8601
    public string UpdatedAt { get; init; } = string.Empty;  // ISO8601
}

/// <summary>
/// Repository interface for service_heartbeats table.
/// Provides type-safe access to heartbeat monitoring data.
/// </summary>
public interface IHeartbeatRepository
{
    /// <summary>
    /// Upserts a heartbeat record (INSERT or UPDATE if service_name exists).
    /// Used by HeartbeatWorker to update service health status.
    /// </summary>
    Task UpsertAsync(ServiceHeartbeat heartbeat, CancellationToken ct);

    /// <summary>
    /// Gets all heartbeat records.
    /// Used by dashboard and monitoring to show service health.
    /// </summary>
    Task<IReadOnlyList<ServiceHeartbeat>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Gets heartbeats that haven't been updated within the specified threshold.
    /// Used for alerting on stale/missing heartbeats.
    /// </summary>
    /// <param name="thresholdSeconds">Max age in seconds (e.g., 300 = 5 minutes)</param>
    Task<IReadOnlyList<ServiceHeartbeat>> GetStaleAsync(int thresholdSeconds, CancellationToken ct);
}

/// <summary>
/// SQLite implementation of IHeartbeatRepository using Dapper.
/// All queries use explicit SQL (no ORM).
/// All IO operations have try/catch with logging.
/// </summary>
public sealed class HeartbeatRepository : IHeartbeatRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<HeartbeatRepository> _logger;

    public HeartbeatRepository(IDbConnectionFactory db, ILogger<HeartbeatRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Upserts a heartbeat record using INSERT OR REPLACE pattern.
    /// This is safe to call repeatedly (idempotent).
    /// </summary>
    public async Task UpsertAsync(ServiceHeartbeat heartbeat, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (heartbeat == null)
        {
            throw new ArgumentNullException(nameof(heartbeat));
        }
        if (string.IsNullOrWhiteSpace(heartbeat.ServiceName))
        {
            throw new ArgumentException("ServiceName cannot be null or empty", nameof(heartbeat));
        }

        const string sql = """
            INSERT INTO service_heartbeats
                (service_name, hostname, last_seen_at, uptime_seconds,
                 cpu_percent, ram_percent, disk_free_gb, trading_mode, version,
                 created_at, updated_at)
            VALUES
                (@ServiceName, @Hostname, @LastSeenAt, @UptimeSeconds,
                 @CpuPercent, @RamPercent, @DiskFreeGb, @TradingMode, @Version,
                 @CreatedAt, datetime('now'))
            ON CONFLICT(service_name) DO UPDATE SET
                hostname = excluded.hostname,
                last_seen_at = excluded.last_seen_at,
                uptime_seconds = excluded.uptime_seconds,
                cpu_percent = excluded.cpu_percent,
                ram_percent = excluded.ram_percent,
                disk_free_gb = excluded.disk_free_gb,
                trading_mode = excluded.trading_mode,
                version = excluded.version,
                updated_at = datetime('now')
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            // Use CommandDefinition for CancellationToken support (Dapper 2.1.x pattern)
            CommandDefinition cmd = new(sql, new
            {
                heartbeat.ServiceName,
                heartbeat.Hostname,
                heartbeat.LastSeenAt,
                heartbeat.UptimeSeconds,
                heartbeat.CpuPercent,
                heartbeat.RamPercent,
                heartbeat.DiskFreeGb,
                heartbeat.TradingMode,
                heartbeat.Version,
                CreatedAt = heartbeat.CreatedAt
            }, cancellationToken: ct);

            await conn.ExecuteAsync(cmd);

            _logger.LogDebug("Upserted heartbeat for service {ServiceName}", heartbeat.ServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert heartbeat for service {ServiceName}",
                heartbeat.ServiceName);
            throw;
        }
    }

    /// <summary>
    /// Gets all heartbeat records ordered by service_name.
    /// No LIMIT needed (small table, ~3-5 services).
    /// </summary>
    public async Task<IReadOnlyList<ServiceHeartbeat>> GetAllAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT
                service_name AS ServiceName,
                hostname AS Hostname,
                last_seen_at AS LastSeenAt,
                uptime_seconds AS UptimeSeconds,
                cpu_percent AS CpuPercent,
                ram_percent AS RamPercent,
                disk_free_gb AS DiskFreeGb,
                trading_mode AS TradingMode,
                version AS Version,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM service_heartbeats
            ORDER BY service_name
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, cancellationToken: ct);
            IEnumerable<ServiceHeartbeat> results = await conn.QueryAsync<ServiceHeartbeat>(cmd);

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get all heartbeats");
            throw;
        }
    }

    /// <summary>
    /// Gets heartbeats older than the specified threshold.
    /// Uses datetime() comparison for robust time-based queries.
    /// </summary>
    public async Task<IReadOnlyList<ServiceHeartbeat>> GetStaleAsync(int thresholdSeconds, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (thresholdSeconds <= 0)
        {
            throw new ArgumentException("Threshold must be positive", nameof(thresholdSeconds));
        }

        const string sql = """
            SELECT
                service_name AS ServiceName,
                hostname AS Hostname,
                last_seen_at AS LastSeenAt,
                uptime_seconds AS UptimeSeconds,
                cpu_percent AS CpuPercent,
                ram_percent AS RamPercent,
                disk_free_gb AS DiskFreeGb,
                trading_mode AS TradingMode,
                version AS Version,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM service_heartbeats
            WHERE datetime(last_seen_at) < datetime('now', '-' || @ThresholdSeconds || ' seconds')
            ORDER BY last_seen_at ASC
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new { ThresholdSeconds = thresholdSeconds },
                cancellationToken: ct);
            IEnumerable<ServiceHeartbeat> results = await conn.QueryAsync<ServiceHeartbeat>(cmd);

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get stale heartbeats (threshold={Threshold}s)",
                thresholdSeconds);
            throw;
        }
    }
}
