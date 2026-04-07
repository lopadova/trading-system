namespace SharedKernel.Domain;

/// <summary>
/// Result of order placement operation. Immutable.
/// </summary>
public sealed record OrderResult
{
    /// <summary>
    /// True if order was successfully submitted to IBKR.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Internal order ID (GUID) for tracking.
    /// </summary>
    public string? OrderId { get; init; }

    /// <summary>
    /// IBKR order ID (assigned by broker after submission).
    /// </summary>
    public int? IbkrOrderId { get; init; }

    /// <summary>
    /// Current order status.
    /// </summary>
    public OrderStatus Status { get; init; }

    /// <summary>
    /// Error message if submission failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Timestamp when order was processed.
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static OrderResult Ok(string orderId, int ibkrOrderId, OrderStatus status)
    {
        return new OrderResult
        {
            Success = true,
            OrderId = orderId,
            IbkrOrderId = ibkrOrderId,
            Status = status,
            Error = null
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static OrderResult Fail(OrderStatus status, string error)
    {
        return new OrderResult
        {
            Success = false,
            OrderId = null,
            IbkrOrderId = null,
            Status = status,
            Error = error
        };
    }
}
