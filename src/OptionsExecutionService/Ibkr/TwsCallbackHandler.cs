using IBApi;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;

namespace OptionsExecutionService.Ibkr;

/// <summary>
/// EWrapper implementation for IBKR TWS API callbacks in OptionsExecutionService.
/// Handles order status updates, errors, and connection events.
/// Thread-safe callback handler with event forwarding for order tracking.
/// </summary>
public sealed class TwsCallbackHandler : EWrapper
{
    private readonly ILogger<TwsCallbackHandler> _logger;
    private readonly object _lock = new();

    // State management
    private Action<ConnectionState>? _onConnectionStateChanged;
    private int _nextValidOrderId = 0;
    private DateTime _lastServerTime = DateTime.MinValue;

    // Events for order tracking
    public event EventHandler<(int OrderId, string Status, int Filled, int Remaining, double AvgFillPrice)>? OrderStatusChanged;
    public event EventHandler<(int OrderId, int ErrorCode, string ErrorMessage)>? OrderError;

    public TwsCallbackHandler(ILogger<TwsCallbackHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Sets the connection state change callback.
    /// Must be called by IbkrClient after construction.
    /// </summary>
    public void SetConnectionStateCallback(Action<ConnectionState> callback)
    {
        _onConnectionStateChanged = callback ?? throw new ArgumentNullException(nameof(callback));
    }

    public int NextValidOrderId
    {
        get { lock (_lock) return _nextValidOrderId; }
    }

    public DateTime LastServerTime
    {
        get { lock (_lock) return _lastServerTime; }
    }

    #region Connection Lifecycle

    public void connectAck()
    {
        _logger.LogInformation("IBKR connection acknowledged");
        _onConnectionStateChanged?.Invoke(ConnectionState.Connected);
    }

    public void connectionClosed()
    {
        _logger.LogWarning("IBKR connection closed");
        _onConnectionStateChanged?.Invoke(ConnectionState.Disconnected);
    }

    #endregion

    #region Server Info

    public void currentTime(long time)
    {
        DateTime serverTime = DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime;
        lock (_lock)
        {
            _lastServerTime = serverTime;
        }
        _logger.LogDebug("IBKR server time: {ServerTime:O}", serverTime);
    }

    public void managedAccounts(string accountsList)
    {
        _logger.LogInformation("IBKR managed accounts: {Accounts}", accountsList);
    }

    public void nextValidId(int orderId)
    {
        lock (_lock)
        {
            _nextValidOrderId = orderId;
        }
        _logger.LogInformation("IBKR next valid order ID: {OrderId}", orderId);
    }

    #endregion

    #region Order Status

    public void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice,
        int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
    {
        _logger.LogInformation(
            "Order status: orderId={OrderId} status={Status} filled={Filled} remaining={Remaining} avgPrice={AvgPrice}",
            orderId, status, filled, remaining, avgFillPrice);

        OrderStatusChanged?.Invoke(this, (orderId, status, (int)filled, (int)remaining, avgFillPrice));
    }

    public void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
    {
        _logger.LogInformation(
            "Open order: orderId={OrderId} symbol={Symbol} action={Action} qty={Qty} status={Status}",
            orderId, contract.Symbol, order.Action, order.TotalQuantity, orderState.Status);
    }

    public void openOrderEnd()
    {
        _logger.LogDebug("Open orders request completed");
    }

    public void orderBound(long orderId, int apiClientId, int apiOrderId)
    {
        _logger.LogDebug("Order bound: orderId={OrderId}", orderId);
    }

    #endregion

    #region Error Handling

    public void error(Exception e)
    {
        _logger.LogError(e, "IBKR exception");
    }

    public void error(string str)
    {
        _logger.LogError("IBKR error: {Message}", str);
    }

    public void error(int id, int errorCode, string errorMsg, string advancedOrderRejectJson)
    {
        // Filter informational messages (2104, 2106, 2158 are market data farm status)
        if (errorCode == 2104 || errorCode == 2106 || errorCode == 2158)
        {
            _logger.LogDebug("IBKR info ({Code}): {Message}", errorCode, errorMsg);
            return;
        }

        // Connection events (1100, 1101, 1102, 1300)
        if (errorCode == 1100 || errorCode == 1300)
        {
            _logger.LogWarning("IBKR connection lost ({Code}): {Message}", errorCode, errorMsg);
            _onConnectionStateChanged?.Invoke(ConnectionState.Disconnected);
            return;
        }

        if (errorCode == 1101 || errorCode == 1102)
        {
            _logger.LogInformation("IBKR connection restored ({Code}): {Message}", errorCode, errorMsg);
            _onConnectionStateChanged?.Invoke(ConnectionState.Connected);
            return;
        }

        // Order-specific errors
        if (id > 0 && errorCode >= 200)
        {
            _logger.LogError("Order error: orderId={OrderId} code={Code} message={Message}", id, errorCode, errorMsg);
            OrderError?.Invoke(this, (id, errorCode, errorMsg));
            return;
        }

        // All other errors
        _logger.LogError("IBKR error: id={Id} code={Code} message={Message}", id, errorCode, errorMsg);
    }

    #endregion

    #region Market Data (stub implementations - not used in OptionsExecutionService)

    public void tickPrice(int tickerId, int field, double price, TickAttrib attribs) { }
    public void tickSize(int tickerId, int field, decimal size) { }
    public void tickString(int tickerId, int tickType, string value) { }
    public void tickGeneric(int tickerId, int tickType, double value) { }
    public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints,
        double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) { }
    public void tickSnapshotEnd(int reqId) { }
    public void marketDataType(int reqId, int marketDataType) { }
    public void tickOptionComputation(int tickerId, int field, int tickAttrib, double impliedVolatility,
        double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) { }

