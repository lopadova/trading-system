using Microsoft.Extensions.Logging.Abstractions;
using OptionsExecutionService.Repositories;
using OptionsExecutionService.Services;
using OptionsExecutionService.Tests.Mocks;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Tests.Data;
using Xunit;

namespace OptionsExecutionService.Tests.Services;

/// <summary>
/// Tests for OrderStatusHandler - background service that subscribes to IBKR order callbacks.
/// Phase 3: Broker callback persistence P1 - Task RM-04
/// </summary>
public sealed class OrderStatusHandlerTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _db;
    private readonly MockIbkrClient _ibkr;
    private readonly IOrderTrackingRepository _orderRepo;
    private readonly OrderStatusHandler _handler;

    public OrderStatusHandlerTests()
    {
        _db = new InMemoryConnectionFactory();
        MigrationRunner runner = new(_db, NullLogger<MigrationRunner>.Instance);
        runner.RunAsync(OptionsExecutionService.Migrations.OptionsMigrations.All, CancellationToken.None)
            .GetAwaiter().GetResult();

        _ibkr = new MockIbkrClient();
        _ibkr.ConnectAsync().GetAwaiter().GetResult();

        _orderRepo = new OrderTrackingRepository(_db, NullLogger<OrderTrackingRepository>.Instance);

        _handler = new OrderStatusHandler(
            _ibkr,
            _orderRepo,
            NullLogger<OrderStatusHandler>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _handler.StopAsync(CancellationToken.None);
        _ibkr.Dispose();
        await _db.DisposeAsync();
    }

    /// <summary>
    /// Verifies that OrderStatusChanged event correctly updates order_tracking status.
    /// </summary>
    [Fact]
    public async Task OnOrderStatusChanged_UpdatesOrderTracking_WhenOrderExists()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        // Create order in database
        string orderId = Guid.NewGuid().ToString();
        int ibkrOrderId = 12345;

        await _orderRepo.LogOrderAsync(
            orderId,
            ibkrOrderId,
            new OrderRequest
            {
                CampaignId = "test-campaign",
                Symbol = "SPX",
                ContractSymbol = "SPX   250321P05000000",
                Side = OrderSide.Buy,
                Type = OrderType.Limit,
                Quantity = 1,
                LimitPrice = 12.5m,
                StrategyName = "TestStrategy"
            },
            OrderStatus.Submitted,
            CancellationToken.None);

        // Start handler (subscribes to events)
        await _handler.StartAsync(CancellationToken.None);

        // Wait briefly for subscription to be active
        await Task.Delay(100);

        // ============================================================
        // ACT
        // ============================================================

        // Simulate IBKR orderStatus callback
        _ibkr.SimulateOrderStatusChanged(ibkrOrderId, "Filled", filled: 1, remaining: 0, avgFillPrice: 12.3);

        // Wait for async event handler to process
        await Task.Delay(200);

        // ============================================================
        // ASSERT
        // ============================================================

        OrderRecord? order = await _orderRepo.GetOrderAsync(orderId, CancellationToken.None);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Filled, order.Status);
        Assert.Equal(1, order.FilledQuantity);
        Assert.Equal(12.3m, order.AvgFillPrice);
    }

    /// <summary>
    /// Verifies that critical IBKR errors mark order as Failed.
    /// </summary>
    [Fact]
    public async Task OnOrderError_MarksOrderFailed_WhenCriticalError()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        string orderId = Guid.NewGuid().ToString();
        int ibkrOrderId = 67890;

        await _orderRepo.LogOrderAsync(
            orderId,
            ibkrOrderId,
            new OrderRequest
            {
                CampaignId = "test-campaign",
                Symbol = "SPX",
                ContractSymbol = "SPX   250321P05000000",
                Side = OrderSide.Buy,
                Type = OrderType.Limit,
                Quantity = 1,
                LimitPrice = 12.5m,
                StrategyName = "TestStrategy"
            },
            OrderStatus.Submitted,
            CancellationToken.None);

        await _handler.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // ============================================================
        // ACT
        // ============================================================

        // Simulate critical error (error code 201 = order rejected)
        _ibkr.SimulateOrderError(ibkrOrderId, errorCode: 201, errorMessage: "Order rejected - insufficient margin");

        await Task.Delay(200);

        // ============================================================
        // ASSERT
        // ============================================================

        OrderRecord? order = await _orderRepo.GetOrderAsync(orderId, CancellationToken.None);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal(0, order.FilledQuantity);
        Assert.Null(order.AvgFillPrice); // Null when no fill (avgFillPrice=0 becomes null in repository)
    }

    /// <summary>
    /// Verifies that non-critical IBKR errors do NOT mark order as Failed.
    /// </summary>
    [Fact]
    public async Task OnOrderError_DoesNotMarkFailed_WhenNonCriticalError()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        string orderId = Guid.NewGuid().ToString();
        int ibkrOrderId = 11111;

        await _orderRepo.LogOrderAsync(
            orderId,
            ibkrOrderId,
            new OrderRequest
            {
                CampaignId = "test-campaign",
                Symbol = "SPX",
                ContractSymbol = "SPX   250321P05000000",
                Side = OrderSide.Buy,
                Type = OrderType.Limit,
                Quantity = 1,
                LimitPrice = 12.5m,
                StrategyName = "TestStrategy"
            },
            OrderStatus.Submitted,
            CancellationToken.None);

        await _handler.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // ============================================================
        // ACT
        // ============================================================

        // Simulate informational error (error code 2104 = market data farm connection OK)
        _ibkr.SimulateOrderError(ibkrOrderId, errorCode: 2104, errorMessage: "Market data farm connection is OK");

        await Task.Delay(200);

        // ============================================================
        // ASSERT
        // ============================================================

        OrderRecord? order = await _orderRepo.GetOrderAsync(orderId, CancellationToken.None);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.Submitted, order.Status); // Status unchanged
    }

    /// <summary>
    /// Verifies that unknown IBKR order IDs are handled gracefully (log warning, no crash).
    /// </summary>
    [Fact]
    public async Task OnOrderStatusChanged_HandlesUnknownOrderId_Gracefully()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        await _handler.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // ============================================================
        // ACT
        // ============================================================

        // Simulate orderStatus for unknown IBKR order ID (manual TWS order)
        _ibkr.SimulateOrderStatusChanged(99999, "Filled", filled: 1, remaining: 0, avgFillPrice: 100.0);

        await Task.Delay(200);

        // ============================================================
        // ASSERT
        // ============================================================

        // Handler should NOT crash - no assertion needed, test passes if no exception
        // In production, this would log a warning: "Received orderStatus callback for unknown IBKR orderId=99999"
    }

    /// <summary>
    /// Verifies that duplicate status updates are idempotent (same status multiple times).
    /// </summary>
    [Fact]
    public async Task OnOrderStatusChanged_IsIdempotent_WhenDuplicateUpdates()
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        string orderId = Guid.NewGuid().ToString();
        int ibkrOrderId = 22222;

        await _orderRepo.LogOrderAsync(
            orderId,
            ibkrOrderId,
            new OrderRequest
            {
                CampaignId = "test-campaign",
                Symbol = "SPX",
                ContractSymbol = "SPX   250321P05000000",
                Side = OrderSide.Buy,
                Type = OrderType.Limit,
                Quantity = 1,
                LimitPrice = 12.5m,
                StrategyName = "TestStrategy"
            },
            OrderStatus.Submitted,
            CancellationToken.None);

        await _handler.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // ============================================================
        // ACT
        // ============================================================

        // Simulate same status update 3 times (IBKR can send duplicates)
        _ibkr.SimulateOrderStatusChanged(ibkrOrderId, "PartFilled", filled: 1, remaining: 0, avgFillPrice: 12.3);
        await Task.Delay(100);
        _ibkr.SimulateOrderStatusChanged(ibkrOrderId, "PartFilled", filled: 1, remaining: 0, avgFillPrice: 12.3);
        await Task.Delay(100);
        _ibkr.SimulateOrderStatusChanged(ibkrOrderId, "PartFilled", filled: 1, remaining: 0, avgFillPrice: 12.3);
        await Task.Delay(100);

        // ============================================================
        // ASSERT
        // ============================================================

        OrderRecord? order = await _orderRepo.GetOrderAsync(orderId, CancellationToken.None);
        Assert.NotNull(order);
        Assert.Equal(OrderStatus.PartiallyFilled, order.Status);
        Assert.Equal(1, order.FilledQuantity);
        Assert.Equal(12.3m, order.AvgFillPrice);

        // Idempotent: 3 updates produce same result as 1 update
    }

    /// <summary>
    /// Verifies that handler maps all IBKR status strings correctly.
    /// </summary>
    [Theory]
    [InlineData("ApiPending", OrderStatus.PendingSubmit)]
    [InlineData("PendingSubmit", OrderStatus.PendingSubmit)]
    [InlineData("PreSubmitted", OrderStatus.PendingSubmit)]
    [InlineData("Submitted", OrderStatus.Submitted)]
    [InlineData("Active", OrderStatus.Active)]
    [InlineData("PartFilled", OrderStatus.PartiallyFilled)]
    [InlineData("Filled", OrderStatus.Filled)]
    [InlineData("Cancelled", OrderStatus.Cancelled)]
    [InlineData("ApiCancelled", OrderStatus.Cancelled)]
    [InlineData("Inactive", OrderStatus.Cancelled)]
    [InlineData("PendingCancel", OrderStatus.Cancelled)]
    public async Task OnOrderStatusChanged_MapsIbkrStatusCorrectly(string ibkrStatus, OrderStatus expectedStatus)
    {
        // ============================================================
        // ARRANGE
        // ============================================================

        string orderId = Guid.NewGuid().ToString();
        int ibkrOrderId = Random.Shared.Next(10000, 99999);

        await _orderRepo.LogOrderAsync(
            orderId,
            ibkrOrderId,
            new OrderRequest
            {
                CampaignId = "test-campaign",
                Symbol = "SPX",
                ContractSymbol = "SPX   250321P05000000",
                Side = OrderSide.Buy,
                Type = OrderType.Limit,
                Quantity = 1,
                LimitPrice = 12.5m,
                StrategyName = "TestStrategy"
            },
            OrderStatus.Submitted,
            CancellationToken.None);

        await _handler.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        // ============================================================
        // ACT
        // ============================================================

        _ibkr.SimulateOrderStatusChanged(ibkrOrderId, ibkrStatus, filled: 0, remaining: 1, avgFillPrice: 0.0);
        await Task.Delay(200);

        // ============================================================
        // ASSERT
        // ============================================================

        OrderRecord? order = await _orderRepo.GetOrderAsync(orderId, CancellationToken.None);
        Assert.NotNull(order);
        Assert.Equal(expectedStatus, order.Status);
    }
}
