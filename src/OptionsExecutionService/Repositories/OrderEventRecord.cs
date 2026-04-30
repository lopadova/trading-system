namespace OptionsExecutionService.Repositories;

/// <summary>
/// Record representing a single IBKR order callback event in the database.
/// Provides an immutable audit trail for crash recovery and duplicate prevention.
/// </summary>
public sealed record OrderEventRecord
{
    /// <summary>
    /// Auto-incremented event ID (monotonic ordering, prevents timestamp flakes).
    /// </summary>
    public long EventId { get; init; }

    /// <summary>
    /// Internal order ID (links to order_tracking table).
    /// </summary>
    public required string OrderId { get; init; }

    /// <summary>
    /// IBKR order ID (null until submitted to IBKR).
    /// </summary>
    public int? IbkrOrderId { get; init; }

    /// <summary>
    /// Order status from OrderStatus enum (stored as string).
    /// NULL for non-status events (execution/error events).
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// Filled quantity.
    /// </summary>
    public int Filled { get; init; }

    /// <summary>
    /// Remaining quantity.
    /// </summary>
    public int Remaining { get; init; }

    /// <summary>
    /// Last fill price (from execDetails callback).
    /// </summary>
    public decimal? LastFillPrice { get; init; }

    /// <summary>
    /// Average fill price (from orderStatus callback).
    /// </summary>
    public decimal? AvgFillPrice { get; init; }

    /// <summary>
    /// IBKR permanent order ID.
    /// </summary>
    public int? PermId { get; init; }

    /// <summary>
    /// IBKR parent order ID (for child orders in bracket/combo orders).
    /// </summary>
    public int? ParentId { get; init; }

    /// <summary>
    /// Last trade date (from orderStatus callback).
    /// </summary>
    public string? LastTradeDate { get; init; }

    /// <summary>
    /// Reason order held (from orderStatus callback).
    /// </summary>
    public string? WhyHeld { get; init; }

    /// <summary>
    /// Market cap price (from orderStatus callback).
    /// </summary>
    public decimal? MktCapPrice { get; init; }

    /// <summary>
    /// Event timestamp (ISO8601 UTC).
    /// </summary>
    public required string EventTimestamp { get; init; }
}
