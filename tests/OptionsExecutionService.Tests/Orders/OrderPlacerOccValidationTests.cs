using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using OptionsExecutionService.Orders;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Services;
using OptionsExecutionService.Tests.Mocks;
using SharedKernel.Configuration;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using SharedKernel.Observability;
using SharedKernel.Safety;

namespace OptionsExecutionService.Tests.Orders;

/// <summary>
/// RM-02: Tests for OCC format validation and option order handling
/// </summary>
public class OrderPlacerOccValidationTests
{
    private readonly Mock<IIbkrClient> _mockIbkr;
    private readonly Mock<IOrderTrackingRepository> _mockOrderRepo;
    private readonly SemaphoreGate _semaphore;
    private readonly Mock<ISafetyFlagStore> _mockFlagStore;
    private readonly Mock<IOrderAuditSink> _mockAuditSink;
    private readonly Mock<IAlerter> _mockAlerter;
    private readonly Mock<IOrderCircuitBreaker> _mockCircuitBreaker;
    private readonly Mock<IAccountEquityProvider> _mockEquityProvider;
    private readonly ILogger<OrderPlacer> _logger;
    private readonly OrderPlacer _orderPlacer;

    public OrderPlacerOccValidationTests()
    {
        _mockIbkr = new Mock<IIbkrClient>();
        _mockOrderRepo = new Mock<IOrderTrackingRepository>();
        _semaphore = GateTestHelpers.FixedGate(SemaphoreStatus.Green); // RM-02: Use real instance (sealed class)
        _mockFlagStore = new Mock<ISafetyFlagStore>();
        _mockAuditSink = new Mock<IOrderAuditSink>();
        _mockAlerter = new Mock<IAlerter>();
        _mockCircuitBreaker = new Mock<IOrderCircuitBreaker>();
        _mockEquityProvider = new Mock<IAccountEquityProvider>();
        _logger = Mock.Of<ILogger<OrderPlacer>>();

        // Setup default mocks
        _mockIbkr.Setup(x => x.IsConnected).Returns(true);
        _mockIbkr.Setup(x => x.ReserveOrderId()).Returns(1001);
        _mockFlagStore.Setup(x => x.IsSetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockCircuitBreaker.Setup(x => x.IsOpen()).Returns(false);

        // Setup equity provider with fresh equity
        AccountEquitySnapshot equity = new()
        {
            NetLiquidation = 100000m,
            AsOfUtc = DateTime.UtcNow,
            IsStale = false,
            Age = TimeSpan.Zero
        };
        _mockEquityProvider.Setup(x => x.GetEquity()).Returns(equity);

        SafetyOptions safetyOptions = new() { OverrideSemaphore = false };
        OrderSafetyConfig safetyConfig = new()
        {
            TradingMode = TradingMode.Paper,
            MaxPositionSize = 100,
            MaxPositionValueUsd = 50000m,
            MinAccountBalanceUsd = 1000m,
            MaxPositionPctOfAccount = 0.10m,
            CircuitBreakerFailureThreshold = 3,
            CircuitBreakerWindowMinutes = 30
        };

        _orderPlacer = new OrderPlacer(
            _mockIbkr.Object,
            _mockOrderRepo.Object,
            safetyConfig,
            _semaphore, // RM-02: Pass real instance instead of mock
            _mockFlagStore.Object,
            _mockAuditSink.Object,
            _mockAlerter.Object,
            _mockCircuitBreaker.Object,
            _mockEquityProvider.Object,
            Options.Create(safetyOptions),
            _logger);
    }

    [Fact]
    public async Task PlaceEntryOrdersAsync_SinglePutLeg_PopulatesOccFormatAndOptionFields()
    {
        // Arrange
        string campaignId = "campaign-123";
        StrategyDefinition strategy = CreateStrategyWithSinglePutLeg();

        _mockIbkr.Setup(x => x.PlaceOrder(It.IsAny<int>(), It.IsAny<OrderRequest>()))
            .Returns(true);
        _mockOrderRepo.Setup(x => x.LogOrderAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<OrderRequest>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockOrderRepo.Setup(x => x.UpdateOrderStatusAsync(
                It.IsAny<string>(), It.IsAny<OrderStatus>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        IReadOnlyList<string> orderIds = await _orderPlacer.PlaceEntryOrdersAsync(campaignId, strategy);

        // Assert
        Assert.Single(orderIds);

        // RM-02: Verify OrderRequest was populated with OCC-format ContractSymbol and option fields
        _mockIbkr.Verify(x => x.PlaceOrder(It.IsAny<int>(), It.Is<OrderRequest>(req =>
            req.SecurityType == "OPT" &&
            req.Strike == 5000.00m &&
            !string.IsNullOrEmpty(req.Expiry) &&
            req.Expiry!.Length == 8 && // YYYYMMDD format
            req.OptionRight == "P" &&
            req.ContractSymbol.Length == 21 && // OCC format is 21 chars
            req.ContractSymbol.StartsWith("SPX   ") && // Underlying padded to 6 chars
            req.ContractSymbol.Contains("P") && // Right
            req.ContractSymbol.EndsWith("05000000") // Strike 5000.00 → 05000000
        )), Times.Once);
    }

    [Fact]
    public async Task PlaceEntryOrdersAsync_CallLeg_PopulatesCallRight()
    {
        // Arrange
        string campaignId = "campaign-456";
        StrategyDefinition strategy = CreateStrategyWithSingleCallLeg();

        _mockIbkr.Setup(x => x.PlaceOrder(It.IsAny<int>(), It.IsAny<OrderRequest>()))
            .Returns(true);
        _mockOrderRepo.Setup(x => x.LogOrderAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<OrderRequest>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockOrderRepo.Setup(x => x.UpdateOrderStatusAsync(
                It.IsAny<string>(), It.IsAny<OrderStatus>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        IReadOnlyList<string> orderIds = await _orderPlacer.PlaceEntryOrdersAsync(campaignId, strategy);

        // Assert
        Assert.Single(orderIds);

        // RM-02: Verify OptionRight is "C" for call
        _mockIbkr.Verify(x => x.PlaceOrder(It.IsAny<int>(), It.Is<OrderRequest>(req =>
            req.OptionRight == "C" &&
            req.ContractSymbol.Contains("C") // OCC format has 'C' for call
        )), Times.Once);
    }

    [Fact]
    public async Task PlaceEntryOrdersAsync_MultiLegSpread_AllLegsHaveOccFormat()
    {
        // Arrange
        string campaignId = "campaign-789";
        StrategyDefinition strategy = CreateBullPutSpread();

        _mockIbkr.Setup(x => x.PlaceOrder(It.IsAny<int>(), It.IsAny<OrderRequest>()))
            .Returns(true);
        _mockOrderRepo.Setup(x => x.LogOrderAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<OrderRequest>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockOrderRepo.Setup(x => x.UpdateOrderStatusAsync(
                It.IsAny<string>(), It.IsAny<OrderStatus>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        IReadOnlyList<string> orderIds = await _orderPlacer.PlaceEntryOrdersAsync(campaignId, strategy);

        // Assert
        Assert.Equal(2, orderIds.Count); // Bull put spread has 2 legs

        // RM-02: Verify both legs have OCC format and option fields
        _mockIbkr.Verify(x => x.PlaceOrder(It.IsAny<int>(), It.Is<OrderRequest>(req =>
            req.SecurityType == "OPT" &&
            req.Strike.HasValue &&
            !string.IsNullOrEmpty(req.Expiry) &&
            !string.IsNullOrEmpty(req.OptionRight) &&
            req.ContractSymbol.Length == 21
        )), Times.Exactly(2));
    }

    [Fact]
    public async Task PlaceEntryOrdersAsync_OccSymbolRoundTrip_ParsedComponentsMatchOriginal()
    {
        // Arrange
        string campaignId = "campaign-roundtrip";
        StrategyDefinition strategy = CreateStrategyWithSinglePutLeg();

        OrderRequest? capturedRequest = null;
        _mockIbkr.Setup(x => x.PlaceOrder(It.IsAny<int>(), It.IsAny<OrderRequest>()))
            .Callback<int, OrderRequest>((_, req) => capturedRequest = req)
            .Returns(true);
        _mockOrderRepo.Setup(x => x.LogOrderAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<OrderRequest>(), It.IsAny<OrderStatus>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockOrderRepo.Setup(x => x.UpdateOrderStatusAsync(
                It.IsAny<string>(), It.IsAny<OrderStatus>(), It.IsAny<int>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _orderPlacer.PlaceEntryOrdersAsync(campaignId, strategy);

        // Assert
        Assert.NotNull(capturedRequest);

        // RM-02: Parse the OCC symbol and verify components match original request
        OccSymbolParser.OccSymbolComponents parsed = OccSymbolParser.Parse(capturedRequest.ContractSymbol);
        Assert.Equal("SPX", parsed.Underlying);
        Assert.Equal(capturedRequest.Strike!.Value, parsed.Strike);
        Assert.Equal(capturedRequest.OptionRight, parsed.Right);
        Assert.Equal(capturedRequest.Expiry, parsed.ExpiryYyyyMmDd);
    }

    // Helper methods to create test strategies

    private static StrategyDefinition CreateStrategyWithSinglePutLeg()
    {
        return new StrategyDefinition
        {
            StrategyName = "Single Put Test",
            Description = "Test strategy with single put leg",
            TradingMode = TradingMode.Paper,
            Underlying = new UnderlyingConfig
            {
                Symbol = "SPX",
                Exchange = "CBOE",
                Currency = "USD"
            },
            EntryRules = CreateDefaultEntryRules(),
            Position = new PositionConfig
            {
                Type = "Put",
                Legs = new[]
                {
                    new OptionLeg
                    {
                        Action = "SELL",
                        Right = "PUT",
                        StrikeSelectionMethod = "ABSOLUTE",
                        StrikeValue = 5000.00m,
                        Quantity = 1
                    }
                },
                MaxPositions = 1,
                CapitalPerPosition = 5000m
            },
            ExitRules = CreateDefaultExitRules(),
            RiskManagement = CreateDefaultRiskManagement()
        };
    }

    private static StrategyDefinition CreateStrategyWithSingleCallLeg()
    {
        return new StrategyDefinition
        {
            StrategyName = "Single Call Test",
            Description = "Test strategy with single call leg",
            TradingMode = TradingMode.Paper,
            Underlying = new UnderlyingConfig
            {
                Symbol = "SPY",
                Exchange = "SMART",
                Currency = "USD"
            },
            EntryRules = CreateDefaultEntryRules(),
            Position = new PositionConfig
            {
                Type = "Call",
                Legs = new[]
                {
                    new OptionLeg
                    {
                        Action = "BUY",
                        Right = "CALL",
                        StrikeSelectionMethod = "ABSOLUTE",
                        StrikeValue = 450.00m,
                        Quantity = 1
                    }
                },
                MaxPositions = 1,
                CapitalPerPosition = 5000m
            },
            ExitRules = CreateDefaultExitRules(),
            RiskManagement = CreateDefaultRiskManagement()
        };
    }

    private static StrategyDefinition CreateBullPutSpread()
    {
        return new StrategyDefinition
        {
            StrategyName = "Bull Put Spread Test",
            Description = "Test strategy with two-leg put spread",
            TradingMode = TradingMode.Paper,
            Underlying = new UnderlyingConfig
            {
                Symbol = "SPX",
                Exchange = "CBOE",
                Currency = "USD"
            },
            EntryRules = CreateDefaultEntryRules(),
            Position = new PositionConfig
            {
                Type = "BullPutSpread",
                Legs = new[]
                {
                    new OptionLeg
                    {
                        Action = "SELL",
                        Right = "PUT",
                        StrikeSelectionMethod = "ABSOLUTE",
                        StrikeValue = 5000.00m,
                        Quantity = 1
                    },
                    new OptionLeg
                    {
                        Action = "BUY",
                        Right = "PUT",
                        StrikeSelectionMethod = "ABSOLUTE",
                        StrikeValue = 4950.00m,
                        Quantity = 1
                    }
                },
                MaxPositions = 1,
                CapitalPerPosition = 5000m
            },
            ExitRules = CreateDefaultExitRules(),
            RiskManagement = CreateDefaultRiskManagement()
        };
    }

    private static EntryRules CreateDefaultEntryRules()
    {
        return new EntryRules
        {
            MarketConditions = new MarketConditions
            {
                MinDaysToExpiration = 30,
                MaxDaysToExpiration = 45,
                IvRankMin = 25.0m,
                IvRankMax = 75.0m
            },
            Timing = new TimingRules
            {
                EntryTimeStart = new TimeOnly(10, 0),
                EntryTimeEnd = new TimeOnly(14, 0),
                DaysOfWeek = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday }
            }
        };
    }

    private static ExitRules CreateDefaultExitRules()
    {
        return new ExitRules
        {
            ProfitTarget = 0.50m,
            StopLoss = 2.00m,
            MaxDaysInTrade = 21,
            ExitTimeOfDay = new TimeOnly(15, 50)
        };
    }

    private static RiskManagement CreateDefaultRiskManagement()
    {
        return new RiskManagement
        {
            MaxTotalCapitalAtRisk = 10000m,
            MaxDrawdownPercent = 10.0m,
            MaxDailyLoss = 1000m
        };
    }
}
