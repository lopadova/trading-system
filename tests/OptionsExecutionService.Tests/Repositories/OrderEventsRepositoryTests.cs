using Microsoft.Extensions.Logging;
using OptionsExecutionService.Repositories;
using SharedKernel.Data;
using SharedKernel.Domain;
using Xunit;

namespace OptionsExecutionService.Tests.Repositories;

/// <summary>
/// Integration tests for OrderEventsRepository.
/// Validates IBKR callback persistence for crash recovery and idempotency.
/// </summary>
public sealed class OrderEventsRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly IDbConnectionFactory _db;
    private readonly OrderEventsRepository _repo;

    public OrderEventsRepositoryTests()
    {
        // Create unique test database
        _dbPath = $"test-order-events-{Guid.NewGuid()}.db";
        _db = new SqliteConnectionFactory(_dbPath);

        // Run migration to create order_events table
        MigrationRunner runner = new(
            _db,
            new LoggerFactory().CreateLogger<MigrationRunner>());

        runner.RunAsync(
            new[] { new OptionsExecutionService.Migrations.AddOrderEvents005() },
            CancellationToken.None).GetAwaiter().GetResult();

        // Create repository
        _repo = new OrderEventsRepository(
            _db,
            new LoggerFactory().CreateLogger<OrderEventsRepository>());
    }

    public void Dispose()
    {
        // Force SQLite to close all connections
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // Small delay to ensure all file handles are released
        System.Threading.Thread.Sleep(50);

        // Clean up test database
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch (IOException)
            {
                // If still locked, just leave it - test cleanup is non-critical
            }
        }
    }

    [Fact(DisplayName = "TEST-PHASE1-01: InsertOrderStatusAsync creates event record")]
    public async Task InsertOrderStatusAsync_CreatesEventRecord()
    {
        // Arrange
        string orderId = "test-order-001";
        int ibkrOrderId = 12345;
        OrderStatus status = OrderStatus.Submitted;
        int filled = 0;
        int remaining = 10;
        decimal avgFillPrice = 100.50m;

        // Act
        await _repo.InsertOrderStatusAsync(
            orderId,
            ibkrOrderId,
            status,
            filled,
            remaining,
            lastFillPrice: null,
            avgFillPrice: avgFillPrice,
            permId: null,
            parentId: null,
            lastTradeDate: null,
            whyHeld: null,
            mktCapPrice: null,
            CancellationToken.None);

        // Assert: Retrieve the event
        OrderEventRecord? latestEvent = await _repo.GetLatestOrderEventAsync(orderId, CancellationToken.None);

        Assert.NotNull(latestEvent);
        Assert.Equal(orderId, latestEvent.OrderId);
        Assert.Equal(ibkrOrderId, latestEvent.IbkrOrderId);
        Assert.Equal(status.ToString(), latestEvent.Status);
        Assert.Equal(filled, latestEvent.Filled);
        Assert.Equal(remaining, latestEvent.Remaining);
        Assert.Equal(avgFillPrice, latestEvent.AvgFillPrice);
        Assert.True(latestEvent.EventId > 0, "EventId should be auto-generated");
        Assert.False(string.IsNullOrEmpty(latestEvent.EventTimestamp), "EventTimestamp should be set");
    }

    [Fact(DisplayName = "TEST-PHASE1-02: GetLatestOrderEventAsync returns latest event by event_id")]
    public async Task GetLatestOrderEventAsync_ReturnsLatestByEventId()
    {
        // Arrange: Insert multiple events for same order
        string orderId = "test-order-002";

        await _repo.InsertOrderStatusAsync(
            orderId, 12345, OrderStatus.Submitted, 0, 10,
            null, null, null, null, null, null, null, CancellationToken.None);

        await _repo.InsertOrderStatusAsync(
            orderId, 12345, OrderStatus.Active, 0, 10,
            null, null, null, null, null, null, null, CancellationToken.None);

        await _repo.InsertOrderStatusAsync(
            orderId, 12345, OrderStatus.PartiallyFilled, 5, 5,
            null, 100.50m, null, null, null, null, null, CancellationToken.None);

        // Act: Get latest event
        OrderEventRecord? latestEvent = await _repo.GetLatestOrderEventAsync(orderId, CancellationToken.None);

        // Assert: Should be the PartiallyFilled event
        Assert.NotNull(latestEvent);
        Assert.Equal(OrderStatus.PartiallyFilled.ToString(), latestEvent.Status);
        Assert.Equal(5, latestEvent.Filled);
        Assert.Equal(5, latestEvent.Remaining);
        Assert.Equal(100.50m, latestEvent.AvgFillPrice);
    }

    [Fact(DisplayName = "TEST-PHASE1-03: GetOrderEventsAsync returns all events ordered by event_id")]
    public async Task GetOrderEventsAsync_ReturnsAllEventsOrderedByEventId()
    {
        // Arrange: Insert multiple events
        string orderId = "test-order-003";

        await _repo.InsertOrderStatusAsync(
            orderId, 12345, OrderStatus.Submitted, 0, 10,
            null, null, null, null, null, null, null, CancellationToken.None);

        await _repo.InsertOrderStatusAsync(
            orderId, 12345, OrderStatus.Active, 0, 10,
            null, null, null, null, null, null, null, CancellationToken.None);

        await _repo.InsertOrderStatusAsync(
            orderId, 12345, OrderStatus.PartiallyFilled, 3, 7,
            null, 100.50m, null, null, null, null, null, CancellationToken.None);

        await _repo.InsertOrderStatusAsync(
            orderId, 12345, OrderStatus.Filled, 10, 0,
            null, 100.75m, null, null, null, null, null, CancellationToken.None);

        // Act: Get all events
        IReadOnlyList<OrderEventRecord> events = await _repo.GetOrderEventsAsync(orderId, CancellationToken.None);

        // Assert: Should have 4 events in chronological order
        Assert.Equal(4, events.Count);

        // Verify chronological order by event_id (monotonic)
        Assert.Equal(OrderStatus.Submitted.ToString(), events[0].Status);
        Assert.Equal(OrderStatus.Active.ToString(), events[1].Status);
        Assert.Equal(OrderStatus.PartiallyFilled.ToString(), events[2].Status);
        Assert.Equal(OrderStatus.Filled.ToString(), events[3].Status);

        // Verify event_id is monotonically increasing
        Assert.True(events[0].EventId < events[1].EventId);
        Assert.True(events[1].EventId < events[2].EventId);
        Assert.True(events[2].EventId < events[3].EventId);
    }

    [Fact(DisplayName = "TEST-PHASE1-04: GetLatestOrderEventAsync returns null for non-existent order")]
    public async Task GetLatestOrderEventAsync_ReturnsNullForNonExistentOrder()
    {
        // Arrange
        string orderId = "non-existent-order";

        // Act
        OrderEventRecord? result = await _repo.GetLatestOrderEventAsync(orderId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact(DisplayName = "TEST-PHASE1-05: GetOrderEventsAsync returns empty list for non-existent order")]
    public async Task GetOrderEventsAsync_ReturnsEmptyListForNonExistentOrder()
    {
        // Arrange
        string orderId = "non-existent-order";

        // Act
        IReadOnlyList<OrderEventRecord> events = await _repo.GetOrderEventsAsync(orderId, CancellationToken.None);

        // Assert
        Assert.Empty(events);
    }

    [Fact(DisplayName = "TEST-PHASE1-06: InsertOrderStatusAsync with null IBKR ID (pre-submission)")]
    public async Task InsertOrderStatusAsync_WithNullIbkrId_StoresCorrectly()
    {
        // Arrange: Order created locally but not yet submitted to IBKR
        string orderId = "test-order-004";
        int? ibkrOrderId = null;
        OrderStatus status = OrderStatus.PendingSubmit;

        // Act
        await _repo.InsertOrderStatusAsync(
            orderId, ibkrOrderId, status, 0, 10,
            null, null, null, null, null, null, null, CancellationToken.None);

        // Assert
        OrderEventRecord? latestEvent = await _repo.GetLatestOrderEventAsync(orderId, CancellationToken.None);

        Assert.NotNull(latestEvent);
        Assert.Null(latestEvent.IbkrOrderId);
        Assert.Equal(OrderStatus.PendingSubmit.ToString(), latestEvent.Status);
    }

    [Fact(DisplayName = "TEST-PHASE1-07: InsertOrderStatusAsync validates orderId")]
    public async Task InsertOrderStatusAsync_EmptyOrderId_ThrowsArgumentException()
    {
        // Arrange
        string emptyOrderId = "";

        // Act & Assert
        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repo.InsertOrderStatusAsync(
                emptyOrderId, 12345, OrderStatus.Submitted, 0, 10,
                null, null, null, null, null, null, null, CancellationToken.None));

        Assert.Contains("OrderId", ex.Message);
    }

    [Fact(DisplayName = "TEST-PHASE1-08: GetLatestOrderEventAsync validates orderId")]
    public async Task GetLatestOrderEventAsync_EmptyOrderId_ThrowsArgumentException()
    {
        // Arrange
        string emptyOrderId = "";

        // Act & Assert
        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repo.GetLatestOrderEventAsync(emptyOrderId, CancellationToken.None));

        Assert.Contains("OrderId", ex.Message);
    }

    [Fact(DisplayName = "TEST-PHASE1-09: GetOrderEventsAsync validates orderId")]
    public async Task GetOrderEventsAsync_EmptyOrderId_ThrowsArgumentException()
    {
        // Arrange
        string emptyOrderId = "";

        // Act & Assert
        ArgumentException ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repo.GetOrderEventsAsync(emptyOrderId, CancellationToken.None));

        Assert.Contains("OrderId", ex.Message);
    }
}
