using Microsoft.Extensions.Logging.Abstractions;
using OptionsExecutionService.Orders;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Tests.Mocks;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Data;
using Xunit;

namespace OptionsExecutionService.Tests.Orders;

/// <summary>
/// Tests for OrderPlacer service.
/// Validates safety checks, circuit breaker, and order tracking.
/// </summary>
public sealed class OrderPlacerTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _dbFactory;
    private readonly MockIbkrClient _mockIbkr;
    private readonly IOrderTrackingRepository _orderRepo;
    private readonly OrderSafetyConfig _safetyConfig;
    private readonly OrderPlacer _orderPlacer;

    public OrderPlacerTests()
    {
        // Setup in-memory database
        _dbFactory = new InMemoryConnectionFactory();

        // Run migrations
        MigrationRunner migrationRunner = new(_dbFactory, NullLogger<MigrationRunner>.Instance);
        migrationRunner.RunAsync(OptionsExecutionService.Migrations.OptionsMigrations.All, CancellationToken.None)
            .GetAwaiter().GetResult();

        // Setup mock IBKR client
        _mockIbkr = new MockIbkrClient();
        _mockIbkr.ConnectAsync().GetAwaiter().GetResult();

        // Setup repository
        _orderRepo = new OrderTrackingRepository(_dbFactory, NullLogger<OrderTrackingRepository>.Instance);

        // Setup safety config with test-friendly values
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

        // Create OrderPlacer
        _orderPlacer = new OrderPlacer(
            _mockIbkr,
            _orderRepo,
            _safetyConfig,
            NullLogger<OrderPlacer>.Instance);

        // Set initial account balance
        _orderPlacer.UpdateAccountBalance(50000m);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbFactory.DisposeAsync();
        _mockIbkr.Dispose();
    }

    [Fact]
    public async Task PlaceOrderAsync_ValidOrder_Succeeds()
    {
        // Arrange
        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 2,
            LimitPrice = 15.50m,
            TimeInForce = "DAY",
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OrderId);
        Assert.NotNull(result.IbkrOrderId);
        Assert.Equal(OrderStatus.Submitted, result.Status);
        Assert.Null(result.Error);

        // Verify order was logged to database
        OrderRecord? dbOrder = await _orderRepo.GetOrderAsync(result.OrderId!);
        Assert.NotNull(dbOrder);
        Assert.Equal(request.CampaignId, dbOrder.CampaignId);
        Assert.Equal(request.Symbol, dbOrder.Symbol);
        Assert.Equal(OrderStatus.Submitted, dbOrder.Status);

        // Verify IBKR client received the order
        Assert.Single(_mockIbkr.PlacedOrders);
        Assert.Equal(result.IbkrOrderId, _mockIbkr.PlacedOrders[0].OrderId);
    }

    [Fact]
    public async Task PlaceOrderAsync_InvalidRequest_ReturnsValidationError()
    {
        // Arrange: empty campaign ID
        OrderRequest request = new()
        {
            CampaignId = "",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 2,
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.ValidationFailed, result.Status);
        Assert.Contains("CampaignId", result.Error);
        Assert.Empty(_mockIbkr.PlacedOrders);
    }

    [Fact]
    public async Task PlaceOrderAsync_ExceedsMaxPositionSize_RejectsOrder()
    {
        // Arrange: quantity exceeds max
        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 20, // Exceeds MaxPositionSize of 10
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.ValidationFailed, result.Status);
        Assert.Contains("exceeds max", result.Error);
        Assert.Empty(_mockIbkr.PlacedOrders);
    }

    [Fact]
    public async Task PlaceOrderAsync_InsufficientAccountBalance_RejectsOrder()
    {
        // Arrange: set very low account balance
        _orderPlacer.UpdateAccountBalance(5000m); // Below MinAccountBalanceUsd of 10000

        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 2,
            LimitPrice = 15.50m,
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.ValidationFailed, result.Status);
        Assert.Contains("below minimum", result.Error);
        Assert.Empty(_mockIbkr.PlacedOrders);
    }

    [Fact]
    public async Task PlaceOrderAsync_ExceedsAccountPercentage_RejectsOrder()
    {
        // Arrange: order value exceeds 20% of account
        _orderPlacer.UpdateAccountBalance(20000m);

        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 5,
            LimitPrice = 100m, // 5 * 100 * 100 = $50,000 which is > 20% of $20,000
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.ValidationFailed, result.Status);
        Assert.Contains("exceeds", result.Error);
        Assert.Contains("of account", result.Error);
        Assert.Empty(_mockIbkr.PlacedOrders);
    }

    [Fact]
    public async Task PlaceOrderAsync_IbkrNotConnected_Fails()
    {
        // Arrange: disconnect IBKR
        await _mockIbkr.DisconnectAsync();

        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 1,  // Reduced to pass safety checks
            LimitPrice = 15.50m,  // 1 * 15.50 * 100 = 1550 < 10000
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.Failed, result.Status);  // Connection check happens AFTER safety
        Assert.Contains("not connected", result.Error);
    }

    [Fact]
    public async Task PlaceOrderAsync_IbkrReturnsFailure_RecordsFailed()
    {
        // Arrange: make IBKR PlaceOrder return false
        _mockIbkr.ShouldPlaceOrderSucceed = false;

        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 1,  // Reduced to pass safety checks
            LimitPrice = 15.50m,
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.Failed, result.Status);

        // Verify order was logged as Failed
        OrderRecord? dbOrder = await _orderRepo.GetOrderAsync(result.OrderId!);
        Assert.NotNull(dbOrder);
        Assert.Equal(OrderStatus.Failed, dbOrder.Status);
    }

    [Fact]
    public async Task CircuitBreaker_TripsAfterThresholdFailures()
    {
        // Arrange: configure mock to fail all orders
        _mockIbkr.ShouldPlaceOrderSucceed = false;

        // Set account balance to pass safety checks
        // Position value will be: 2 * 100 * 100 = 20,000
        // Need balance such that 20,000 <= 20% of balance
        // Therefore balance >= 100,000
        _orderPlacer.UpdateAccountBalance(100000m);

        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 2,
            StrategyName = "TestStrategy"
        };

        // Act: place 3 failing orders (threshold is 3)
        await _orderPlacer.PlaceOrderAsync(request);
        await _orderPlacer.PlaceOrderAsync(request);
        await _orderPlacer.PlaceOrderAsync(request);

        // Assert: circuit breaker should be open
        Assert.True(_orderPlacer.IsCircuitBreakerOpen());

        // Act: try to place another order
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert: order should be rejected by circuit breaker
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.ValidationFailed, result.Status);
        Assert.Contains("Circuit breaker is open", result.Error);
    }

    [Fact]
    public async Task CircuitBreaker_CanBeManuallyReset()
    {
        // Arrange: trip the circuit breaker
        _mockIbkr.ShouldPlaceOrderSucceed = false;
        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 1,
            StrategyName = "TestStrategy"
        };

        await _orderPlacer.PlaceOrderAsync(request);
        await _orderPlacer.PlaceOrderAsync(request);
        await _orderPlacer.PlaceOrderAsync(request);

        Assert.True(_orderPlacer.IsCircuitBreakerOpen());

        // Act: manually reset
        _orderPlacer.ResetCircuitBreaker();

        // Assert: circuit breaker should be closed
        Assert.False(_orderPlacer.IsCircuitBreakerOpen());
    }

    [Fact]
    public async Task CancelOrderAsync_ValidOrder_CancelsSuccessfully()
    {
        // Arrange: place an order first
        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 2,
            LimitPrice = 15.50m,
            StrategyName = "TestStrategy"
        };

        OrderResult placeResult = await _orderPlacer.PlaceOrderAsync(request);
        Assert.True(placeResult.Success);

        // Act: cancel the order
        bool cancelled = await _orderPlacer.CancelOrderAsync(placeResult.OrderId!);

        // Assert
        Assert.True(cancelled);
        Assert.Single(_mockIbkr.CancelledOrders);
        Assert.Equal(placeResult.IbkrOrderId, _mockIbkr.CancelledOrders[0]);
    }

    [Fact]
    public async Task CancelOrderAsync_NonexistentOrder_ReturnsFalse()
    {
        // Act
        bool cancelled = await _orderPlacer.CancelOrderAsync("nonexistent-order-id");

        // Assert
        Assert.False(cancelled);
        Assert.Empty(_mockIbkr.CancelledOrders);
    }

    [Fact]
    public async Task GetOrderStatsAsync_ReturnsCorrectStats()
    {
        // Arrange: place some orders
        _mockIbkr.ShouldPlaceOrderSucceed = true;

        OrderRequest request1 = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 1,  // Reduced to pass safety checks
            LimitPrice = 15.50m,  // Explicit price for safety calc: 1 * 15.50 * 100 = 1550 < 10000
            StrategyName = "TestStrategy"
        };

        await _orderPlacer.PlaceOrderAsync(request1);
        await _orderPlacer.PlaceOrderAsync(request1);

        // Place a failing order
        _mockIbkr.ShouldPlaceOrderSucceed = false;
        await _orderPlacer.PlaceOrderAsync(request1);

        // Act
        OrderStats stats = await _orderPlacer.GetOrderStatsAsync();

        // Assert
        Assert.Equal(3, stats.TotalOrders);
        Assert.Equal(2, stats.ActiveOrders); // Submitted orders
        Assert.Equal(1, stats.FailedOrders);
        Assert.Equal(0, stats.FilledOrders);
        Assert.Equal(0, stats.CancelledOrders);
    }

    [Fact]
    public async Task PlaceOrderAsync_LimitOrderWithoutPrice_Fails()
    {
        // Arrange: Limit order without LimitPrice
        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Limit,
            Quantity = 2,
            LimitPrice = null, // Missing required field
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.ValidationFailed, result.Status);
        Assert.Contains("LimitPrice is required", result.Error);
    }

    [Fact]
    public async Task PlaceOrderAsync_StopOrderWithoutStopPrice_Fails()
    {
        // Arrange: Stop order without StopPrice
        OrderRequest request = new()
        {
            CampaignId = "test-campaign",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Sell,
            Type = OrderType.Stop,
            Quantity = 2,
            StopPrice = null, // Missing required field
            StrategyName = "TestStrategy"
        };

        // Act
        OrderResult result = await _orderPlacer.PlaceOrderAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(OrderStatus.ValidationFailed, result.Status);
        Assert.Contains("StopPrice is required", result.Error);
    }

    [Fact]
    public async Task PlaceOrderAsync_MultipleOrders_TrackedCorrectly()
    {
        // Arrange
        OrderRequest request1 = new()
        {
            CampaignId = "campaign-1",
            Symbol = "SPX",
            ContractSymbol = "SPX   250321P05000000",
            Side = OrderSide.Buy,
            Type = OrderType.Market,
            Quantity = 2,
            LimitPrice = 15.50m, // Provide price estimate for safety validation
            StrategyName = "Strategy1"
        };

        OrderRequest request2 = new()
        {
            CampaignId = "campaign-2",
            Symbol = "SPY",
            ContractSymbol = "SPY   250321C00500000",
            Side = OrderSide.Sell,
            Type = OrderType.Limit,
            Quantity = 5,
            LimitPrice = 10.25m,
            StrategyName = "Strategy2"
        };

        // Act
        OrderResult result1 = await _orderPlacer.PlaceOrderAsync(request1);
        OrderResult result2 = await _orderPlacer.PlaceOrderAsync(request2);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        Assert.NotEqual(result1.OrderId, result2.OrderId);
        Assert.NotEqual(result1.IbkrOrderId, result2.IbkrOrderId);

        // Verify both orders in database
        OrderRecord? dbOrder1 = await _orderRepo.GetOrderAsync(result1.OrderId!);
        OrderRecord? dbOrder2 = await _orderRepo.GetOrderAsync(result2.OrderId!);

        Assert.NotNull(dbOrder1);
        Assert.NotNull(dbOrder2);
        Assert.Equal("SPX", dbOrder1.Symbol);
        Assert.Equal("SPY", dbOrder2.Symbol);
    }
}
