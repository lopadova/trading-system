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
