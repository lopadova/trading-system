using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Domain;
using TradingSupervisorService.Repositories;
using TradingSupervisorService.Workers;
using Xunit;

namespace TradingSupervisorService.Tests.Workers;

/// <summary>
/// Unit tests for GreeksMonitorWorker.
/// Tests configuration validation, threshold breach detection, and alert creation.
/// </summary>
public sealed class GreeksMonitorWorkerTests
{
    [Fact]
    [Trait("TestId", "TEST-19-01")]
    public void Constructor_WithValidConfiguration_Succeeds()
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Act
        GreeksMonitorWorker worker = new(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config);

        // Assert
        Assert.NotNull(worker);
    }

    [Theory]
    [Trait("TestId", "TEST-19-02")]
    [InlineData(-1)]    // Negative interval
    [InlineData(0)]     // Zero interval
    public void Constructor_WithInvalidIntervalSeconds_ThrowsArgumentException(int intervalSeconds)
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true, intervalSeconds: intervalSeconds);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new GreeksMonitorWorker(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config));

        Assert.Contains("IntervalSeconds", ex.Message);
    }

    [Theory]
    [Trait("TestId", "TEST-19-03")]
    [InlineData(-0.1)]   // Negative delta
    [InlineData(1.5)]    // Delta > 1.0
    public void Constructor_WithInvalidDeltaThreshold_ThrowsArgumentException(double deltaThreshold)
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true, deltaThreshold: deltaThreshold);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Act & Assert
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new GreeksMonitorWorker(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config));

        Assert.Contains("DeltaThreshold", ex.Message);
    }

    [Fact]
    [Trait("TestId", "TEST-19-04")]
    public async Task ExecuteAsync_WhenDisabled_ExitsImmediately()
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: false);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        GreeksMonitorWorker worker = new(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config);

        CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromSeconds(1));

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(100);  // Give it time to check enabled flag
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // Worker should exit without calling repositories
        positionsRepoMock.Verify(
            r => r.GetActivePositionsWithGreeksAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("TestId", "TEST-19-05")]
    public async Task RunCycle_WithNoPositions_DoesNotCreateAlerts()
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true, intervalSeconds: 1);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Repository returns empty list (no positions)
        positionsRepoMock
            .Setup(r => r.GetActivePositionsWithGreeksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivePositionRecord>());

        GreeksMonitorWorker worker = new(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config);

        CancellationTokenSource cts = new();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);  // Let one cycle run
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // No alerts should be created
        alertRepoMock.Verify(
            a => a.InsertAsync(It.IsAny<AlertRecord>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("TestId", "TEST-19-06")]
    public async Task RunCycle_WithHighDeltaPosition_CreatesDeltaAlert()
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true, intervalSeconds: 1, deltaThreshold: 0.70);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Position with delta = 0.85 (exceeds threshold of 0.70)
        ActivePositionRecord position = new()
        {
            PositionId = "pos-001",
            CampaignId = "camp-001",
            Symbol = "SPY",
            ContractSymbol = "SPY 250101C450",
            StrategyName = "TestStrategy",
            Quantity = 10,
            Delta = 0.85,  // HIGH DELTA
            Gamma = 0.01,
            Theta = -10.0,
            Vega = 50.0,
            UnderlyingPrice = 450.0,
            GreeksUpdatedAt = DateTime.UtcNow.ToString("O")
        };

        positionsRepoMock
            .Setup(r => r.GetActivePositionsWithGreeksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivePositionRecord> { position });

        GreeksMonitorWorker worker = new(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config);

        CancellationTokenSource cts = new();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(5000);  // Give worker time to complete cycle
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // Delta alert should be created (at least once - worker may run multiple cycles)
        alertRepoMock.Verify(
            a => a.InsertAsync(It.Is<AlertRecord>(alert =>
                alert.AlertType == "GreeksDelta" &&
                alert.Severity == "warning" &&
                alert.Message.Contains("0.85")), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    [Trait("TestId", "TEST-19-07")]
    public async Task RunCycle_WithHighGammaPosition_CreatesGammaAlert()
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true, intervalSeconds: 1, gammaThreshold: 0.05);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Position with gamma = 0.08 (exceeds threshold of 0.05)
        ActivePositionRecord position = new()
        {
            PositionId = "pos-002",
            CampaignId = "camp-001",
            Symbol = "SPY",
            ContractSymbol = "SPY 250101C450",
            StrategyName = "TestStrategy",
            Quantity = 10,
            Delta = 0.50,
            Gamma = 0.08,  // HIGH GAMMA
            Theta = -10.0,
            Vega = 50.0,
            UnderlyingPrice = 450.0,
            GreeksUpdatedAt = DateTime.UtcNow.ToString("O")
        };

        positionsRepoMock
            .Setup(r => r.GetActivePositionsWithGreeksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivePositionRecord> { position });

        GreeksMonitorWorker worker = new(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config);

        CancellationTokenSource cts = new();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);  // Let one cycle run
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // Gamma alert should be created
        alertRepoMock.Verify(
            a => a.InsertAsync(It.Is<AlertRecord>(alert =>
                alert.AlertType == "GreeksGamma" &&
                alert.Severity == "warning"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-19-08")]
    public async Task RunCycle_WithHighThetaPosition_CreatesThetaAlert()
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true, intervalSeconds: 1, thetaThreshold: 50.0);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Position with theta = -75.0 (absolute value exceeds threshold of 50.0)
        ActivePositionRecord position = new()
        {
            PositionId = "pos-003",
            CampaignId = "camp-001",
            Symbol = "SPY",
            ContractSymbol = "SPY 250101C450",
            StrategyName = "TestStrategy",
            Quantity = 10,
            Delta = 0.50,
            Gamma = 0.01,
            Theta = -75.0,  // HIGH THETA DECAY
            Vega = 50.0,
            UnderlyingPrice = 450.0,
            GreeksUpdatedAt = DateTime.UtcNow.ToString("O")
        };

        positionsRepoMock
            .Setup(r => r.GetActivePositionsWithGreeksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivePositionRecord> { position });

        GreeksMonitorWorker worker = new(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config);

        CancellationTokenSource cts = new();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);  // Let one cycle run
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // Theta alert should be created
        alertRepoMock.Verify(
            a => a.InsertAsync(It.Is<AlertRecord>(alert =>
                alert.AlertType == "GreeksTheta" &&
                alert.Severity == "warning"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-19-09")]
    public async Task RunCycle_WithHighVegaPosition_CreatesVegaAlert()
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true, intervalSeconds: 1, vegaThreshold: 100.0);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Position with vega = 150.0 (exceeds threshold of 100.0)
        ActivePositionRecord position = new()
        {
            PositionId = "pos-004",
            CampaignId = "camp-001",
            Symbol = "SPY",
            ContractSymbol = "SPY 250101C450",
            StrategyName = "TestStrategy",
            Quantity = 10,
            Delta = 0.50,
            Gamma = 0.01,
            Theta = -10.0,
            Vega = 150.0,  // HIGH VEGA
            ImpliedVolatility = 0.25,
            UnderlyingPrice = 450.0,
            GreeksUpdatedAt = DateTime.UtcNow.ToString("O")
        };

        positionsRepoMock
            .Setup(r => r.GetActivePositionsWithGreeksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivePositionRecord> { position });

        GreeksMonitorWorker worker = new(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config);

        CancellationTokenSource cts = new();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);  // Let one cycle run
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // Vega alert should be created
        alertRepoMock.Verify(
            a => a.InsertAsync(It.Is<AlertRecord>(alert =>
                alert.AlertType == "GreeksVega" &&
                alert.Severity == "warning"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-19-10")]
    public async Task RunCycle_WithMultipleThresholdBreaches_CreatesMultipleAlerts()
    {
        // Arrange
        IConfiguration config = BuildConfiguration(enabled: true, intervalSeconds: 1);
        Mock<IPositionsRepository> positionsRepoMock = new();
        Mock<IAlertRepository> alertRepoMock = new();
        Mock<ILogger<GreeksMonitorWorker>> loggerMock = new();

        // Position with ALL Greeks exceeding thresholds
        ActivePositionRecord position = new()
        {
            PositionId = "pos-005",
            CampaignId = "camp-001",
            Symbol = "SPY",
            ContractSymbol = "SPY 250101C450",
            StrategyName = "TestStrategy",
            Quantity = 10,
            Delta = 0.85,    // Exceeds 0.70
            Gamma = 0.08,    // Exceeds 0.05
            Theta = -75.0,   // Exceeds 50.0 (absolute)
            Vega = 150.0,    // Exceeds 100.0
            ImpliedVolatility = 0.25,
            UnderlyingPrice = 450.0,
            GreeksUpdatedAt = DateTime.UtcNow.ToString("O")
        };

        positionsRepoMock
            .Setup(r => r.GetActivePositionsWithGreeksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActivePositionRecord> { position });

        GreeksMonitorWorker worker = new(
            loggerMock.Object,
            positionsRepoMock.Object,
            alertRepoMock.Object,
            config);

        CancellationTokenSource cts = new();

        // Act
        await worker.StartAsync(cts.Token);
        await Task.Delay(500);  // Let one cycle run
        await worker.StopAsync(CancellationToken.None);

        // Assert
        // Should create 4 alerts (one for each Greek)
        alertRepoMock.Verify(
            a => a.InsertAsync(It.IsAny<AlertRecord>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));
    }

    // Helper method to build IConfiguration for testing
    private static IConfiguration BuildConfiguration(
        bool enabled = true,
        int intervalSeconds = 60,
        double deltaThreshold = 0.70,
        double gammaThreshold = 0.05,
        double thetaThreshold = 50.0,
        double vegaThreshold = 100.0,
        int startupDelaySeconds = 0)  // Default 0 for tests (immediate execution)
    {
        Dictionary<string, string?> inMemorySettings = new()
        {
            { "GreeksMonitor:Enabled", enabled.ToString() },
            { "GreeksMonitor:IntervalSeconds", intervalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            { "GreeksMonitor:StartupDelaySeconds", startupDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            { "GreeksMonitor:DeltaThreshold", deltaThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            { "GreeksMonitor:GammaThreshold", gammaThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            { "GreeksMonitor:ThetaThreshold", thetaThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture) },
            { "GreeksMonitor:VegaThreshold", vegaThreshold.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }
}
