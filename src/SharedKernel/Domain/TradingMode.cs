namespace SharedKernel.Domain;

/// <summary>
/// Represents the trading mode for the system: Paper (simulation) or Live (real money).
/// SAFETY: Default is always Paper. Live mode must be explicitly enabled.
/// </summary>
public enum TradingMode
{
    /// <summary>
    /// Paper trading mode - simulated orders, no real money.
    /// This is the default and safe mode for development and testing.
    /// </summary>
    Paper = 0,

    /// <summary>
    /// Live trading mode - real orders with real money.
    /// USE WITH EXTREME CAUTION. Requires explicit configuration.
    /// </summary>
    Live = 1
}
