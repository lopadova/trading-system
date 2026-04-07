using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Data;
using OptionsExecutionService.Campaign;
using OptionsExecutionService.Migrations;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Orders;
using Xunit;
using CampaignEntity = OptionsExecutionService.Campaign.Campaign;

namespace OptionsExecutionService.Tests.Repositories;

/// <summary>
/// Integration tests for OptionsExecutionService repositories with SQLite.
/// Verifies repositories persist and retrieve data correctly with real database.
/// TEST-22-31 through TEST-22-35
/// </summary>
public sealed class RepositoryIntegrationTests : IAsyncLifetime
{
    private InMemoryConnectionFactory _factory = default!;
    private ICampaignRepository _campaignRepo = default!;
    private IOrderTrackingRepository _orderRepo = default!;

    public async Task InitializeAsync()
    {
        _factory = new InMemoryConnectionFactory();

        // Run migrations
        MigrationRunner runner = new(_factory, NullLogger<MigrationRunner>.Instance);
        await runner.RunAsync(OptionsMigrations.All, CancellationToken.None);

        // Create repositories
        _campaignRepo = new CampaignRepository(_factory, NullLogger<CampaignRepository>.Instance);
        _orderRepo = new OrderTrackingRepository(_factory, NullLogger<OrderTrackingRepository>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact(DisplayName = "TEST-22-31: CampaignRepository creates and retrieves campaigns")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-31")]
    public async Task TEST_22_31_CampaignRepositoryCreatesAndRetrievesCampaigns()
    {
        // Arrange: Create test campaign
        StrategyDefinition strategy = CreateTestStrategy("test-strategy-1");
        CampaignEntity campaign = CampaignEntity.Create(strategy);

        // Act: Save campaign
        await _campaignRepo.SaveCampaignAsync(campaign, CancellationToken.None);

        // Retrieve by ID
        CampaignEntity? retrieved = await _campaignRepo.GetCampaignAsync(campaign.CampaignId, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(campaign.CampaignId, retrieved.CampaignId);
        Assert.Equal("test-strategy-1", retrieved.Strategy.StrategyName);
        Assert.Equal(CampaignState.Open, retrieved.State);
        Assert.Equal(campaign.CreatedAt, retrieved.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact(DisplayName = "TEST-22-32: CampaignRepository lists campaigns by state")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-32")]
    public async Task TEST_22_32_CampaignRepositoryListsCampaignsByState()
    {
        // Arrange: Create campaigns in different states
        StrategyDefinition strategy1 = CreateTestStrategy("strategy-1");
        StrategyDefinition strategy2 = CreateTestStrategy("strategy-2");
        StrategyDefinition strategy3 = CreateTestStrategy("strategy-3");

        CampaignEntity active1 = CampaignEntity.Create(strategy1).Activate();
        CampaignEntity active2 = CampaignEntity.Create(strategy2).Activate();
        CampaignEntity closed = CampaignEntity.Create(strategy3).Activate().Close("test", 100m);

        // Act: Save all campaigns
        await _campaignRepo.SaveCampaignAsync(active1, CancellationToken.None);
        await _campaignRepo.SaveCampaignAsync(active2, CancellationToken.None);
        await _campaignRepo.SaveCampaignAsync(closed, CancellationToken.None);

        // Get active campaigns
        IReadOnlyList<CampaignEntity> activeCampaigns = await _campaignRepo.GetCampaignsByStateAsync(
            CampaignState.Active,
            CancellationToken.None);

        // Assert: Only active campaigns should be returned
        Assert.Equal(2, activeCampaigns.Count);
        Assert.All(activeCampaigns, c => Assert.Equal(CampaignState.Active, c.State));
    }

    [Fact(DisplayName = "TEST-22-33: CampaignRepository updates campaign via save")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-33")]
    public async Task TEST_22_33_CampaignRepositoryUpdatesCampaignViaSave()
    {
        // Arrange: Create and save campaign
        StrategyDefinition strategy = CreateTestStrategy("test-strategy");
        CampaignEntity campaign = CampaignEntity.Create(strategy);
        await _campaignRepo.SaveCampaignAsync(campaign, CancellationToken.None);

        // Act: Activate campaign and save again
        CampaignEntity activated = campaign.Activate();
        await _campaignRepo.SaveCampaignAsync(activated, CancellationToken.None);

        // Retrieve updated campaign
        CampaignEntity? updated = await _campaignRepo.GetCampaignAsync(campaign.CampaignId, CancellationToken.None);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(CampaignState.Active, updated.State);
        Assert.NotNull(updated.ActivatedAt);
    }

    [Fact(DisplayName = "TEST-22-34: OrderTrackingRepository logs and retrieves orders")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-34")]
    public async Task TEST_22_34_OrderTrackingRepositoryLogsAndRetrievesOrders()
    {
        // Arrange: Create test order request
        string orderId = Guid.NewGuid().ToString();
        OrderRequest request = new()
        {
            CampaignId = Guid.NewGuid().ToString(),
            PositionId = null,
            Symbol = "SPY",
            ContractSymbol = "SPY 240119C00450000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 10,
            LimitPrice = 450.0m,
            TimeInForce = "DAY",
            StrategyName = "test-strategy",
            MetadataJson = "{}"
        };

        // Act: Log order
        await _orderRepo.LogOrderAsync(
            orderId,
            ibkrOrderId: null,
            request,
            OrderStatus.PendingSubmit,
            CancellationToken.None);

        // Retrieve by ID
        OrderRecord? retrieved = await _orderRepo.GetOrderAsync(orderId, CancellationToken.None);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(orderId, retrieved.OrderId);
        Assert.Null(retrieved.IbkrOrderId);
        Assert.Equal(OrderStatus.PendingSubmit, retrieved.Status);
        Assert.Equal("SPY", retrieved.Symbol);
        Assert.Equal(10, retrieved.Quantity);
        Assert.Equal(450.0m, retrieved.LimitPrice);
    }

    [Fact(DisplayName = "TEST-22-35: OrderTrackingRepository updates order status")]
    [Trait("TaskId", "T-22")]
    [Trait("TestId", "TEST-22-35")]
    public async Task TEST_22_35_OrderTrackingRepositoryUpdatesOrderStatus()
    {
        // Arrange: Create and log order
        string orderId = Guid.NewGuid().ToString();
        OrderRequest request = new()
        {
            CampaignId = Guid.NewGuid().ToString(),
            PositionId = null,
            Symbol = "SPY",
            ContractSymbol = "SPY 240119C00450000",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 5,
            TimeInForce = "DAY",
            StrategyName = "test-strategy",
            MetadataJson = "{}"
        };

        await _orderRepo.LogOrderAsync(
            orderId,
            ibkrOrderId: 12345,
            request,
            OrderStatus.Submitted,
            CancellationToken.None);

        // Act: Update status to filled
        await _orderRepo.UpdateOrderStatusAsync(
            orderId,
            OrderStatus.Filled,
            filledQuantity: 5,
            avgFillPrice: 451.25m,
            CancellationToken.None);

        // Retrieve updated order
        OrderRecord? updated = await _orderRepo.GetOrderAsync(orderId, CancellationToken.None);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(OrderStatus.Filled, updated.Status);
        Assert.Equal(5, updated.FilledQuantity);
        Assert.Equal(451.25m, updated.AvgFillPrice);
    }

    /// <summary>
    /// Helper to create a minimal test strategy.
    /// </summary>
    private static StrategyDefinition CreateTestStrategy(string name)
    {
        return new StrategyDefinition
        {
            StrategyName = name,
            Description = $"Test strategy {name}",
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
                    MinDaysToExpiration = 40,
                    MaxDaysToExpiration = 50,
                    IvRankMin = 20.0m,
                    IvRankMax = 80.0m
                },
                Timing = new TimingRules
                {
                    EntryTimeStart = new TimeOnly(9, 30),
                    EntryTimeEnd = new TimeOnly(16, 0),
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
                CapitalPerPosition = 5000m
            },
            ExitRules = new ExitRules
            {
                ProfitTarget = 0.5m,
                StopLoss = 2.0m,
                MaxDaysInTrade = 21,
                ExitTimeOfDay = new TimeOnly(15, 45)
            },
            RiskManagement = new RiskManagement
            {
                MaxTotalCapitalAtRisk = 15000m,
                MaxDrawdownPercent = 10.0m,
                MaxDailyLoss = 500m
            }
        };
    }
}
