namespace SharedKernel.Domain;

/// <summary>
/// Request to place an order. Immutable.
/// </summary>
public sealed record OrderRequest
{
    /// <summary>
    /// Campaign ID that owns this order.
    /// </summary>
    public string CampaignId { get; init; } = string.Empty;

    /// <summary>
    /// Position ID if this order modifies an existing position (null for new positions).
    /// </summary>
    public string? PositionId { get; init; }

    /// <summary>
    /// Underlying symbol (e.g., "SPX", "SPY").
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Full option contract symbol (OCC format).
    /// </summary>
    public string ContractSymbol { get; init; } = string.Empty;

    /// <summary>
    /// Order side (Buy or Sell).
    /// </summary>
    public OrderSide Side { get; init; }

    /// <summary>
    /// Order type (Market, Limit, etc.).
    /// </summary>
    public OrderType Type { get; init; }

    /// <summary>
    /// Number of contracts to trade (must be positive).
    /// </summary>
    public int Quantity { get; init; }

    /// <summary>
    /// Limit price for Limit orders (required for Limit and StopLimit orders).
    /// </summary>
    public decimal? LimitPrice { get; init; }

    /// <summary>
    /// Stop price for Stop orders (required for Stop and StopLimit orders).
    /// </summary>
    public decimal? StopPrice { get; init; }

    /// <summary>
    /// Time-in-force ("DAY" or "GTC"). Default: DAY.
    /// </summary>
    public string TimeInForce { get; init; } = "DAY";

    /// <summary>
    /// Strategy name that created this order.
    /// </summary>
    public string StrategyName { get; init; } = string.Empty;

    /// <summary>
    /// Optional metadata (JSON).
    /// </summary>
    public string? MetadataJson { get; init; }

    // Additional fields for IBKR integration (optional, for IbkrClient compatibility)
    /// <summary>
    /// Security type for IBKR ("OPT", "STK", "IND", etc.). Derived from ContractSymbol if not set.
    /// </summary>
    public string? SecurityType { get; init; }

    /// <summary>
    /// Exchange for IBKR ("SMART", "CBOE", etc.). Defaults to "SMART" if not set.
    /// </summary>
    public string? Exchange { get; init; }

    /// <summary>
    /// Option strike price (required for options). Parsed from ContractSymbol if not set.
    /// </summary>
    public decimal? Strike { get; init; }

    /// <summary>
    /// Option expiry date in YYYYMMDD format (required for options). Parsed from ContractSymbol if not set.
    /// </summary>
    public string? Expiry { get; init; }

    /// <summary>
    /// Option right ("C" for call, "P" for put). Parsed from ContractSymbol if not set.
    /// </summary>
    public string? OptionRight { get; init; }

    /// <summary>
    /// IBKR account number. Optional, uses default account if not set.
    /// </summary>
    public string? Account { get; init; }

    /// <summary>
    /// Validates the order request. Returns error message if invalid, null if valid.
    /// </summary>
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(CampaignId))
        {
            return "CampaignId is required";
        }

        if (string.IsNullOrWhiteSpace(Symbol))
        {
            return "Symbol is required";
        }

        if (string.IsNullOrWhiteSpace(ContractSymbol))
        {
            return "ContractSymbol is required";
        }

        if (Quantity <= 0)
        {
            return $"Quantity must be positive, got {Quantity}";
        }

        if (Type is OrderType.Limit or OrderType.StopLimit)
        {
            if (LimitPrice is null or <= 0)
            {
                return $"LimitPrice is required and must be positive for {Type} orders";
            }
        }

        if (Type is OrderType.Stop or OrderType.StopLimit)
        {
            if (StopPrice is null or <= 0)
            {
                return $"StopPrice is required and must be positive for {Type} orders";
            }
        }

        if (TimeInForce is not ("DAY" or "GTC"))
        {
            return $"TimeInForce must be DAY or GTC, got {TimeInForce}";
        }

        if (string.IsNullOrWhiteSpace(StrategyName))
        {
            return "StrategyName is required";
        }

        return null; // Valid
    }
}
