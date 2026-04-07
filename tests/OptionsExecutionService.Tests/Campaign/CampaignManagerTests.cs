namespace OptionsExecutionService.Tests.Campaign;

using Microsoft.Extensions.Logging;
using Moq;
using OptionsExecutionService.Campaign;
using OptionsExecutionService.Orders;
using SharedKernel.Domain;
using SharedKernel.Strategy;
using Xunit;

/// <summary>
/// Tests for CampaignManager service.
/// Verifies campaign lifecycle: Create → Check Entry → Activate → Check Exit → Close.
/// </summary>
public sealed class CampaignManagerTests
{
    private readonly Mock<IStrategyLoader> _mockStrategyLoader;
    private readonly Mock<ICampaignRepository> _mockRepository;
    private readonly Mock<IOrderPlacer> _mockOrderPlacer;
    private readonly ILogger<CampaignManager> _logger;
    private readonly CampaignManager _manager;

    public CampaignManagerTests()
    {
        _mockStrategyLoader = new Mock<IStrategyLoader>();
        _mockRepository = new Mock<ICampaignRepository>();
        _mockOrderPlacer = new Mock<IOrderPlacer>();
        _logger = new LoggerFactory().CreateLogger<CampaignManager>();

        _manager = new CampaignManager(
            _mockStrategyLoader.Object,
            _mockRepository.Object,
            _mockOrderPlacer.Object,
            _logger);
    }

