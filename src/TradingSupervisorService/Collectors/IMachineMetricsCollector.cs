namespace TradingSupervisorService.Collectors;

/// <summary>
/// Machine-level metrics for monitoring service health.
/// All percentages are 0-100. All sizes in appropriate units (GB, MB).
/// </summary>
public sealed record MachineMetrics
{
    public string Hostname { get; init; } = string.Empty;
    public long UptimeSeconds { get; init; }
    public double CpuPercent { get; init; }  // 0-100
    public double RamPercent { get; init; }  // 0-100
    public double DiskFreeGb { get; init; }

    /// <summary>
    /// Total disk size in GB for the drive where the data directory lives.
    /// Introduced in Phase 7.3 — Worker migration 0008 adds the column.
    /// Null when the drive info cannot be resolved (non-fatal).
    /// </summary>
    public double? DiskTotalGb { get; init; }

    /// <summary>
    /// Averaged network throughput in kilobits/second since the previous sample.
    /// Computed as (bytes-delta × 8) / (seconds-delta × 1000). Null on the first
    /// sample (no baseline) or when counters are unavailable.
    /// Introduced in Phase 7.3 — Worker migration 0008 adds the column.
    /// </summary>
    public double? NetworkKbps { get; init; }

    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Platform-specific implementation for collecting machine metrics.
/// Windows implementation uses PerformanceCounter.
/// Linux implementation would use /proc filesystem.
/// </summary>
public interface IMachineMetricsCollector
{
    /// <summary>
    /// Collects current machine metrics (CPU, RAM, disk, uptime).
    /// This operation may block briefly (100-500ms) while sampling performance counters.
    /// </summary>
    /// <returns>Current machine metrics snapshot</returns>
    Task<MachineMetrics> CollectAsync(CancellationToken ct);
}
