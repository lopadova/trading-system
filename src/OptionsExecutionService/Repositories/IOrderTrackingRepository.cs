using SharedKernel.Domain;
using OptionsExecutionService.Orders;

namespace OptionsExecutionService.Repositories;

/// <summary>
/// Repository for tracking orders and executions.
/// </summary>
public interface IOrderTrackingRepository
{
    /// <summary>
    /// Logs an order submission attempt to the database.
    /// This is called BEFORE submitting to IBKR to create an audit trail.
    /// </summary>
    /// <param name="orderId">Internal order ID (GUID)</param>
    /// <param name="ibkrOrderId">IBKR order ID (if assigned, null if not submitted yet)</param>
    /// <param name="request">Order request details</param>
    /// <param name="status">Current order status</param>
    /// <param name="ct">Cancellation token</param>
    Task LogOrderAsync(
        string orderId,
        int? ibkrOrderId,
        OrderRequest request,
        OrderStatus status,
        CancellationToken ct = default);

    /// <summary>
    /// Updates order status when status changes (submitted, filled, cancelled, etc.).
    /// </summary>
    /// <param name="orderId">Internal order ID</param>
    /// <param name="status">New status</param>
    /// <param name="filledQuantity">Number of contracts filled so far</param>
    /// <param name="avgFillPrice">Average fill price</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateOrderStatusAsync(
        string orderId,
        OrderStatus status,
        int filledQuantity,
        decimal avgFillPrice,
        CancellationToken ct = default);

    /// <summary>
    /// Logs an execution (fill) to execution_log table.
    /// </summary>
    /// <param name="executionId">IBKR execution ID</param>
    /// <param name="orderId">Internal order ID</param>
    /// <param name="ibkrOrderId">IBKR order ID</param>
    /// <param name="request">Original order request</param>
    /// <param name="fillQuantity">Number of contracts filled in this execution</param>
    /// <param name="fillPrice">Fill price for this execution</param>
    /// <param name="commission">Commission charged</param>
    /// <param name="executedAt">Execution timestamp from IBKR</param>
    /// <param name="ct">Cancellation token</param>
    Task LogExecutionAsync(
        string executionId,
        string orderId,
        int ibkrOrderId,
        OrderRequest request,
        int fillQuantity,
        decimal fillPrice,
        decimal commission,
        DateTime executedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Gets order by internal order ID.
    /// </summary>
    /// <param name="orderId">Internal order ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Order record or null if not found</returns>
    Task<OrderRecord?> GetOrderAsync(string orderId, CancellationToken ct = default);

    /// <summary>
    /// Gets order by IBKR order ID.
    /// </summary>
    /// <param name="ibkrOrderId">IBKR order ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Order record or null if not found</returns>
    Task<OrderRecord?> GetOrderByIbkrIdAsync(int ibkrOrderId, CancellationToken ct = default);

    /// <summary>
    /// Gets failed orders within a time window (for circuit breaker).
    /// </summary>
    /// <param name="windowMinutes">Time window in minutes</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of failed orders in the window</returns>
    Task<List<OrderRecord>> GetFailedOrdersInWindowAsync(int windowMinutes, CancellationToken ct = default);

    /// <summary>
    /// Gets order statistics for monitoring.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Order statistics</returns>
    Task<OrderStats> GetOrderStatsAsync(CancellationToken ct = default);
}

/// <summary>
/// Order record from database.
/// </summary>
public sealed record OrderRecord
{
    public string OrderId { get; init; } = string.Empty;
    public int? IbkrOrderId { get; init; }
    public string CampaignId { get; init; } = string.Empty;
    public string? PositionId { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public string ContractSymbol { get; init; } = string.Empty;
    public OrderSide Side { get; init; }
    public OrderType Type { get; init; }
    public int Quantity { get; init; }
    public decimal? LimitPrice { get; init; }
    public decimal? StopPrice { get; init; }
    public string TimeInForce { get; init; } = string.Empty;
    public OrderStatus Status { get; init; }
    public int FilledQuantity { get; init; }
    public decimal? AvgFillPrice { get; init; }
    public string StrategyName { get; init; } = string.Empty;
    public string? MetadataJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
