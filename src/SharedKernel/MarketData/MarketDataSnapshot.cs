namespace SharedKernel.MarketData;

/// <summary>
/// Immutable snapshot of market data for a symbol at a specific time.
/// Thread-safe value type for concurrent access.
/// </summary>
public sealed record MarketDataSnapshot
{
    /// <summary>
    /// Symbol (e.g., "SPX", "VIX3M", or option contract symbol).
    /// </summary>
    public required string Symbol { get; init; }

    /// <summary>
    /// Security type: "IND" (index), "OPT" (option), "STK" (stock).
    /// </summary>
    public required string SecType { get; init; }

    /// <summary>
    /// Last traded price. Null if not available.
    /// </summary>
    public double? LastPrice { get; init; }

    /// <summary>
    /// Current bid price. Null if not available.
    /// </summary>
    public double? BidPrice { get; init; }

    /// <summary>
    /// Current ask price. Null if not available.
    /// </summary>
    public double? AskPrice { get; init; }

    /// <summary>
    /// Bid size (number of contracts/shares at bid). Null if not available.
    /// </summary>
    public decimal? BidSize { get; init; }

    /// <summary>
    /// Ask size (number of contracts/shares at ask). Null if not available.
    /// </summary>
    public decimal? AskSize { get; init; }

    /// <summary>
    /// Implied volatility (annualized, e.g., 0.20 = 20%). Null if not available.
    /// Only applicable for options (SecType="OPT").
    /// </summary>
    public double? ImpliedVolatility { get; init; }

    /// <summary>
    /// Option delta. Null if not available or not an option.
    /// </summary>
    public double? Delta { get; init; }

    /// <summary>
    /// Option gamma. Null if not available or not an option.
    /// </summary>
    public double? Gamma { get; init; }

    /// <summary>
    /// Option theta (per day). Null if not available or not an option.
    /// </summary>
    public double? Theta { get; init; }

    /// <summary>
    /// Option vega. Null if not available or not an option.
    /// </summary>
    public double? Vega { get; init; }

    /// <summary>
    /// Underlying price (for options). Null if not available.
    /// </summary>
    public double? UnderlyingPrice { get; init; }

    /// <summary>
    /// Days to expiration (for options). Null if not an option.
    /// </summary>
    public int? DaysToExpiration { get; init; }

    /// <summary>
    /// Expiration date (for options). Null if not an option.
    /// </summary>
    public DateTime? ExpirationDate { get; init; }

    /// <summary>
    /// Bid/Ask spread in dollars. Null if bid or ask not available.
    /// </summary>
    public double? Spread => (BidPrice.HasValue && AskPrice.HasValue)
        ? Math.Abs(AskPrice.Value - BidPrice.Value)
        : null;

    /// <summary>
    /// Mid price (average of bid and ask). Null if either is missing.
    /// </summary>
    public double? MidPrice => (BidPrice.HasValue && AskPrice.HasValue)
        ? (BidPrice.Value + AskPrice.Value) / 2.0
        : null;

    /// <summary>
    /// Timestamp when this snapshot was captured (UTC).
    /// </summary>
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Request ID used to retrieve this data from IBKR.
    /// </summary>
    public int RequestId { get; init; }

    /// <summary>
    /// Creates an empty snapshot (no data available).
    /// </summary>
    public static MarketDataSnapshot Empty(string symbol, string secType, int requestId) => new()
    {
        Symbol = symbol,
        SecType = secType,
        RequestId = requestId,
        TimestampUtc = DateTime.UtcNow
    };

    /// <summary>
    /// Checks if this snapshot has valid price data.
    /// </summary>
    public bool HasPriceData => LastPrice.HasValue || (BidPrice.HasValue && AskPrice.HasValue);

    /// <summary>
    /// Checks if this snapshot has option Greeks data.
    /// </summary>
    public bool HasGreeks => Delta.HasValue || Gamma.HasValue || Theta.HasValue || Vega.HasValue;

    /// <summary>
    /// Checks if this snapshot is stale (older than threshold).
    /// </summary>
    /// <param name="maxAgeSeconds">Maximum age in seconds before considered stale</param>
    public bool IsStale(int maxAgeSeconds = 60) =>
        (DateTime.UtcNow - TimestampUtc).TotalSeconds > maxAgeSeconds;
}
