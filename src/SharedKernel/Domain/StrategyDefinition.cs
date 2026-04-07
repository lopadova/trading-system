namespace SharedKernel.Domain;

/// <summary>
/// Root entity for a trading strategy definition loaded from JSON file.
/// Immutable record representing a complete strategy configuration.
/// </summary>
public sealed record StrategyDefinition
{
    /// <summary>
    /// Unique name for this strategy. Used for identification and logging.
    /// </summary>
    public required string StrategyName { get; init; }

    /// <summary>
    /// Human-readable description of the strategy purpose and behavior.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Trading mode: Paper (default) or Live. Must be validated against system config.
    /// </summary>
    public required TradingMode TradingMode { get; init; }

    /// <summary>
    /// Underlying asset configuration (symbol, exchange, currency).
    /// </summary>
    public required UnderlyingConfig Underlying { get; init; }

    /// <summary>
    /// Rules for when to enter a trade.
    /// </summary>
    public required EntryRules EntryRules { get; init; }

    /// <summary>
    /// Position structure and sizing configuration.
    /// </summary>
    public required PositionConfig Position { get; init; }

    /// <summary>
    /// Rules for when to exit a trade (profit target, stop loss, time-based).
    /// </summary>
    public required ExitRules ExitRules { get; init; }

    /// <summary>
    /// Risk management parameters and limits.
    /// </summary>
    public required RiskManagement RiskManagement { get; init; }

    /// <summary>
    /// Optional: file path from which this strategy was loaded.
    /// </summary>
    public string? SourceFilePath { get; init; }
}

/// <summary>
/// Configuration for the underlying asset.
/// </summary>
public sealed record UnderlyingConfig
{
    public required string Symbol { get; init; }
    public required string Exchange { get; init; }
    public required string Currency { get; init; }
}

/// <summary>
/// Entry rules defining when to open a position.
/// </summary>
public sealed record EntryRules
{
    public required MarketConditions MarketConditions { get; init; }
    public required TimingRules Timing { get; init; }
}

/// <summary>
/// Market condition filters for entry.
/// </summary>
public sealed record MarketConditions
{
    public required int MinDaysToExpiration { get; init; }
    public required int MaxDaysToExpiration { get; init; }
    public required decimal IvRankMin { get; init; }
    public required decimal IvRankMax { get; init; }
}

/// <summary>
/// Time-based entry filters.
/// </summary>
public sealed record TimingRules
{
    public required TimeOnly EntryTimeStart { get; init; }
    public required TimeOnly EntryTimeEnd { get; init; }
    public required DayOfWeek[] DaysOfWeek { get; init; }
}

/// <summary>
/// Position configuration including type and leg definitions.
/// </summary>
public sealed record PositionConfig
{
    /// <summary>
    /// Strategy type: BullPutSpread, BearCallSpread, IronCondor, etc.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Individual option legs that make up the position.
    /// </summary>
    public required OptionLeg[] Legs { get; init; }

    /// <summary>
    /// Maximum number of concurrent positions allowed.
    /// </summary>
    public required int MaxPositions { get; init; }

    /// <summary>
    /// Capital allocated per position in USD.
    /// </summary>
    public required decimal CapitalPerPosition { get; init; }
}

/// <summary>
/// Single option leg within a multi-leg strategy.
/// </summary>
public sealed record OptionLeg
{
    /// <summary>
    /// Order action: BUY or SELL
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Option right: PUT or CALL
    /// </summary>
    public required string Right { get; init; }

    /// <summary>
    /// How to select the strike: DELTA, OFFSET, ABSOLUTE
    /// </summary>
    public required string StrikeSelectionMethod { get; init; }

    /// <summary>
    /// Value for strike selection. Meaning depends on StrikeSelectionMethod:
    /// - DELTA: delta value (e.g., -0.30 for 30 delta put)
    /// - OFFSET: points offset from reference (e.g., -5 means 5 points lower)
    /// - ABSOLUTE: exact strike price
    /// </summary>
    public decimal? StrikeValue { get; init; }

    /// <summary>
    /// Strike offset in points (used with OFFSET method).
    /// </summary>
    public decimal? StrikeOffset { get; init; }

    /// <summary>
    /// Number of contracts for this leg.
    /// </summary>
    public required int Quantity { get; init; }
}

/// <summary>
/// Exit rules defining when to close a position.
/// </summary>
public sealed record ExitRules
{
    /// <summary>
    /// Profit target as multiplier of initial credit (e.g., 0.50 = exit at 50% profit).
    /// </summary>
    public required decimal ProfitTarget { get; init; }

    /// <summary>
    /// Stop loss as multiplier of initial credit (e.g., 2.00 = exit if loss reaches 200% of credit).
    /// </summary>
    public required decimal StopLoss { get; init; }

    /// <summary>
    /// Maximum days to hold position before forced exit.
    /// </summary>
    public required int MaxDaysInTrade { get; init; }

    /// <summary>
    /// Time of day to force exit (e.g., before market close).
    /// </summary>
    public required TimeOnly ExitTimeOfDay { get; init; }
}

/// <summary>
/// Risk management parameters and system-wide limits.
/// </summary>
public sealed record RiskManagement
{
    /// <summary>
    /// Maximum total capital at risk across all positions in USD.
    /// </summary>
    public required decimal MaxTotalCapitalAtRisk { get; init; }

    /// <summary>
    /// Maximum portfolio drawdown percentage (e.g., 10.0 = 10%).
    /// </summary>
    public required decimal MaxDrawdownPercent { get; init; }

    /// <summary>
    /// Maximum allowed loss in a single day in USD.
    /// </summary>
    public required decimal MaxDailyLoss { get; init; }
}
