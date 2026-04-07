using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TradingSupervisorService.Collectors;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests.Workers;

/// <summary>
/// Tests for HeartbeatWorker BackgroundService.
/// Verifies that metrics are collected and heartbeats are written on interval.
/// </summary>
public sealed class HeartbeatWorkerTests
{
    private readonly Mock<ILogger<HeartbeatWorker>> _mockLogger;
    private readonly Mock<IMachineMetricsCollector> _mockMetricsCollector;
    private readonly Mock<IHeartbeatRepository> _mockHeartbeatRepo;
    private readonly IConfiguration _testConfig;

    public HeartbeatWorkerTests()
    {
        _mockLogger = new Mock<ILogger<HeartbeatWorker>>();
        _mockMetricsCollector = new Mock<IMachineMetricsCollector>();
        _mockHeartbeatRepo = new Mock<IHeartbeatRepository>();

        // Build test configuration
        Dictionary<string, string?> configValues = new()
        {
            { "Monitoring:IntervalSeconds", "1" },  // Fast interval for testing
            { "TradingMode", "paper" }
        };
        _testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();
    }

    [Fact]
    public async Task ExecuteAsync_CollectsMetricsAndWritesHeartbeat()
    {
        // Arrange
        MachineMetrics testMetrics = new()
        {
            Hostname = "test-host",
            UptimeSeconds = 3600,
            CpuPercent = 45.5,
            RamPercent = 62.3,
            DiskFreeGb = 123.4,
            TimestampUtc = DateTime.UtcNow
        };

        _mockMetricsCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testMetrics);

        ServiceHeartbeat? capturedHeartbeat = null;
        _mockHeartbeatRepo
            .Setup(x => x.UpsertAsync(It.IsAny<ServiceHeartbeat>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceHeartbeat, CancellationToken>((hb, ct) => capturedHeartbeat = hb)
            .Returns(Task.CompletedTask);

        HeartbeatWorker worker = new(
            _mockLogger.Object,
            _mockMetricsCollector.Object,
            _mockHeartbeatRepo.Object,
            _testConfig);

        using CancellationTokenSource cts = new();

        // Act
        // Start worker and let it run for 1.5 seconds (should complete 1 cycle)
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(1500);  // Wait for at least one cycle
        cts.Cancel();
        await workerTask;

        // Assert
        // Verify metrics were collected
        _mockMetricsCollector.Verify(
            x => x.CollectAsync(It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Metrics should be collected at least once");

        // Verify heartbeat was written
        _mockHeartbeatRepo.Verify(
            x => x.UpsertAsync(It.IsAny<ServiceHeartbeat>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Heartbeat should be written at least once");

        // Verify heartbeat content
        Assert.NotNull(capturedHeartbeat);
        Assert.Equal("TradingSupervisorService", capturedHeartbeat.ServiceName);
        Assert.Equal("test-host", capturedHeartbeat.Hostname);
        Assert.Equal(45.5, capturedHeartbeat.CpuPercent);
        Assert.Equal(62.3, capturedHeartbeat.RamPercent);
        Assert.Equal(123.4, capturedHeartbeat.DiskFreeGb);
        Assert.Equal("paper", capturedHeartbeat.TradingMode);
        Assert.Equal("1.0.0", capturedHeartbeat.Version);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesOnError()
    {
        // Arrange
        int callCount = 0;
        _mockMetricsCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                callCount++;
                if (callCount == 1)
                {
                    throw new InvalidOperationException("Test error - first call fails");
                }
            })
            .ReturnsAsync(new MachineMetrics { Hostname = "test-host" });

        _mockHeartbeatRepo
            .Setup(x => x.UpsertAsync(It.IsAny<ServiceHeartbeat>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        HeartbeatWorker worker = new(
            _mockLogger.Object,
            _mockMetricsCollector.Object,
            _mockHeartbeatRepo.Object,
            _testConfig);

        using CancellationTokenSource cts = new();

        // Act
        // Start worker and let it run for 2.5 seconds (should attempt 2+ cycles)
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(2500);
        cts.Cancel();
        await workerTask;

        // Assert
        // Worker should have retried after first failure
        Assert.True(callCount >= 2, $"Expected at least 2 calls (retry after error), got {callCount}");

        // Verify heartbeat was written on successful retry
        _mockHeartbeatRepo.Verify(
            x => x.UpsertAsync(It.IsAny<ServiceHeartbeat>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "Heartbeat should be written after retry succeeds");
    }

    [Fact]
    public void Constructor_ThrowsOnInvalidInterval()
    {
        // Arrange
        Dictionary<string, string?> invalidConfig = new()
        {
            { "Monitoring:IntervalSeconds", "0" },  // Invalid: must be > 0
            { "TradingMode", "paper" }
        };
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(invalidConfig)
            .Build();

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            new HeartbeatWorker(
                _mockLogger.Object,
                _mockMetricsCollector.Object,
                _mockHeartbeatRepo.Object,
                config));

        Assert.Contains("IntervalSeconds", ex.Message);
    }

    [Theory]
    [InlineData("paper", "paper")]
    [InlineData("PAPER", "paper")]
    [InlineData("live", "live")]
    [InlineData("LIVE", "live")]
    [InlineData("invalid", "paper")]  // Unknown values default to paper
    [InlineData("", "paper")]
    public async Task ExecuteAsync_HandlesTradingModeCorrectly(string configValue, string expectedMode)
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            { "Monitoring:IntervalSeconds", "1" },
            { "TradingMode", configValue }
        };
        IConfiguration testConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        _mockMetricsCollector
            .Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MachineMetrics { Hostname = "test" });

        ServiceHeartbeat? capturedHeartbeat = null;
        _mockHeartbeatRepo
            .Setup(x => x.UpsertAsync(It.IsAny<ServiceHeartbeat>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceHeartbeat, CancellationToken>((hb, ct) => capturedHeartbeat = hb)
            .Returns(Task.CompletedTask);

        HeartbeatWorker worker = new(
            _mockLogger.Object,
            _mockMetricsCollector.Object,
            _mockHeartbeatRepo.Object,
            testConfig);

        using CancellationTokenSource cts = new();

        // Act
        Task workerTask = worker.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await workerTask;

        // Assert
        Assert.NotNull(capturedHeartbeat);
        Assert.Equal(expectedMode, capturedHeartbeat.TradingMode);
    }
}
