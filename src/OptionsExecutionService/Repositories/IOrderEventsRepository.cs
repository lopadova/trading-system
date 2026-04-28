using SharedKernel.Domain;

namespace OptionsExecutionService.Repositories;

/// <summary>
/// Repository for persisting IBKR order callback events to the database.
/// Provides immutable audit trail for crash recovery and idempotency.
/// </summary>
public interface IOrderEventsRepository
{
    /// <summary>
    /// Inserts an order status callback event into the database.
    /// This method creates an append-only audit trail. Each callback creates a new immutable event row.
    /// Idempotency (deduplication) is the caller's responsibility via upstream IBKR callback filtering.
    /// </summary>
    /// <param name="orderId">Internal order ID (links to order_tracking).</param>
    /// <param name="ibkrOrderId">IBKR order ID (null until submitted).</param>
    /// <param name="status">Order status from OrderStatus enum.</param>
    /// <param name="filled">Filled quantity.</param>
    /// <param name="remaining">Remaining quantity.</param>
    /// <param name="lastFillPrice">Last fill price (from execDetails).</param>
    /// <param name="avgFillPrice">Average fill price (from orderStatus).</param>
    /// <param name="permId">IBKR permanent order ID.</param>
    /// <param name="parentId">IBKR parent order ID (for child orders).</param>
    /// <param name="lastTradeDate">Last trade date (from orderStatus).</param>
    /// <param name="whyHeld">Reason order held (from orderStatus).</param>
    /// <param name="mktCapPrice">Market cap price (from orderStatus).</param>
    /// <param name="ct">Cancellation token.</param>
    Task InsertOrderStatusAsync(
        string orderId,
        int? ibkrOrderId,
        OrderStatus status,
        int filled,
        int remaining,
        decimal? lastFillPrice,
        decimal? avgFillPrice,
        int? permId,
        int? parentId,
        string? lastTradeDate,
        string? whyHeld,
        decimal? mktCapPrice,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the latest event for a specific order.
    /// Returns null if no events exist for the order.
    /// </summary>
    /// <param name="orderId">Internal order ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Latest OrderEventRecord or null.</returns>
    Task<OrderEventRecord?> GetLatestOrderEventAsync(string orderId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all events for a specific order, ordered by event_id (chronological).
    /// Returns empty list if no events exist.
    /// </summary>
    /// <param name="orderId">Internal order ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of OrderEventRecords ordered by event_id.</returns>
    Task<IReadOnlyList<OrderEventRecord>> GetOrderEventsAsync(string orderId, CancellationToken ct = default);
}
