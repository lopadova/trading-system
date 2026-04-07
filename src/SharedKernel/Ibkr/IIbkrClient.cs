namespace SharedKernel.Ibkr;

/// <summary>
/// Abstraction for IBKR TWS API client. Enables testing and decouples from IBApi.EClient.
/// </summary>
public interface IIbkrClient : IDisposable
{
    /// <summary>
    /// Current connection state.
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// True if currently connected to IBKR.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Connects to IBKR asynchronously with retry logic.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task that completes when connection succeeds or max retries reached</returns>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnects from IBKR gracefully.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Request current server time (keepalive).
    /// Response arrives via currentTime() callback.
    /// </summary>
    void RequestCurrentTime();

    /// <summary>
    /// Request market data for a contract.
    /// </summary>
    /// <param name="requestId">Unique request ID</param>
    /// <param name="symbol">Symbol (e.g., "SPX")</param>
    /// <param name="secType">Security type (e.g., "IND", "OPT")</param>
    /// <param name="exchange">Exchange (e.g., "CBOE", "SMART")</param>
    /// <param name="currency">Currency (default: USD)</param>
    /// <param name="genericTickList">Comma-separated tick types (e.g., "106" for Greeks)</param>
    /// <param name="snapshot">True for snapshot, false for streaming</param>
    void RequestMarketData(
        int requestId,
        string symbol,
        string secType,
        string exchange,
        string currency = "USD",
        string genericTickList = "",
        bool snapshot = false);

    /// <summary>
    /// Cancel market data subscription.
    /// </summary>
    void CancelMarketData(int requestId);

    /// <summary>
    /// Place an order with IBKR.
    /// </summary>
    /// <param name="orderId">Unique IBKR order ID (must be unique across all orders)</param>
    /// <param name="request">Order request details</param>
    /// <returns>True if order was successfully submitted (does not guarantee fill)</returns>
    bool PlaceOrder(int orderId, Domain.OrderRequest request);

    /// <summary>
    /// Cancel an order by IBKR order ID.
    /// </summary>
    /// <param name="orderId">IBKR order ID to cancel</param>
    void CancelOrder(int orderId);

    /// <summary>
    /// Request all open orders for this client.
    /// Responses arrive via openOrder() callback.
    /// </summary>
    void RequestOpenOrders();

    /// <summary>
    /// Request current positions.
    /// Responses arrive via position() callback.
    /// </summary>
    void RequestPositions();

    /// <summary>
    /// Request account information (balance, buying power, etc.).
    /// Responses arrive via accountSummary() callback.
    /// </summary>
    /// <param name="requestId">Unique request ID</param>
    void RequestAccountSummary(int requestId);

    /// <summary>
    /// Get the next valid order ID. Must be called before placing first order.
    /// Response arrives via nextValidId() callback.
    /// </summary>
    int GetNextOrderId();

    /// <summary>
    /// Event raised when connection state changes.
    /// </summary>
    event EventHandler<ConnectionState>? ConnectionStateChanged;

    /// <summary>
    /// Event raised when an order status update is received from IBKR.
    /// Parameters: orderId, status, filled, remaining, avgFillPrice
    /// </summary>
    event EventHandler<(int OrderId, string Status, int Filled, int Remaining, double AvgFillPrice)>? OrderStatusChanged;

    /// <summary>
    /// Event raised when an order is rejected or encounters an error.
    /// Parameters: orderId, errorCode, errorMessage
    /// </summary>
    event EventHandler<(int OrderId, int ErrorCode, string ErrorMessage)>? OrderError;
}
