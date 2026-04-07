using SharedKernel.Domain;

namespace OptionsExecutionService.Orders;

/// <summary>
/// Service for placing and tracking orders with safety validations and circuit breaker.
/// </summary>
public interface IOrderPlacer
{
    /// <summary>
    /// Places an order with full safety checks.
    /// This method validates trading mode, position sizes, account balance,
    /// and circuit breaker status before submitting to IBKR.
    /// </summary>
    /// <param name="request">Order request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Order result with success status and order ID</returns>
    Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cancels an order by internal order ID.
    /// </summary>
    /// <param name="orderId">Internal order ID (GUID)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if cancellation was submitted successfully</returns>
    Task<bool> CancelOrderAsync(string orderId, CancellationToken ct = default);

    /// <summary>
    /// Gets the current circuit breaker status.
    /// </summary>
    /// <returns>True if circuit breaker is open (orders blocked)</returns>
    bool IsCircuitBreakerOpen();

    /// <summary>
    /// Manually resets the circuit breaker (admin operation).
    /// </summary>
    void ResetCircuitBreaker();

    /// <summary>
    /// Gets current order statistics (for monitoring).
    /// </summary>
    /// <returns>Order stats (total, filled, failed, etc.)</returns>
    Task<OrderStats> GetOrderStatsAsync(CancellationToken ct = default);

    // Legacy methods for Campaign Manager (pre-existing code)
    Task<IReadOnlyList<string>> PlaceEntryOrdersAsync(string campaignId, SharedKernel.Domain.StrategyDefinition strategy, CancellationToken ct = default);
    Task<decimal> GetUnrealizedPnLAsync(string campaignId, CancellationToken ct = default);
    Task<decimal> ClosePositionsAsync(string campaignId, CancellationToken ct = default);
}

/// <summary>
/// Order statistics for monitoring.
/// </summary>
public sealed record OrderStats
{
    public int TotalOrders { get; init; }
    public int FilledOrders { get; init; }
    public int FailedOrders { get; init; }
    public int ActiveOrders { get; init; }
    public int CancelledOrders { get; init; }
    public bool CircuitBreakerOpen { get; init; }
    public int FailuresInWindow { get; init; }
    public DateTime? CircuitBreakerTrippedAt { get; init; }
}
