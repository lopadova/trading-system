namespace SharedKernel.Domain;

/// <summary>
/// Safety configuration for order placement. Immutable.
/// These limits prevent catastrophic losses from strategy bugs or market anomalies.
/// </summary>
public sealed record OrderSafetyConfig
{
    /// <summary>
    /// Trading mode. MUST be Paper for safety.
    /// </summary>
    public TradingMode TradingMode { get; init; } = TradingMode.Paper;

    /// <summary>
    /// Maximum position size (number of contracts) per order. Default: 10
    /// </summary>
    public int MaxPositionSize { get; init; } = 10;

    /// <summary>
    /// Maximum total position value (contracts * price) per order in USD. Default: 50000
    /// </summary>
    public decimal MaxPositionValueUsd { get; init; } = 50000m;

    /// <summary>
    /// Minimum account balance required before placing orders (USD). Default: 10000
    /// </summary>
    public decimal MinAccountBalanceUsd { get; init; } = 10000m;

    /// <summary>
    /// Maximum percentage of account balance per single order. Default: 0.2 (20%)
    /// </summary>
    public decimal MaxPositionPctOfAccount { get; init; } = 0.2m;

    /// <summary>
    /// Maximum number of failed orders before circuit breaker trips. Default: 5
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; init; } = 5;

    /// <summary>
    /// Time window for counting failures (in minutes). Default: 60 (1 hour)
    /// </summary>
    public int CircuitBreakerWindowMinutes { get; init; } = 60;

    /// <summary>
    /// Circuit breaker cooldown period (in minutes) after tripping. Default: 30
    /// </summary>
    public int CircuitBreakerCooldownMinutes { get; init; } = 30;

    /// <summary>
    /// Whether to allow partial fills. Default: true
    /// </summary>
    public bool AllowPartialFills { get; init; } = true;

    /// <summary>
    /// Validates the configuration. Throws ArgumentException if invalid.
    /// </summary>
    public void Validate()
    {
        if (TradingMode != TradingMode.Paper)
        {
            throw new ArgumentException(
                "Only Paper trading mode is allowed. Live trading requires explicit authorization.",
                nameof(TradingMode));
        }

        if (MaxPositionSize <= 0)
        {
            throw new ArgumentException(
                $"MaxPositionSize must be positive, got {MaxPositionSize}",
                nameof(MaxPositionSize));
        }

        if (MaxPositionValueUsd <= 0)
        {
            throw new ArgumentException(
                $"MaxPositionValueUsd must be positive, got {MaxPositionValueUsd}",
                nameof(MaxPositionValueUsd));
        }

        if (MinAccountBalanceUsd < 0)
        {
            throw new ArgumentException(
                $"MinAccountBalanceUsd must be non-negative, got {MinAccountBalanceUsd}",
                nameof(MinAccountBalanceUsd));
        }

        if (MaxPositionPctOfAccount <= 0 || MaxPositionPctOfAccount > 1)
        {
            throw new ArgumentException(
                $"MaxPositionPctOfAccount must be between 0 and 1, got {MaxPositionPctOfAccount}",
                nameof(MaxPositionPctOfAccount));
        }

        if (CircuitBreakerFailureThreshold <= 0)
        {
            throw new ArgumentException(
                $"CircuitBreakerFailureThreshold must be positive, got {CircuitBreakerFailureThreshold}",
                nameof(CircuitBreakerFailureThreshold));
        }

        if (CircuitBreakerWindowMinutes <= 0)
        {
            throw new ArgumentException(
                $"CircuitBreakerWindowMinutes must be positive, got {CircuitBreakerWindowMinutes}",
                nameof(CircuitBreakerWindowMinutes));
        }

        if (CircuitBreakerCooldownMinutes <= 0)
        {
            throw new ArgumentException(
                $"CircuitBreakerCooldownMinutes must be positive, got {CircuitBreakerCooldownMinutes}",
                nameof(CircuitBreakerCooldownMinutes));
        }
    }
}
