using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OptionsExecutionService.Orders;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Services;
using OptionsExecutionService.Tests.Mocks;
using SharedKernel.Configuration;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Safety;
using SharedKernel.Tests.Data;
using Xunit;

namespace OptionsExecutionService.Tests.Orders;

/// <summary>
/// Tests for OrderPlacer campaign entry methods (PlaceEntryOrdersAsync).
/// Phase 4: Campaign execution P1 - Task RM-03
/// </summary>
public sealed class OrderPlacerCampaignEntryTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _db;
    private readonly MockIbkrClient _ibkr;
    private readonly IOrderTrackingRepository _orderRepo;
    private readonly OrderSafetyConfig _safetyConfig;
    private readonly RecordingAlerter _alerter;
    private readonly InMemorySafetyFlagStore _flagStore;
    private readonly RecordingAuditSink _auditSink;
    private readonly IOrderCircuitBreaker _circuitBreaker;
    private readonly IAccountEquityProvider _equityProvider;

    public OrderPlacerCampaignEntryTests()
    {
        _db = new InMemoryConnectionFactory();
        MigrationRunner runner = new(_db, NullLogger<MigrationRunner>.Instance);
        runner.RunAsync(OptionsExecutionService.Migrations.OptionsMigrations.All, CancellationToken.None)
            .GetAwaiter().GetResult();

        _ibkr = new MockIbkrClient();
        _ibkr.ConnectAsync().GetAwaiter().GetResult();

        _orderRepo = new OrderTrackingRepository(_db, NullLogger<OrderTrackingRepository>.Instance);
        _safetyConfig = new OrderSafetyConfig
        {
            TradingMode = TradingMode.Paper,
            MaxPositionSize = 10,
            MaxPositionValueUsd = 50000m,
            MinAccountBalanceUsd = 10000m,
            MaxPositionPctOfAccount = 0.2m,
            CircuitBreakerFailureThreshold = 3,
            CircuitBreakerWindowMinutes = 60,
            CircuitBreakerCooldownMinutes = 30
        };
        _alerter = new RecordingAlerter();
        _flagStore = new InMemorySafetyFlagStore();
        _auditSink = new RecordingAuditSink();

        // Real singleton services
        _circuitBreaker = new OrderCircuitBreaker(_safetyConfig, NullLogger<OrderCircuitBreaker>.Instance);
        _equityProvider = new AccountEquityProvider(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Safety:AccountBalanceMaxAgeSeconds"] = "300"
            }).Build(),
            NullLogger<AccountEquityProvider>.Instance);
        _equityProvider.UpdateEquity(100000m, DateTime.UtcNow); // Set fresh equity
    }

    public async ValueTask DisposeAsync()
    {
        _ibkr.Dispose();
        await _db.DisposeAsync();
    }

    private OrderPlacer BuildPlacer()
    {
        SemaphoreGate gate = GateTestHelpers.FixedGate(SemaphoreStatus.Green);

        return new OrderPlacer(
            _ibkr,
            _orderRepo,
            _safetyConfig,
            gate,
            _flagStore,
            _auditSink,
            _alerter,
            _circuitBreaker,
            _equityProvider,
            Options.Create(new SafetyOptions()),
            NullLogger<OrderPlacer>.Instance);
    }

    private static StrategyDefinition BuildTestStrategy(params OptionLeg[] legs)
    {
        return new StrategyDefinition
        {
            StrategyName = "TestStrategy",
            Description = "Test strategy for campaign entry",
            TradingMode = TradingMode.Paper,
            Underlying = new UnderlyingConfig
            {
                Symbol = "SPX",
                Exchange = "SMART",
                Currency = "USD"
            },
            EntryRules = new EntryRules
            {
                MarketConditions = new MarketConditions
                {
                    MinDaysToExpiration = 30,
                    MaxDaysToExpiration = 45,
                    IvRankMin = 0.3m,
                    IvRankMax = 0.7m
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
                Legs = legs,
                MaxPositions = 5,
                CapitalPerPosition = 10000m
            },
            ExitRules = new ExitRules
            {
                ProfitTarget = 0.5m,
                StopLoss = 2.0m,
                MaxDaysInTrade = 30,
                ExitTimeOfDay = new TimeOnly(15, 45)
            },
            RiskManagement = new RiskManagement
            {
                MaxTotalCapitalAtRisk = 50000m,
                MaxDrawdownPercent = 10m,
                MaxDailyLoss = 5000m
            }
        };
    }

    /// <summary>
    /// Verifies that PlaceEntryOrdersAsync creates real orders for all legs.
    /// </summary>
    [Fact]
    public async Task PlaceEntryOrdersAsync_CreatesRealOrders_ForAllLegs()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        OrderPlacer placer = BuildPlacer();

        OptionLeg[] legs = new[]
        {
            new OptionLeg
            {
                Action = "SELL",
                Right = "PUT",
                StrikeSelectionMethod = "ABSOLUTE",
                StrikeValue = 5000m,
                Quantity = 1
            },
            new OptionLeg
            {
                Action = "BUY",
                Right = "PUT",
                StrikeSelectionMethod = "ABSOLUTE",
                StrikeValue = 4950m,
                Quantity = 1
            }
        };

        StrategyDefinition strategy = BuildTestStrategy(legs);

        // ============================================================
        // ACT
        // ============================================================

        IReadOnlyList<string> orderIds = await placer.PlaceEntryOrdersAsync("campaign-test", strategy, CancellationToken.None);

        // ============================================================
        // ASSERT
        // ============================================================

        Assert.Equal(2, orderIds.Count); // 2 legs = 2 orders
        Assert.All(orderIds, orderId => Assert.False(string.IsNullOrWhiteSpace(orderId)));
        Assert.Equal(2, _ibkr.PlacedOrders.Count); // 2 IBKR orders placed

        // Verify orders in database
        foreach (string orderId in orderIds)
        {
            OrderRecord? order = await _orderRepo.GetOrderAsync(orderId, CancellationToken.None);
            Assert.NotNull(order);
            Assert.Equal("campaign-test", order.CampaignId);
            Assert.Equal("TestStrategy", order.StrategyName);
        }
    }

    /// <summary>
    /// Verifies that if one leg fails, PlaceEntryOrdersAsync throws and campaign does not activate.
    /// </summary>
    [Fact]
    public async Task PlaceEntryOrdersAsync_ThrowsException_WhenLegFails()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        OrderPlacer placer = BuildPlacer();

        OptionLeg[] legs = new[]
        {
            new OptionLeg
            {
                Action = "SELL",
                Right = "PUT",
                StrikeSelectionMethod = "ABSOLUTE",
                StrikeValue = 5000m,
                Quantity = 1
            },
            new OptionLeg
            {
                Action = "BUY",
                Right = "PUT",
                StrikeSelectionMethod = "ABSOLUTE",
                StrikeValue = 4950m,
                Quantity = 99 // Exceeds MaxPositionSize → will fail
            }
        };

        StrategyDefinition strategy = BuildTestStrategy(legs);

        // ============================================================
        // ACT & ASSERT
        // ============================================================

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await placer.PlaceEntryOrdersAsync("campaign-fail", strategy, CancellationToken.None));

        Assert.Contains("entry failed at leg 1", ex.Message);
        Assert.Contains("Position size 99 exceeds max 10", ex.Message);

        // First leg should have been placed
        Assert.Single(_ibkr.PlacedOrders);
    }

    /// <summary>
    /// Verifies that PlaceEntryOrdersAsync throws if strategy has no legs.
    /// </summary>
    [Fact]
    public async Task PlaceEntryOrdersAsync_Throws_WhenNoLegs()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        OrderPlacer placer = BuildPlacer();
        StrategyDefinition strategy = BuildTestStrategy(); // No legs

        // ============================================================
        // ACT & ASSERT
        // ============================================================

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await placer.PlaceEntryOrdersAsync("campaign-no-legs", strategy, CancellationToken.None));

        Assert.Contains("has no legs defined", ex.Message);
    }

    /// <summary>
    /// Verifies that PlaceEntryOrdersAsync throws if leg action is missing.
    /// </summary>
    [Fact]
    public async Task PlaceEntryOrdersAsync_Throws_WhenLegActionMissing()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        OrderPlacer placer = BuildPlacer();

        OptionLeg[] legs = new[]
        {
            new OptionLeg
            {
                Action = "", // Missing action
                Right = "PUT",
                StrikeSelectionMethod = "ABSOLUTE",
                StrikeValue = 5000m,
                Quantity = 1
            }
        };

        StrategyDefinition strategy = BuildTestStrategy(legs);

        // ============================================================
        // ACT & ASSERT
        // ============================================================

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await placer.PlaceEntryOrdersAsync("campaign-bad-action", strategy, CancellationToken.None));

        Assert.Contains("Action (BUY/SELL) is required", ex.Message);
    }

    /// <summary>
    /// Verifies that PlaceEntryOrdersAsync throws if leg right is missing.
    /// </summary>
    [Fact]
    public async Task PlaceEntryOrdersAsync_Throws_WhenLegRightMissing()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        OrderPlacer placer = BuildPlacer();

        OptionLeg[] legs = new[]
        {
            new OptionLeg
            {
                Action = "SELL",
                Right = "", // Missing right
                StrikeSelectionMethod = "ABSOLUTE",
                StrikeValue = 5000m,
                Quantity = 1
            }
        };

        StrategyDefinition strategy = BuildTestStrategy(legs);

        // ============================================================
        // ACT & ASSERT
        // ============================================================

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await placer.PlaceEntryOrdersAsync("campaign-bad-right", strategy, CancellationToken.None));

        Assert.Contains("Right (CALL/PUT) is required", ex.Message);
    }

    /// <summary>
    /// Verifies that PlaceEntryOrdersAsync throws if strike selection method is DELTA (not yet implemented).
    /// </summary>
    [Fact]
    public async Task PlaceEntryOrdersAsync_Throws_WhenDeltaSelectionNotImplemented()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        OrderPlacer placer = BuildPlacer();

        OptionLeg[] legs = new[]
        {
            new OptionLeg
            {
                Action = "SELL",
                Right = "PUT",
                StrikeSelectionMethod = "DELTA", // Not implemented yet
                StrikeValue = null, // No explicit strike
                Quantity = 1
            }
        };

        StrategyDefinition strategy = BuildTestStrategy(legs);

        // ============================================================
        // ACT & ASSERT
        // ============================================================

        NotImplementedException ex = await Assert.ThrowsAsync<NotImplementedException>(
            async () => await placer.PlaceEntryOrdersAsync("campaign-delta", strategy, CancellationToken.None));

        Assert.Contains("DELTA/OFFSET not yet implemented", ex.Message);
    }
}
