namespace SharedKernel.Options;

/// <summary>
/// Greeks values for an option position.
/// All values are per-contract unless otherwise specified.
/// </summary>
public sealed record GreeksData
{
    /// <summary>
    /// Delta: rate of change of option price with respect to underlying price.
    /// Range: [0, 1] for calls, [-1, 0] for puts.
    /// </summary>
    public double Delta { get; init; }

    /// <summary>
    /// Gamma: rate of change of delta with respect to underlying price.
    /// Always positive for both calls and puts.
    /// </summary>
    public double Gamma { get; init; }

    /// <summary>
    /// Theta: rate of change of option price with respect to time (per day).
    /// Usually negative (time decay). Expressed in dollars per day.
    /// </summary>
    public double Theta { get; init; }

    /// <summary>
    /// Vega: rate of change of option price with respect to volatility.
    /// Expressed in dollars per 1% change in IV. Always positive.
    /// </summary>
    public double Vega { get; init; }

    /// <summary>
    /// Implied volatility used for calculation (annualized, e.g., 0.20 = 20%).
    /// Null if not available (fallback to historical vol).
    /// </summary>
    public double? ImpliedVolatility { get; init; }

    /// <summary>
    /// Timestamp when these Greeks were calculated.
    /// </summary>
    public DateTime CalculatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Underlying price used for calculation.
    /// </summary>
    public double UnderlyingPrice { get; init; }

    /// <summary>
    /// Creates an empty Greeks object (all zeros) for positions without options.
    /// </summary>
    public static GreeksData Empty => new()
    {
        Delta = 0,
        Gamma = 0,
        Theta = 0,
        Vega = 0,
        ImpliedVolatility = null,
        CalculatedAtUtc = DateTime.UtcNow,
        UnderlyingPrice = 0
    };
}