    #endregion

    #region Account & Position (stub implementations - not used in OptionsExecutionService)

    public void updateAccountValue(string key, string value, string currency, string accountName) { }
    public void updatePortfolio(Contract contract, decimal position, double marketPrice, double marketValue,
        double averageCost, double unrealizedPNL, double realizedPNL, string accountName) { }
    public void updateAccountTime(string timestamp) { }
    public void accountDownloadEnd(string account) { }
    public void position(string account, Contract contract, decimal pos, double avgCost) { }
    public void positionEnd() { }
    public void accountSummary(int reqId, string account, string tag, string value, string currency) { }
    public void accountSummaryEnd(int reqId) { }

    #endregion

    #region Contract & Execution (minimal implementations)

    public void contractDetails(int reqId, ContractDetails contractDetails)
    {
        _logger.LogDebug("Contract details: reqId={ReqId} symbol={Symbol}", reqId, contractDetails.Contract.Symbol);
    }

    public void bondContractDetails(int reqId, ContractDetails contractDetails) { }
    public void contractDetailsEnd(int reqId) { }

    public void execDetails(int reqId, Contract contract, Execution execution)
    {
        _logger.LogInformation(
            "Execution: reqId={ReqId} orderId={OrderId} symbol={Symbol} side={Side} shares={Shares} price={Price}",
            reqId, execution.OrderId, contract.Symbol, execution.Side, execution.Shares, execution.Price);
    }

    public void execDetailsEnd(int reqId) { }
    public void commissionReport(CommissionReport commissionReport)
    {
        _logger.LogInformation("Commission: orderId={OrderId} commission={Commission}",
            commissionReport.ExecId, commissionReport.Commission);
    }

    #endregion

    #region Historical Data & Other (stub implementations)

    public void historicalData(int reqId, Bar bar) { }
    public void historicalDataEnd(int reqId, string start, string end) { }
    public void scannerParameters(string xml) { }
    public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance,
        string benchmark, string projection, string legsStr) { }
    public void scannerDataEnd(int reqId) { }
    public void realtimeBar(int reqId, long time, double open, double high, double low, double close,
        decimal volume, decimal wap, int count) { }
    public void fundamentalData(int reqId, string data) { }
    public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) { }
    public void receiveFA(int faDataType, string faXmlData) { }
    public void verifyMessageAPI(string apiData) { }
    public void verifyCompleted(bool isSuccessful, string errorText) { }
    public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) { }
    public void verifyAndAuthCompleted(bool isSuccessful, string errorText) { }
    public void displayGroupList(int reqId, string groups) { }
    public void displayGroupUpdated(int reqId, string contractInfo) { }
    public void positionMulti(int reqId, string account, string modelCode, Contract contract, decimal pos, double avgCost) { }
    public void positionMultiEnd(int reqId) { }
    public void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) { }
    public void accountUpdateMultiEnd(int reqId) { }
    public void securityDefinitionOptionalParameter(int reqId, string exchange, int underlyingConId, string tradingClass,
        string multiplier, HashSet<string> expirations, HashSet<double> strikes) { }
    public void securityDefinitionOptionalParameterEnd(int reqId) { }
    public void softDollarTiers(int reqId, SoftDollarTier[] tiers) { }
    public void familyCodes(FamilyCode[] familyCodes) { }
    public void symbolSamples(int reqId, ContractDescription[] contractDescriptions) { }
    public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) { }
    public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }
    public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) { }
    public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { }
    public void newsProviders(NewsProvider[] newsProviders) { }
    public void newsArticle(int requestId, int articleType, string articleText) { }
    public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { }
    public void historicalNewsEnd(int requestId, bool hasMore) { }
    public void headTimestamp(int reqId, string headTimestamp) { }
    public void histogramData(int reqId, Tuple<double, decimal>[] data) { }
    public void historicalDataUpdate(int reqId, Bar bar) { }
    public void rerouteMktDataReq(int reqId, int conId, string exchange) { }
    public void rerouteMktDepthReq(int reqId, int conId, string exchange) { }
    public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) { }
    public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) { }
    public void pnlSingle(int reqId, decimal pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) { }
    public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { }
    public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { }
    public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { }
    public void tickByTickAllLast(int reqId, int tickType, long time, double price, decimal size,
        TickAttribLast tickAttribLast, string exchange, string specialConditions) { }
    public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, decimal bidSize,
        decimal askSize, TickAttribBidAsk tickAttribBidAsk) { }
    public void tickByTickMidPoint(int reqId, long time, double midPoint) { }
    public void completedOrder(Contract contract, Order order, OrderState orderState) { }
    public void completedOrdersEnd() { }
    public void replaceFAEnd(int reqId, string text) { }
    public void wshMetaData(int reqId, string dataJson) { }
    public void wshEventData(int reqId, string dataJson) { }
    public void historicalSchedule(int reqId, string startDateTime, string endDateTime, string timeZone, HistoricalSession[] sessions) { }
    public void userInfo(int reqId, string whiteBrandingId) { }

    // Additional missing methods from EWrapper interface
    public void updateMktDepth(int tickerId, int position, int operation, int side, double price, decimal size) { }
    public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth) { }
    public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) { }
    public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) { }
    public void securityDefinitionOptionParameterEnd(int reqId) { }
    public void familyCodeList(FamilyCode[] familyCodes) { }
    public void tickNews2(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }
    public void histogramData(int reqId, HistogramEntry[] data) { }

    #endregion
}