    [Fact]
    [Trait("TestId", "TEST-17-01")]
    public async Task CreateCampaignAsync_LoadsStrategy_AndCreatesOpenCampaign()
    {
        // Arrange
        string strategyPath = "/strategies/test-strategy.json";
        StrategyDefinition strategy = CreateMockStrategy();

        _mockStrategyLoader
            .Setup(x => x.LoadStrategyAsync(strategyPath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(strategy);

        _mockRepository
            .Setup(x => x.SaveCampaignAsync(It.IsAny<Campaign>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        string campaignId = await _manager.CreateCampaignAsync(strategyPath);

        // Assert
        Assert.NotNull(campaignId);
        Assert.NotEmpty(campaignId);

        _mockStrategyLoader.Verify(
            x => x.LoadStrategyAsync(strategyPath, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRepository.Verify(
            x => x.SaveCampaignAsync(
                It.Is<Campaign>(c =>
                    c.CampaignId == campaignId &&
                    c.State == CampaignState.Open &&
                    c.Strategy == strategy),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-17-02")]
    public async Task CheckAndExecuteEntryAsync_WhenConditionsMet_PlacesOrdersAndActivatesCampaign()
    {
        // Arrange
        string campaignId = "test-campaign-123";
        Campaign openCampaign = CreateOpenCampaign(campaignId);

        _mockRepository
            .Setup(x => x.GetCampaignAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(openCampaign);

        List<string> positionIds = new() { "pos-1", "pos-2" };

        _mockOrderPlacer
            .Setup(x => x.PlaceEntryOrdersAsync(
                campaignId,
                openCampaign.Strategy,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(positionIds);

        _mockRepository
            .Setup(x => x.SaveCampaignAsync(It.IsAny<Campaign>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        bool activated = await _manager.CheckAndExecuteEntryAsync(campaignId);

        // Assert
        Assert.True(activated);

        _mockOrderPlacer.Verify(
            x => x.PlaceEntryOrdersAsync(campaignId, openCampaign.Strategy, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRepository.Verify(
            x => x.SaveCampaignAsync(
                It.Is<Campaign>(c =>
                    c.CampaignId == campaignId &&
                    c.State == CampaignState.Active &&
                    c.ActivatedAt != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-17-03")]
    public async Task CheckAndExecuteEntryAsync_WhenAlreadyActive_ReturnsFalse()
    {
        // Arrange
        string campaignId = "test-campaign-123";
        Campaign activeCampaign = CreateActiveCampaign(campaignId);

        _mockRepository
            .Setup(x => x.GetCampaignAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCampaign);

        // Act
        bool activated = await _manager.CheckAndExecuteEntryAsync(campaignId);

        // Assert
        Assert.False(activated);

        // Order placer should NOT be called
        _mockOrderPlacer.Verify(
            x => x.PlaceEntryOrdersAsync(It.IsAny<string>(), It.IsAny<StrategyDefinition>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("TestId", "TEST-17-04")]
    public async Task CheckAndExecuteExitAsync_WhenProfitTargetHit_ClosesPosition()
    {
        // Arrange
        string campaignId = "test-campaign-123";
        Campaign activeCampaign = CreateActiveCampaign(campaignId);

        _mockRepository
            .Setup(x => x.GetCampaignAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCampaign);

        // Profit target = 50% of initial credit = 500
        decimal unrealizedPnL = 600m; // Above target
        _mockOrderPlacer
            .Setup(x => x.GetUnrealizedPnLAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unrealizedPnL);

        decimal realizedPnL = 580m;
        _mockOrderPlacer
            .Setup(x => x.ClosePositionsAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(realizedPnL);

        _mockRepository
            .Setup(x => x.SaveCampaignAsync(It.IsAny<Campaign>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        bool closed = await _manager.CheckAndExecuteExitAsync(campaignId);

        // Assert
        Assert.True(closed);

        _mockOrderPlacer.Verify(
            x => x.ClosePositionsAsync(campaignId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRepository.Verify(
            x => x.SaveCampaignAsync(
                It.Is<Campaign>(c =>
                    c.CampaignId == campaignId &&
                    c.State == CampaignState.Closed &&
                    c.CloseReason == "profit_target" &&
                    c.RealizedPnL == realizedPnL),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-17-05")]
    public async Task CheckAndExecuteExitAsync_WhenStopLossHit_ClosesPosition()
    {
        // Arrange
        string campaignId = "test-campaign-123";
        Campaign activeCampaign = CreateActiveCampaign(campaignId);

        _mockRepository
            .Setup(x => x.GetCampaignAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCampaign);

        // Stop loss = 200% of initial credit = -2000
        decimal unrealizedPnL = -2100m; // Below stop loss
        _mockOrderPlacer
            .Setup(x => x.GetUnrealizedPnLAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unrealizedPnL);

        decimal realizedPnL = -2050m;
        _mockOrderPlacer
            .Setup(x => x.ClosePositionsAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(realizedPnL);

        _mockRepository
            .Setup(x => x.SaveCampaignAsync(It.IsAny<Campaign>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        bool closed = await _manager.CheckAndExecuteExitAsync(campaignId);

        // Assert
        Assert.True(closed);

        _mockRepository.Verify(
            x => x.SaveCampaignAsync(
                It.Is<Campaign>(c =>
                    c.CloseReason == "stop_loss" &&
                    c.RealizedPnL == realizedPnL),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-17-06")]
    public async Task CheckAndExecuteExitAsync_WhenNoExitConditionMet_ReturnsFalse()
    {
        // Arrange
        string campaignId = "test-campaign-123";
        Campaign activeCampaign = CreateActiveCampaign(campaignId);

        _mockRepository
            .Setup(x => x.GetCampaignAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCampaign);

        // P&L within acceptable range
        decimal unrealizedPnL = 100m;
        _mockOrderPlacer
            .Setup(x => x.GetUnrealizedPnLAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unrealizedPnL);

        // Act
        bool closed = await _manager.CheckAndExecuteExitAsync(campaignId);

        // Assert
        Assert.False(closed);

        // Positions should NOT be closed
        _mockOrderPlacer.Verify(
            x => x.ClosePositionsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("TestId", "TEST-17-07")]
    public async Task CloseCampaignAsync_ManualClose_ClosesActivePositions()
    {
        // Arrange
        string campaignId = "test-campaign-123";
        Campaign activeCampaign = CreateActiveCampaign(campaignId);

        _mockRepository
            .Setup(x => x.GetCampaignAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCampaign);

        decimal realizedPnL = -150m;
        _mockOrderPlacer
            .Setup(x => x.ClosePositionsAsync(campaignId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(realizedPnL);

        _mockRepository
            .Setup(x => x.SaveCampaignAsync(It.IsAny<Campaign>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _manager.CloseCampaignAsync(campaignId);

        // Assert
        _mockOrderPlacer.Verify(
            x => x.ClosePositionsAsync(campaignId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockRepository.Verify(
            x => x.SaveCampaignAsync(
                It.Is<Campaign>(c =>
                    c.State == CampaignState.Closed &&
                    c.CloseReason == "manual" &&
                    c.RealizedPnL == realizedPnL),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("TestId", "TEST-17-08")]
    public async Task GetCampaignsByStateAsync_ReturnsFilteredCampaigns()
    {
        // Arrange
        List<Campaign> activeCampaigns = new()
        {
            CreateActiveCampaign("campaign-1"),
            CreateActiveCampaign("campaign-2")
        };

        _mockRepository
            .Setup(x => x.GetCampaignsByStateAsync(CampaignState.Active, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeCampaigns);

        // Act
        IReadOnlyList<Campaign> result = await _manager.GetCampaignsByStateAsync(CampaignState.Active);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(CampaignState.Active, c.State));
    }

    // Helper methods for creating mock data

    private StrategyDefinition CreateMockStrategy()
    {
        return new StrategyDefinition
        {
            StrategyName = "TestStrategy",
            Description = "Test strategy for unit tests",
            TradingMode = TradingMode.Paper,
            Underlying = new UnderlyingConfig
            {
                Symbol = "SPY",
                Exchange = "SMART",
                Currency = "USD"
            },
            EntryRules = new EntryRules
            {
                MarketConditions = new MarketConditions
                {
                    MinDaysToExpiration = 30,
                    MaxDaysToExpiration = 45,
                    IvRankMin = 30m,
                    IvRankMax = 70m
                },
                Timing = new TimingRules
                {
                    EntryTimeStart = new TimeOnly(9, 30),
                    EntryTimeEnd = new TimeOnly(15, 30),
                    DaysOfWeek = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
                }
            },
            Position = new PositionConfig
            {
                Type = "BullPutSpread",
                Legs = new[]
                {
                    new OptionLeg
                    {
                        Action = "SELL",
                        Right = "PUT",
                        StrikeSelectionMethod = "DELTA",
                        StrikeValue = -0.30m,
                        Quantity = 1
                    },
                    new OptionLeg
                    {
                        Action = "BUY",
                        Right = "PUT",
                        StrikeSelectionMethod = "OFFSET",
                        StrikeOffset = -5m,
                        Quantity = 1
                    }
                },
                MaxPositions = 3,
                CapitalPerPosition = 1000m
            },
            ExitRules = new ExitRules
            {
                ProfitTarget = 0.50m,
                StopLoss = 2.00m,
                MaxDaysInTrade = 21,
                ExitTimeOfDay = new TimeOnly(15, 45)
            },
            RiskManagement = new RiskManagement
            {
                MaxTotalCapitalAtRisk = 5000m,
                MaxDrawdownPercent = 10m,
                MaxDailyLoss = 500m
            }
        };
    }

    private Campaign CreateOpenCampaign(string campaignId)
    {
        StrategyDefinition strategy = CreateMockStrategy();
        return new Campaign
        {
            CampaignId = campaignId,
            Strategy = strategy,
            State = CampaignState.Open,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ActivatedAt = null,
            ClosedAt = null,
            CloseReason = null,
            RealizedPnL = null,
            StateJson = null
        };
    }

    private Campaign CreateActiveCampaign(string campaignId)
    {
        StrategyDefinition strategy = CreateMockStrategy();
        return new Campaign
        {
            CampaignId = campaignId,
            Strategy = strategy,
            State = CampaignState.Active,
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ActivatedAt = DateTime.UtcNow.AddHours(-1),
            ClosedAt = null,
            CloseReason = null,
            RealizedPnL = null,
            StateJson = null
        };
    }
}
