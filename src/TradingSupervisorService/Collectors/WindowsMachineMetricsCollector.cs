using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net.NetworkInformation;

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

    // Network throughput — track the previous sample so we can compute a delta.
    // Null on the first call (no baseline yet).
    private long? _prevNetworkBytes;
    private DateTime _prevNetworkSampleUtc;
    private readonly object _networkLock = new();

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

            // Disk: Get free and total space on C: drive (where logs/data typically live)
            (double diskFreeGb, double? diskTotalGb) = GetDiskGb("C:\\");

            // Network: cumulative bytes across all "up" interfaces; returns null on first call.
            double? networkKbps = SampleNetworkKbps();

            // Uptime: Process uptime (service uptime, not machine uptime)
            long uptimeSeconds = (long)(DateTime.UtcNow - _processStartTime).TotalSeconds;

            MachineMetrics metrics = new()
            {
                Hostname = _hostname,
                UptimeSeconds = uptimeSeconds,
                CpuPercent = Math.Round(cpuPercent, 2),
                RamPercent = Math.Round(ramPercent, 2),
                DiskFreeGb = Math.Round(diskFreeGb, 2),
                DiskTotalGb = diskTotalGb.HasValue ? Math.Round(diskTotalGb.Value, 2) : null,
                NetworkKbps = networkKbps.HasValue ? Math.Round(networkKbps.Value, 2) : null,
                TimestampUtc = DateTime.UtcNow
            };

            _logger.LogTrace(
                "Collected metrics: CPU={Cpu}%, RAM={Ram}%, DiskFree={DiskFree}GB, DiskTotal={DiskTotal}GB, Net={NetKbps}kbps, Uptime={Uptime}s",
                metrics.CpuPercent, metrics.RamPercent, metrics.DiskFreeGb, metrics.DiskTotalGb, metrics.NetworkKbps, metrics.UptimeSeconds);

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
    /// Retained for backward compatibility — delegates to <see cref="GetDiskGb(string)"/>.
    /// </summary>
    private double GetDiskFreeGb(string drivePath) => GetDiskGb(drivePath).free;

    /// <summary>
    /// Gets both free AND total disk capacity in GB for the specified drive.
    /// Returns (0, null) on failure so callers always get a valid free-space number
    /// while allowing total-space to be omitted from the heartbeat.
    /// </summary>
    private (double free, double? total) GetDiskGb(string drivePath)
    {
        try
        {
            DriveInfo drive = new(drivePath);
            if (!drive.IsReady)
            {
                _logger.LogWarning("Drive {Drive} is not ready", drivePath);
                return (0.0, null);
            }

            const double BytesToGb = 1024.0 * 1024.0 * 1024.0;
            double freeGb = drive.AvailableFreeSpace / BytesToGb;
            double totalGb = drive.TotalSize / BytesToGb;
            return (freeGb, totalGb);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get disk info for {Drive}", drivePath);
            return (0.0, null);
        }
    }

    /// <summary>
    /// Samples aggregate network throughput across all "up" interfaces, returning
    /// kilobits/second averaged over the interval since the previous call.
    /// <para>
    /// Returns null on the first call (no baseline) and whenever the counters are
    /// unavailable. The delta approach ignores interface add/remove during sampling —
    /// if the total bytes counter ever goes backwards we reset the baseline and return null.
    /// </para>
    /// </summary>
    private double? SampleNetworkKbps()
    {
        try
        {
            long currentBytes = GetTotalNetworkBytes();
            DateTime nowUtc = DateTime.UtcNow;

            lock (_networkLock)
            {
                if (!_prevNetworkBytes.HasValue)
                {
                    // First sample → set baseline, no reading to report.
                    _prevNetworkBytes = currentBytes;
                    _prevNetworkSampleUtc = nowUtc;
                    return null;
                }

                long deltaBytes = currentBytes - _prevNetworkBytes.Value;
                double deltaSeconds = (nowUtc - _prevNetworkSampleUtc).TotalSeconds;

                // Update baseline for the next call regardless of whether we can emit a reading.
                _prevNetworkBytes = currentBytes;
                _prevNetworkSampleUtc = nowUtc;

                if (deltaBytes < 0 || deltaSeconds <= 0)
                {
                    // Counter reset (interface restart, overflow, ...); skip this sample.
                    return null;
                }

                // bytes/s × 8 / 1000 = kbps
                return (deltaBytes * 8.0) / (deltaSeconds * 1000.0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Network throughput sample failed");
            return null;
        }
    }

    /// <summary>
    /// Returns the sum of BytesSent + BytesReceived across all UP network interfaces.
    /// Skips loopback and tunnel adapters which would double-count LAN traffic.
    /// </summary>
    private static long GetTotalNetworkBytes()
    {
        long total = 0;
        NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
        foreach (NetworkInterface ni in interfaces)
        {
            if (ni.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            IPv4InterfaceStatistics stats = ni.GetIPv4Statistics();
            total += stats.BytesSent + stats.BytesReceived;
        }
        return total;
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
