using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TradingSupervisorService.Collectors;

/// <summary>
/// Windows-specific implementation of IMachineMetricsCollector using PerformanceCounter.
/// Counters are initialized once and reused for performance.
/// Thread-safe for concurrent reads.
/// </summary>
public sealed class WindowsMachineMetricsCollector : IMachineMetricsCollector, IDisposable
{
    private readonly ILogger<WindowsMachineMetricsCollector> _logger;
    private readonly string _hostname;
    private readonly DateTime _processStartTime;

    // Performance counters (reused across calls for efficiency)
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _ramCounter;

    // Flag to track disposal
    private bool _disposed;

    public WindowsMachineMetricsCollector(ILogger<WindowsMachineMetricsCollector> logger)
    {
        _logger = logger;
        _hostname = Environment.MachineName;
        _processStartTime = DateTime.UtcNow;

        // Initialize performance counters
        // CPU: Processor Time (all cores average)
        _cpuCounter = new PerformanceCounter(
            categoryName: "Processor",
            counterName: "% Processor Time",
            instanceName: "_Total",
            readOnly: true);

        // RAM: Available memory (we'll calculate percent from total)
        _ramCounter = new PerformanceCounter(
            categoryName: "Memory",
            counterName: "Available MBytes",
            readOnly: true);

        // First read is always 0 for CPU counter (needs baseline)
        // Call NextValue() to initialize, discard result
        _ = _cpuCounter.NextValue();

        _logger.LogDebug("WindowsMachineMetricsCollector initialized for host {Hostname}", _hostname);
    }

    /// <summary>
    /// Collects machine metrics. First CPU sample may be inaccurate (returns 0).
    /// Subsequent calls are accurate after baseline is established.
    /// </summary>
    public async Task<MachineMetrics> CollectAsync(CancellationToken ct)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsMachineMetricsCollector));
        }

        try
        {
            // CPU percentage (0-100)
            // Note: First call returns 0, need a baseline. In production, the worker
            // calls this repeatedly so baseline is quickly established.
            double cpuPercent = _cpuCounter.NextValue();

            // RAM: Get available MB and calculate percentage
            double availableRamMb = _ramCounter.NextValue();
            double totalRamMb = GetTotalPhysicalMemoryMb();
            double ramPercent = totalRamMb > 0
                ? ((totalRamMb - availableRamMb) / totalRamMb) * 100.0
                : 0.0;

            // Disk: Get free space on C: drive (where logs/data typically live)
            double diskFreeGb = GetDiskFreeGb("C:\\");

            // Uptime: Process uptime (service uptime, not machine uptime)
            long uptimeSeconds = (long)(DateTime.UtcNow - _processStartTime).TotalSeconds;

            MachineMetrics metrics = new()
            {
                Hostname = _hostname,
                UptimeSeconds = uptimeSeconds,
                CpuPercent = Math.Round(cpuPercent, 2),
                RamPercent = Math.Round(ramPercent, 2),
                DiskFreeGb = Math.Round(diskFreeGb, 2),
                TimestampUtc = DateTime.UtcNow
            };

            _logger.LogTrace("Collected metrics: CPU={Cpu}%, RAM={Ram}%, Disk={Disk}GB, Uptime={Uptime}s",
                metrics.CpuPercent, metrics.RamPercent, metrics.DiskFreeGb, metrics.UptimeSeconds);

            // No actual async work, but keep async signature for interface compatibility
            // and future flexibility (e.g., network-based metrics collection)
            return await Task.FromResult(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect machine metrics");
            throw;
        }
    }

    /// <summary>
    /// Gets total physical memory in MB.
    /// Uses GC.GetGCMemoryInfo() for accurate total memory reporting.
    /// </summary>
    private static double GetTotalPhysicalMemoryMb()
    {
        try
        {
            // GCMemoryInfo provides accurate total physical memory
            GCMemoryInfo memInfo = GC.GetGCMemoryInfo();
            double totalBytes = memInfo.TotalAvailableMemoryBytes;
            return totalBytes / (1024.0 * 1024.0);  // Convert to MB
        }
        catch
        {
            // Fallback: estimate from Environment.WorkingSet (less accurate)
            return 16384.0;  // Default to 16GB if detection fails
        }
    }

    /// <summary>
    /// Gets free disk space in GB for the specified drive.
    /// </summary>
    private double GetDiskFreeGb(string drivePath)
    {
        try
        {
            DriveInfo drive = new(drivePath);
            if (!drive.IsReady)
            {
                _logger.LogWarning("Drive {Drive} is not ready", drivePath);
                return 0.0;
            }

            double freeBytes = drive.AvailableFreeSpace;
            return freeBytes / (1024.0 * 1024.0 * 1024.0);  // Convert to GB
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get disk space for {Drive}", drivePath);
            return 0.0;
        }
    }

    /// <summary>
    /// Disposes performance counters to release system resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _cpuCounter?.Dispose();
        _ramCounter?.Dispose();

        _disposed = true;
        _logger.LogDebug("WindowsMachineMetricsCollector disposed");
    }
}
