namespace SharedKernel.Domain;

/// <summary>
/// Immutable snapshot of Implied Volatility Term Structure (IVTS) data for a given symbol.
/// Contains IV values for multiple expirations (30d, 60d, 90d, 120d) and derived metrics.
/// </summary>
public sealed record IvtsSnapshot
{
    /// <summary>
    /// Unique snapshot identifier (GUID).
    /// </summary>
    public string SnapshotId { get; init; } = string.Empty;

    /// <summary>
    /// Underlying symbol (e.g., "SPX", "SPY").
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp when this snapshot was captured (ISO8601 UTC).
    /// </summary>
    public string TimestampUtc { get; init; } = string.Empty;

    /// <summary>
    /// Implied volatility for ~30-day expiration (decimal, e.g., 0.15 = 15%).
    /// </summary>
    public double Iv30d { get; init; }

    /// <summary>
    /// Implied volatility for ~60-day expiration (decimal, e.g., 0.18 = 18%).
    /// </summary>
    public double Iv60d { get; init; }

    /// <summary>
    /// Implied volatility for ~90-day expiration (decimal, e.g., 0.20 = 20%).
    /// </summary>
    public double Iv90d { get; init; }

    /// <summary>
    /// Implied volatility for ~120-day expiration (decimal, e.g., 0.22 = 22%).
    /// </summary>
    public double Iv120d { get; init; }

    /// <summary>
    /// Implied Volatility Rank (IVR): (Current IV - Min IV) / (Max IV - Min IV).
    /// Range: 0.0 to 1.0 (or null if insufficient data).
    /// </summary>
    public double? IvrPercentile { get; init; }

    /// <summary>
    /// Term structure slope: (IV120d - IV30d) / 90.
    /// Positive = upward slope, Negative = inverted curve.
    /// </summary>
    public double TermStructureSlope { get; init; }

    /// <summary>
    /// True if term structure is inverted (shorter expiry > longer expiry).
    /// Indicates market stress or unusual conditions.
    /// </summary>
    public bool IsInverted { get; init; }

    /// <summary>
    /// Minimum IV observed in lookback window (for IVR calculation).
    /// </summary>
    public double? IvMin52Week { get; init; }

    /// <summary>
    /// Maximum IV observed in lookback window (for IVR calculation).
    /// </summary>
    public double? IvMax52Week { get; init; }

    /// <summary>
    /// ISO8601 timestamp when this record was created in the database.
    /// </summary>
    public string CreatedAt { get; init; } = string.Empty;
}
