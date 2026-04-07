using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests.Workers;

/// <summary>
/// Unit tests for IvtsMonitorWorker.
/// Uses mocks for dependencies since the worker requires IBKR connection.
/// </summary>
public sealed class IvtsMonitorWorkerTests
{
    [Fact]
    public void Constructor_ValidConfiguration_Succeeds()
    {
        // Arrange
        Mock<ILogger<IvtsMonitorWorker>> mockLogger = new();
        Mock<IIbkrClient> mockIbkrClient = new();
        Mock<IIvtsRepository> mockIvtsRepo = new();
        Mock<IAlertRepository> mockAlertRepo = new();

        Dictionary<string, string?> configDict = new()
        {
            ["IvtsMonitor:Enabled"] = "true",
            ["IvtsMonitor:IntervalSeconds"] = "900",
            ["IvtsMonitor:Symbol"] = "SPX",
            ["IvtsMonitor:IvrThresholdPercent"] = "80.0",
            ["IvtsMonitor:InvertedThresholdPercent"] = "5.0",
            ["IvtsMonitor:SpikeThresholdPercent"] = "20.0"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        IvtsMonitorWorker worker = new(
            mockLogger.Object,
            mockIbkrClient.Object,
            mockIvtsRepo.Object,
            mockAlertRepo.Object,
            config);

        // Assert - constructor should not throw
        Assert.NotNull(worker);
    }

    [Fact]
    public void Constructor_InvalidIntervalSeconds_ThrowsArgumentException()
    {
        // Arrange
        Mock<ILogger<IvtsMonitorWorker>> mockLogger = new();
        Mock<IIbkrClient> mockIbkrClient = new();
        Mock<IIvtsRepository> mockIvtsRepo = new();
        Mock<IAlertRepository> mockAlertRepo = new();

        Dictionary<string, string?> configDict = new()
        {
            ["IvtsMonitor:Enabled"] = "true",
            ["IvtsMonitor:IntervalSeconds"] = "0",  // Invalid
            ["IvtsMonitor:Symbol"] = "SPX"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IvtsMonitorWorker(
            mockLogger.Object,
            mockIbkrClient.Object,
            mockIvtsRepo.Object,
            mockAlertRepo.Object,
            config));
    }

    [Fact]
    public void Constructor_InvalidIvrThreshold_ThrowsArgumentException()
    {
        // Arrange
        Mock<ILogger<IvtsMonitorWorker>> mockLogger = new();
        Mock<IIbkrClient> mockIbkrClient = new();
        Mock<IIvtsRepository> mockIvtsRepo = new();
        Mock<IAlertRepository> mockAlertRepo = new();

        Dictionary<string, string?> configDict = new()
        {
            ["IvtsMonitor:Enabled"] = "true",
            ["IvtsMonitor:IntervalSeconds"] = "900",
            ["IvtsMonitor:Symbol"] = "SPX",
            ["IvtsMonitor:IvrThresholdPercent"] = "150.0"  // Invalid (> 100)
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new IvtsMonitorWorker(
            mockLogger.Object,
            mockIbkrClient.Object,
            mockIvtsRepo.Object,
            mockAlertRepo.Object,
            config));
    }

    [Fact]
    public void Constructor_DisabledInConfig_WorkerDoesNotStart()
    {
        // Arrange
        Mock<ILogger<IvtsMonitorWorker>> mockLogger = new();
        Mock<IIbkrClient> mockIbkrClient = new();
        Mock<IIvtsRepository> mockIvtsRepo = new();
        Mock<IAlertRepository> mockAlertRepo = new();

        Dictionary<string, string?> configDict = new()
        {
            ["IvtsMonitor:Enabled"] = "false",  // Disabled
            ["IvtsMonitor:IntervalSeconds"] = "900",
            ["IvtsMonitor:Symbol"] = "SPX"
        };

        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        // Act
        IvtsMonitorWorker worker = new(
            mockLogger.Object,
            mockIbkrClient.Object,
            mockIvtsRepo.Object,
            mockAlertRepo.Object,
            config);

        // Assert - worker should be created but won't start monitoring
        Assert.NotNull(worker);

        // Note: Testing actual execution would require running ExecuteAsync,
        // which is an integration test. For unit tests, we verify configuration validation only.
    }

    [Fact]
    public void Constructor_MissingConfiguration_UsesDefaults()
    {
        // Arrange
        Mock<ILogger<IvtsMonitorWorker>> mockLogger = new();
        Mock<IIbkrClient> mockIbkrClient = new();
        Mock<IIvtsRepository> mockIvtsRepo = new();
        Mock<IAlertRepository> mockAlertRepo = new();

        // Empty configuration - should use defaults
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        IvtsMonitorWorker worker = new(
            mockLogger.Object,
            mockIbkrClient.Object,
            mockIvtsRepo.Object,
            mockAlertRepo.Object,
            config);

        // Assert - worker should be created with defaults
        // Defaults: Enabled=false, IntervalSeconds=900, Symbol=SPX, IvrThreshold=80%, etc.
        Assert.NotNull(worker);
    }
}
