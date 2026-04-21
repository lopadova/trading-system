using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Ibkr;

namespace SharedKernel.Observability;

/// <summary>
/// Default <see cref="IHealthState"/> implementation shared by both services.
/// <para>
/// Tracks:
/// </para>
/// <list type="bullet">
///   <item><description>Uptime — <see cref="Stopwatch"/> started at construction.</description></item>
///   <item><description>IBKR — checks <see cref="IIbkrClient.IsConnected"/>; optional (null = "not-configured").</description></item>
///   <item><description>DB — issues a cheap "SELECT 1" against the supplied <see cref="IDbConnectionFactory"/>; optional.</description></item>
/// </list>
/// <para>
/// Status aggregation rules:
/// </para>
/// <list type="bullet">
///   <item><description>All checks "ok"/"connected"/"not-configured" → <c>ok</c>.</description></item>
///   <item><description>Any check "down" / "error" → <c>down</c>.</description></item>
///   <item><description>Otherwise → <c>degraded</c> (e.g. IBKR disconnected but DB reachable).</description></item>
/// </list>
/// The method <see cref="Current"/> never throws — it wraps all check failures and
/// returns a populated <see cref="HealthReport"/>.
/// </summary>
public sealed class HealthState : IHealthState
{
    private readonly string _service;
    private readonly string _version;
    private readonly Stopwatch _uptime;
    private readonly IIbkrClient? _ibkr;
    private readonly IDbConnectionFactory? _db;
    private readonly ILogger<HealthState> _logger;

    /// <summary>
    /// Creates a health-state tracker. Pass null for subsystems the service doesn't use
    /// (e.g. SharedKernel tests that don't have a real DB connection).
    /// </summary>
    /// <param name="serviceName">Logical short name (e.g. "supervisor", "options-execution").</param>
    /// <param name="ibkr">Optional IBKR client for connection-status check.</param>
    /// <param name="db">Optional DB connection factory for "SELECT 1" sanity check.</param>
    /// <param name="logger">Logger for check failures (debug-level, not error-level — the endpoint itself is the alarm).</param>
    public HealthState(
        string serviceName,
        IIbkrClient? ibkr,
        IDbConnectionFactory? db,
        ILogger<HealthState> logger)
    {
        if (string.IsNullOrWhiteSpace(serviceName))
        {
            throw new ArgumentException("serviceName required", nameof(serviceName));
        }

        _service = serviceName;
        _version = ResolveAssemblyVersion();
        _uptime = Stopwatch.StartNew();
        _ibkr = ibkr;
        _db = db;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Returns the current health snapshot. Does NOT throw; any subsystem error
    /// is folded into a "down" / "error" check entry.
    /// </summary>
    public HealthReport Current()
    {
        Dictionary<string, string> checks = new(StringComparer.OrdinalIgnoreCase);

        // IBKR check: optional. "not-configured" means this service doesn't wire IBKR at all.
        string ibkrStatus = "not-configured";
        if (_ibkr != null)
        {
            try
            {
                ibkrStatus = _ibkr.IsConnected ? "connected" : "disconnected";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "IBKR health check threw");
                ibkrStatus = "error";
            }
        }
        checks["ibkr"] = ibkrStatus;

        // DB check: optional. Runs a synchronous "SELECT 1" with 1-second budget.
        string dbStatus = "not-configured";
        if (_db != null)
        {
            dbStatus = ProbeDatabase();
        }
        checks["db"] = dbStatus;

        string overall = AggregateStatus(checks.Values);

        return new HealthReport(
            Service: _service,
            Status: overall,
            Version: _version,
            Uptime: _uptime.Elapsed,
            Now: DateTimeOffset.UtcNow,
            Checks: checks);
    }

    /// <summary>
    /// Cheap DB probe: opens a connection, runs "SELECT 1", disposes. Entire operation
    /// is wrapped in try/catch so the health endpoint never crashes on a transient DB failure.
    /// </summary>
    private string ProbeDatabase()
    {
        try
        {
            // .Result here is acceptable: the caller owns a brief synchronous path through
            // the health endpoint, and OpenAsync is fast against SQLite (WAL).
            // If we see contention we can move to an async health endpoint later.
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(1));
            SqliteConnection conn = _db!.OpenAsync(cts.Token).GetAwaiter().GetResult();
            try
            {
                using SqliteCommand cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT 1";
                cmd.CommandTimeout = 1;
                object? result = cmd.ExecuteScalar();
                return result != null ? "ok" : "empty";
            }
            finally
            {
                conn.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "DB health probe failed");
            return "error";
        }
    }

    /// <summary>
    /// Overall status aggregation: single "error" or "down" → down; single non-ok/connected/not-configured → degraded; else ok.
    /// </summary>
    private static string AggregateStatus(IEnumerable<string> statuses)
    {
        bool anyError = false;
        bool anyDegraded = false;
        foreach (string s in statuses)
        {
            if (s == "error" || s == "down")
            {
                anyError = true;
                continue;
            }
            if (s != "ok" && s != "connected" && s != "not-configured")
            {
                anyDegraded = true;
            }
        }

        if (anyError)
        {
            return "down";
        }
        if (anyDegraded)
        {
            return "degraded";
        }
        return "ok";
    }

    /// <summary>
    /// Reads the AssemblyInformationalVersion (falls back to AssemblyVersion, then "unknown").
    /// </summary>
    private static string ResolveAssemblyVersion()
    {
        try
        {
            Assembly? asm = Assembly.GetEntryAssembly();
            if (asm == null)
            {
                return "unknown";
            }

            AssemblyInformationalVersionAttribute? info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (info != null && !string.IsNullOrWhiteSpace(info.InformationalVersion))
            {
                return info.InformationalVersion;
            }

            Version? v = asm.GetName().Version;
            return v != null ? v.ToString() : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
