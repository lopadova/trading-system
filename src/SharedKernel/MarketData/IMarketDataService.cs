namespace SharedKernel.MarketData;

/// <summary>
/// Service for subscribing to and retrieving real-time market data from IBKR.
/// Thread-safe. Provides caching and event notifications for price updates.
/// </summary>
public interface IMarketDataService
{
    /// <summary>
    /// Subscribe to market data for a symbol.
    /// Data updates will be cached and available via GetSnapshot().
    /// </summary>
    /// <param name="symbol">Symbol (e.g., "SPX", "VIX3M")</param>
    /// <param name="secType">Security type (e.g., "IND", "OPT", "STK")</param>
    /// <param name="exchange">Exchange (e.g., "CBOE", "SMART")</param>
    /// <param name="currency">Currency (default: USD)</param>
    /// <param name="includeGreeks">Whether to request option Greeks (genericTickList="106")</param>
    /// <returns>Request ID for this subscription</returns>
    int Subscribe(string symbol, string secType, string exchange, string currency = "USD", bool includeGreeks = false);

    /// <summary>
    /// Subscribe to market data for an option contract with expiration date.
    /// Automatically calculates DTE.
    /// </summary>
    /// <param name="symbol">Underlying symbol (e.g., "SPX")</param>
    /// <param name="exchange">Exchange (e.g., "SMART")</param>
    /// <param name="right">Option right: "C" (call) or "P" (put)</param>
    /// <param name="strike">Strike price</param>
    /// <param name="expirationDate">Expiration date (YYYYMMDD format)</param>
    /// <param name="includeGreeks">Whether to request option Greeks</param>
    /// <returns>Request ID for this subscription</returns>
    int SubscribeOption(
        string symbol,
        string exchange,
        string right,
        double strike,
        string expirationDate,
        bool includeGreeks = true);

    /// <summary>
    /// Cancel market data subscription.
    /// </summary>
    /// <param name="requestId">Request ID to cancel</param>
    void Unsubscribe(int requestId);

    /// <summary>
    /// Get the latest market data snapshot for a request ID.
    /// Returns null if no data available yet.
    /// </summary>
    /// <param name="requestId">Request ID</param>
    /// <returns>Latest snapshot or null</returns>
    MarketDataSnapshot? GetSnapshot(int requestId);

    /// <summary>
    /// Get the latest market data snapshot for a symbol.
    /// Returns null if symbol is not subscribed or no data available.
    /// </summary>
    /// <param name="symbol">Symbol</param>
    /// <returns>Latest snapshot or null</returns>
    MarketDataSnapshot? GetSnapshotBySymbol(string symbol);

    /// <summary>
    /// Get all active subscriptions.
    /// </summary>
    /// <returns>Dictionary of requestId -> symbol</returns>
    IReadOnlyDictionary<int, string> GetActiveSubscriptions();

    /// <summary>
    /// Calculate days to expiration from an expiration date.
    /// </summary>
    /// <param name="expirationDate">Expiration date in YYYYMMDD format</param>
    /// <returns>Days to expiration (0 if today, null if invalid format)</returns>
    int? CalculateDTE(string expirationDate);

    /// <summary>
    /// Event raised when market data is updated for a symbol.
    /// Parameters: (requestId, snapshot)
    /// </summary>
    event EventHandler<(int RequestId, MarketDataSnapshot Snapshot)>? MarketDataUpdated;
}
