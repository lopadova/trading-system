namespace OptionsExecutionService.Services;

/// <summary>
/// Provides current account equity/balance with freshness tracking.
/// Singleton service that caches equity from IBKR account summary.
/// Phase 2: Shared safety state P1 - Task RM-06
/// </summary>
public interface IAccountEquityProvider
{
    /// <summary>
    /// Gets the current account equity with freshness information.
    /// Returns null if no equity data is available.
    /// </summary>
    AccountEquitySnapshot? GetEquity();

    /// <summary>
    /// Updates the cached equity value.
    /// Called by background service that polls IBKR account summary.
    /// </summary>
    void UpdateEquity(decimal netLiquidation, DateTime asOfUtc);
}

/// <summary>
/// Account equity snapshot with timestamp for freshness validation.
/// </summary>
public sealed record AccountEquitySnapshot
{
    public decimal NetLiquidation { get; init; }
    public DateTime AsOfUtc { get; init; }
    public bool IsStale { get; init; }
    public TimeSpan Age { get; init; }
}
