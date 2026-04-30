using OptionsExecutionService.Campaign;
using SharedKernel.Domain;
using Xunit;

namespace OptionsExecutionService.Tests.Campaign;

/// <summary>
/// Tests for Campaign domain entity state transitions.
/// Verifies state machine invariants and lifecycle correctness.
/// Phase 1: State Persistence & Idempotency
/// </summary>
public sealed class CampaignTests
{
    [Fact]
    public void Create_CreatesOpenCampaign()
    {
        // Arrange
        StrategyDefinition strategy = CreateMockStrategy();

        // Act
        var campaign = OptionsExecutionService.Campaign.Campaign.Create(strategy);

        // Assert
        Assert.Equal(CampaignState.Open, campaign.State);
        Assert.NotNull(campaign.CampaignId);
        Assert.NotEqual(default(DateTime), campaign.CreatedAt);
        Assert.Null(campaign.ActivatedAt);
        Assert.Null(campaign.ClosedAt);
        Assert.Null(campaign.CloseReason);
        Assert.Null(campaign.RealizedPnL);
    }

    [Fact]
    public void Activate_TransitionsOpenToActive()
    {
        // Arrange
        StrategyDefinition strategy = CreateMockStrategy();
        var campaign = OptionsExecutionService.Campaign.Campaign.Create(strategy);

        // Act
        var activated = campaign.Activate();

        // Assert
        Assert.Equal(CampaignState.Active, activated.State);
        Assert.NotNull(activated.ActivatedAt);
        Assert.NotEqual(default(DateTime), activated.ActivatedAt.Value);
    }

    [Fact]
    public void Activate_ThrowsIfNotOpen()
    {
        // Arrange
        StrategyDefinition strategy = CreateMockStrategy();
        var campaign = OptionsExecutionService.Campaign.Campaign.Create(strategy).Activate();

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => campaign.Activate());
        Assert.Contains("Cannot activate campaign in state Active", ex.Message);
    }

    [Fact]
    public void BeginExit_TransitionsActiveToPendingExit()
    {
        // Arrange
        StrategyDefinition strategy = CreateMockStrategy();
        var campaign = OptionsExecutionService.Campaign.Campaign.Create(strategy).Activate();

        // Act
        var pendingExit = campaign.BeginExit();

        // Assert
        Assert.Equal(CampaignState.PendingExit, pendingExit.State);
        Assert.NotNull(pendingExit.PendingExitAt);
        Assert.NotEqual(default(DateTime), pendingExit.PendingExitAt.Value);
        // Campaign should still be active (positions not yet closed)
        Assert.NotNull(pendingExit.ActivatedAt);
        Assert.Null(pendingExit.ClosedAt);
    }

    [Fact]
    public void BeginExit_ThrowsIfNotActive()
    {
        // Arrange
        StrategyDefinition strategy = CreateMockStrategy();
        var campaign = OptionsExecutionService.Campaign.Campaign.Create(strategy); // Open state

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => campaign.BeginExit());
        Assert.Contains("Cannot begin exit from state Open", ex.Message);
    }

    [Fact]
    public void Close_TransitionsPendingExitToClosed()
    {
        // Arrange
        StrategyDefinition strategy = CreateMockStrategy();
        var campaign = OptionsExecutionService.Campaign.Campaign.Create(strategy)
            .Activate()
            .BeginExit();

        // Act
        var closed = campaign.Close("profit_target", 1500.00m);

        // Assert
        Assert.Equal(CampaignState.Closed, closed.State);
        Assert.NotNull(closed.ClosedAt);
        Assert.Equal("profit_target", closed.CloseReason);
        Assert.Equal(1500.00m, closed.RealizedPnL);
    }

    [Fact]
    public void Close_ThrowsIfAlreadyClosed()
    {
        // Arrange
        StrategyDefinition strategy = CreateMockStrategy();
        var campaign = OptionsExecutionService.Campaign.Campaign.Create(strategy)
            .Activate()
            .BeginExit()
            .Close("profit_target", 1500.00m);

        // Act & Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => campaign.Close("manual", 0m));
        Assert.Contains("Campaign is already closed", ex.Message);
    }

    private static StrategyDefinition CreateMockStrategy()
    {
        return new StrategyDefinition
        {
            StrategyName = "TestStrategy",
            Description = "Test strategy for PendingExit transition",
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
                    DaysOfWeek = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday }
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
                    }
                },
                MaxPositions = 3,
                CapitalPerPosition = 1000m
            },
            ExitRules = new ExitRules
            {
                ProfitTarget = 0.50m,
                StopLoss = 2.00m,
                MaxDaysInTrade = 7,
                ExitTimeOfDay = new TimeOnly(15, 0)
            },
            RiskManagement = new RiskManagement
            {
                MaxTotalCapitalAtRisk = 5000m,
                MaxDrawdownPercent = 10m,
                MaxDailyLoss = 500m
            }
        };
    }
}
