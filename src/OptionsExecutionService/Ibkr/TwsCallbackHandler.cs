using IBApi;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;

namespace OptionsExecutionService.Ibkr;

/// <summary>
/// EWrapper implementation for IBKR TWS API callbacks in OptionsExecutionService.
/// Handles order status updates, errors, and connection events.
/// Thread-safe callback handler with event forwarding for order tracking.
/// Inherits from DefaultEWrapper to get default implementations of all interface methods.
/// </summary>
public sealed class TwsCallbackHandler : DefaultEWrapper
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

    public override void connectAck()
    {
        _logger.LogInformation("════════════════════════════════");
        _logger.LogInformation("✓ IBKR connectAck() received!");
        _logger.LogInformation("════════════════════════════════");
        _onConnectionStateChanged?.Invoke(ConnectionState.Connected);
    }

    public override void connectionClosed()
    {
        _logger.LogWarning("IBKR connection closed");
        _onConnectionStateChanged?.Invoke(ConnectionState.Disconnected);
    }

    #endregion

    #region Server Info

    public override void currentTime(long time)
    {
        DateTime serverTime = DateTimeOffset.FromUnixTimeSeconds(time).UtcDateTime;
        lock (_lock)
        {
            _lastServerTime = serverTime;
        }
        _logger.LogDebug("IBKR server time: {ServerTime:O}", serverTime);
    }

    public override void managedAccounts(string accountsList)
    {
        _logger.LogInformation("IBKR managed accounts: {Accounts}", accountsList);
    }

    public override void nextValidId(int orderId)
    {
        lock (_lock)
        {
            _nextValidOrderId = orderId;
        }
        _logger.LogInformation("✓ TWS nextValidId({OrderId}) received - connection ready for orders", orderId);
    }

    #endregion

    #region Order Status

    public override void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice,
        long permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
    {
        _logger.LogInformation(
            "Order status: orderId={OrderId} status={Status} filled={Filled} remaining={Remaining} avgPrice={AvgPrice}",
            orderId, status, filled, remaining, avgFillPrice);

        OrderStatusChanged?.Invoke(this, (orderId, status, (int)filled, (int)remaining, avgFillPrice));
    }

    public override void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
    {
        _logger.LogInformation(
            "Open order: orderId={OrderId} symbol={Symbol} action={Action} qty={Qty} status={Status}",
            orderId, contract.Symbol, order.Action, order.TotalQuantity, orderState.Status);
    }

    public override void openOrderEnd()
    {
        _logger.LogDebug("Open orders request completed");
    }

    public override void orderBound(long orderId, int apiClientId, int apiOrderId)
    {
        _logger.LogDebug("Order bound: orderId={OrderId}", orderId);
    }

    #endregion

    #region Error Handling

    public override void error(Exception e)
    {
        _logger.LogError(e, "IBKR exception");
    }

    public override void error(string str)
    {
        _logger.LogError("IBKR error: {Message}", str);
    }

    public override void error(int id, long errorTime, int errorCode, string errorMsg, string advancedOrderRejectJson)
    {
        // Log ALL errors/messages during connection phase (first 30 seconds)
        _logger.LogDebug("← TWS message: id={Id} code={Code} msg={Msg} time={Time}", id, errorCode, errorMsg, errorTime);

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

    public override void tickPrice(int tickerId, int field, double price, TickAttrib attribs) { }
    public override void tickSize(int tickerId, int field, decimal size) { }
    public override void tickString(int tickerId, int tickType, string value) { }
    public override void tickGeneric(int tickerId, int tickType, double value) { }
    public override void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints,
        double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) { }
    public override void tickSnapshotEnd(int reqId) { }
    public override void marketDataType(int reqId, int marketDataType) { }
    public override void tickOptionComputation(int tickerId, int field, int tickAttrib, double impliedVolatility,
        double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) { }

    #endregion

    #region Account & Position (stub implementations - not used in OptionsExecutionService)

    public override void updateAccountValue(string key, string value, string currency, string accountName) { }
    public override void updatePortfolio(Contract contract, decimal position, double marketPrice, double marketValue,
        double averageCost, double unrealizedPNL, double realizedPNL, string accountName) { }
    public override void updateAccountTime(string timestamp) { }
    public override void accountDownloadEnd(string account) { }
    public override void position(string account, Contract contract, decimal pos, double avgCost) { }
    public override void positionEnd() { }
    public override void accountSummary(int reqId, string account, string tag, string value, string currency) { }
    public override void accountSummaryEnd(int reqId) { }

    #endregion

    #region Contract & Execution (minimal implementations)

    public override void contractDetails(int reqId, ContractDetails contractDetails)
    {
        _logger.LogDebug("Contract details: reqId={ReqId} symbol={Symbol}", reqId, contractDetails.Contract.Symbol);
    }

    public override void bondContractDetails(int reqId, ContractDetails contractDetails) { }
    public override void contractDetailsEnd(int reqId) { }

    public override void execDetails(int reqId, Contract contract, Execution execution)
    {
        _logger.LogInformation(
            "Execution: reqId={ReqId} orderId={OrderId} symbol={Symbol} side={Side} shares={Shares} price={Price}",
            reqId, execution.OrderId, contract.Symbol, execution.Side, execution.Shares, execution.Price);
    }

    public override void execDetailsEnd(int reqId) { }
    public override void commissionAndFeesReport(CommissionAndFeesReport commissionAndFeesReport)
    {
        _logger.LogInformation("Commission: execId={ExecId} commission={CommissionAndFees}",
            commissionAndFeesReport.ExecId, commissionAndFeesReport.CommissionAndFees);
    }

    #endregion

    #region Historical Data & Other (stub implementations)

    public override void historicalData(int reqId, Bar bar) { }
    public override void historicalDataEnd(int reqId, string start, string end) { }
    public override void scannerParameters(string xml) { }
    public override void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance,
        string benchmark, string projection, string legsStr) { }
    public override void scannerDataEnd(int reqId) { }
    public override void realtimeBar(int reqId, long time, double open, double high, double low, double close,
        decimal volume, decimal wap, int count) { }
    public override void fundamentalData(int reqId, string data) { }
    public override void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) { }
    public override void receiveFA(int faDataType, string faXmlData) { }
    public override void verifyMessageAPI(string apiData) { }
    public override void verifyCompleted(bool isSuccessful, string errorText) { }
    public override void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) { }
    public override void verifyAndAuthCompleted(bool isSuccessful, string errorText) { }
    public override void displayGroupList(int reqId, string groups) { }
    public override void displayGroupUpdated(int reqId, string contractInfo) { }
    public override void positionMulti(int reqId, string account, string modelCode, Contract contract, decimal pos, double avgCost) { }
    public override void positionMultiEnd(int reqId) { }
    public override void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) { }
    public override void accountUpdateMultiEnd(int reqId) { }
    public override void softDollarTiers(int reqId, SoftDollarTier[] tiers) { }
    public override void familyCodes(FamilyCode[] familyCodes) { }
    public override void symbolSamples(int reqId, ContractDescription[] contractDescriptions) { }
    public override void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) { }
    public override void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }
    public override void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) { }
    public override void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { }
    public override void newsProviders(NewsProvider[] newsProviders) { }
    public override void newsArticle(int requestId, int articleType, string articleText) { }
    public override void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { }
    public override void historicalNewsEnd(int requestId, bool hasMore) { }
    public override void headTimestamp(int reqId, string headTimestamp) { }
    public override void historicalDataUpdate(int reqId, Bar bar) { }
    public override void rerouteMktDataReq(int reqId, int conId, string exchange) { }
    public override void rerouteMktDepthReq(int reqId, int conId, string exchange) { }
    public override void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) { }
    public override void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) { }
    public override void pnlSingle(int reqId, decimal pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) { }
    public override void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { }
    public override void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { }
    public override void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { }
    public override void tickByTickAllLast(int reqId, int tickType, long time, double price, decimal size,
        TickAttribLast tickAttribLast, string exchange, string specialConditions) { }
    public override void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, decimal bidSize,
        decimal askSize, TickAttribBidAsk tickAttribBidAsk) { }
    public override void tickByTickMidPoint(int reqId, long time, double midPoint) { }
    public override void completedOrder(Contract contract, Order order, OrderState orderState) { }
    public override void completedOrdersEnd() { }
    public override void replaceFAEnd(int reqId, string text) { }
    public override void wshMetaData(int reqId, string dataJson) { }
    public override void wshEventData(int reqId, string dataJson) { }
    public override void historicalSchedule(int reqId, string startDateTime, string endDateTime, string timeZone, HistoricalSession[] sessions) { }
    public override void userInfo(int reqId, string whiteBrandingId) { }

    // Additional missing methods from EWrapper interface
    public override void updateMktDepth(int tickerId, int position, int operation, int side, double price, decimal size) { }
    public override void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth) { }
    public override void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) { }
    public override void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) { }
    public override void securityDefinitionOptionParameterEnd(int reqId) { }
    public override void histogramData(int reqId, HistogramEntry[] data) { }

    #endregion
}
