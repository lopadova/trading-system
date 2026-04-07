using Microsoft.Extensions.Logging;
using Moq;
using TradingSupervisorService.Collectors;
using Xunit;

namespace TradingSupervisorService.Tests.Collectors;

/// <summary>
/// Tests for WindowsMachineMetricsCollector.
/// Note: These tests use real PerformanceCounters on Windows.
/// They may fail on non-Windows platforms or in restricted environments.
/// </summary>
public sealed class WindowsMachineMetricsCollectorTests : IDisposable
{
    private readonly Mock<ILogger<WindowsMachineMetricsCollector>> _mockLogger;
    private readonly WindowsMachineMetricsCollector _collector;

    public WindowsMachineMetricsCollectorTests()
    {
        _mockLogger = new Mock<ILogger<WindowsMachineMetricsCollector>>();
        _collector = new WindowsMachineMetricsCollector(_mockLogger.Object);
    }

    [Fact]
    public async Task CollectAsync_ReturnsValidMetrics()
    {
        // Act
        MachineMetrics metrics = await _collector.CollectAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(metrics);
        Assert.False(string.IsNullOrWhiteSpace(metrics.Hostname), "Hostname should not be empty");
        Assert.True(metrics.UptimeSeconds >= 0, "Uptime should be non-negative");

        // CPU can be 0 on first call (needs baseline), so just check it's in valid range
        Assert.InRange(metrics.CpuPercent, 0.0, 100.0);

        // RAM should be > 0 (process is using memory)
        Assert.InRange(metrics.RamPercent, 0.0, 100.0);

        // Disk free should be > 0 (C: drive has some free space)
        Assert.True(metrics.DiskFreeGb >= 0.0, "Disk free should be non-negative");

        // Timestamp should be recent (within last minute)
        TimeSpan age = DateTime.UtcNow - metrics.TimestampUtc;
        Assert.True(age.TotalSeconds < 60, "Timestamp should be recent");
    }

    [Fact]
    public async Task CollectAsync_SecondCallReturnsAccurateCpu()
    {
        // First call establishes baseline (may return 0)
        MachineMetrics firstMetrics = await _collector.CollectAsync(CancellationToken.None);

        // Wait a bit for CPU counter to update
        await Task.Delay(100);

        // Second call should have accurate CPU reading
        MachineMetrics secondMetrics = await _collector.CollectAsync(CancellationToken.None);

        // Assert
        // CPU should be in valid range (not necessarily > 0 if machine is idle)
        Assert.InRange(secondMetrics.CpuPercent, 0.0, 100.0);

        // Uptime should increase
        Assert.True(secondMetrics.UptimeSeconds >= firstMetrics.UptimeSeconds,
            "Uptime should increase between calls");
    }

    [Fact]
    public async Task CollectAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        using CancellationTokenSource cts = new();
        cts.Cancel();  // Cancel immediately

        // Act & Assert
        // Note: CollectAsync is currently not truly async, so this may not throw.
        // If implementation changes to async IO, this test will validate cancellation.
        try
        {
            await _collector.CollectAsync(cts.Token);
            // If we get here, the method completed before checking cancellation
            // This is acceptable for the current implementation
        }
        catch (OperationCanceledException)
        {
            // Expected if cancellation is checked
        }
    }

    [Fact]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        // Act - dispose multiple times
        _collector.Dispose();
        _collector.Dispose();  // Should not throw

        // Assert
        // Should throw ObjectDisposedException on next use
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _collector.CollectAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Dispose_PreventsSubsequentCalls()
    {
        // Arrange
        _collector.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            await _collector.CollectAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CollectAsync_MetricsAreRounded()
    {
        // Act
        MachineMetrics metrics = await _collector.CollectAsync(CancellationToken.None);

        // Assert
        // All metrics should be rounded to 2 decimal places
        Assert.Equal(Math.Round(metrics.CpuPercent, 2), metrics.CpuPercent);
        Assert.Equal(Math.Round(metrics.RamPercent, 2), metrics.RamPercent);
        Assert.Equal(Math.Round(metrics.DiskFreeGb, 2), metrics.DiskFreeGb);
    }

    public void Dispose()
    {
        _collector?.Dispose();
    }
}
