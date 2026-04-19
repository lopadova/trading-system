using IBApi;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;
using TradingSupervisorService.Services;

namespace TradingSupervisorService.Ibkr;

/// <summary>
/// EWrapper implementation for IBKR TWS API callbacks.
/// Thread-safe callback handler with logging for all IBKR messages.
/// Inherits from DefaultEWrapper to get default implementations of all interface methods.
/// </summary>
public sealed class TwsCallbackHandler : DefaultEWrapper
{
    private readonly ILogger<TwsCallbackHandler> _logger;
    private readonly Action<ConnectionState> _onConnectionStateChanged;

    // Thread-safe storage for callback responses
    private readonly object _lock = new();
    private DateTime _lastServerTime = DateTime.MinValue;

    // Optional market data service for forwarding callbacks
    private MarketDataService? _marketDataService;

    public TwsCallbackHandler(
        ILogger<TwsCallbackHandler> logger,
        Action<ConnectionState> onConnectionStateChanged)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _onConnectionStateChanged = onConnectionStateChanged ?? throw new ArgumentNullException(nameof(onConnectionStateChanged));
    }

    /// <summary>
    /// Sets the market data service for forwarding market data callbacks.
    /// This should be called after the service is instantiated.
    /// </summary>
    public void SetMarketDataService(MarketDataService marketDataService)
    {
        _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
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
        _onConnectionStateChanged(ConnectionState.Connected);
    }

    public override void connectionClosed()
    {
        _logger.LogWarning("IBKR connection closed");
        _onConnectionStateChanged(ConnectionState.Disconnected);
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
        _logger.LogInformation("✓ TWS nextValidId({OrderId}) received - connection ready for orders", orderId);
    }

    #endregion

    #region Market Data Callbacks

    public override void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
    {
        _logger.LogDebug("Tick price: reqId={ReqId} field={Field} price={Price} canAutoExecute={CanAutoExecute} pastLimit={PastLimit}",
            tickerId, field, price, attribs.CanAutoExecute, attribs.PastLimit);

        // Forward to MarketDataService if available
        _marketDataService?.OnTickPrice(tickerId, field, price);
    }

    public override void tickSize(int tickerId, int field, decimal size)
    {
        _logger.LogDebug("Tick size: reqId={ReqId} field={Field} size={Size}", tickerId, field, size);

        // Forward to MarketDataService if available
        _marketDataService?.OnTickSize(tickerId, field, size);
    }

    public override void tickString(int tickerId, int tickType, string value)
    {
        _logger.LogDebug("Tick string: reqId={ReqId} type={Type} value={Value}", tickerId, tickType, value);
    }

    public override void tickGeneric(int tickerId, int tickType, double value)
    {
        _logger.LogDebug("Tick generic: reqId={ReqId} type={Type} value={Value}", tickerId, tickType, value);
    }

    public override void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints,
        double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate)
    {
        _logger.LogDebug("Tick EFP: reqId={ReqId}", tickerId);
    }

    public override void tickSnapshotEnd(int reqId)
    {
        _logger.LogDebug("Tick snapshot end: reqId={ReqId}", reqId);
    }

    public override void marketDataType(int reqId, int marketDataType)
    {
        _logger.LogDebug("Market data type: reqId={ReqId} type={Type}", reqId, marketDataType);
    }

    public override void tickOptionComputation(int tickerId, int field, int tickAttrib, double impliedVolatility,
        double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice)
    {
        _logger.LogDebug(
            "Option computation: reqId={ReqId} field={Field} IV={IV:F4} delta={Delta:F4} gamma={Gamma:F4} vega={Vega:F4} theta={Theta:F4}",
            tickerId, field, impliedVolatility, delta, gamma, vega, theta);

        // Forward to MarketDataService if available
        _marketDataService?.OnTickOptionComputation(tickerId, field, impliedVolatility, delta, optPrice, gamma, vega, theta, undPrice);
    }

    #endregion

    #region Error Handling

    public override void error(Exception e)
    {
        _logger.LogError(e, "IBKR exception");
    }

    public override void error(string str)
    {
        _logger.LogError("IBKR error string: {Error}", str);
    }

    public override void error(int id, long errorTime, int errorCode, string errorMsg, string advancedOrderRejectJson)
    {
        // Log ALL errors/messages during connection phase (first 30 seconds)
        _logger.LogDebug("← TWS message: id={Id} code={Code} msg={Msg} time={Time}", id, errorCode, errorMsg, errorTime);

        // Filter informational messages (market data farm connections)
        if (errorCode is 2104 or 2106 or 2158)
        {
            _logger.LogDebug("IBKR info {Code}: {Msg}", errorCode, errorMsg);
            return;
        }

        // Connection lost errors
        if (errorCode is 1100 or 1300)
        {
            _logger.LogWarning("IBKR connection lost {Code}: {Msg}", errorCode, errorMsg);
            _onConnectionStateChanged(ConnectionState.Disconnected);
            return;
        }

        // Connection restored
        if (errorCode is 1101 or 1102)
        {
            _logger.LogInformation("IBKR connection restored {Code}: {Msg}", errorCode, errorMsg);
            _onConnectionStateChanged(ConnectionState.Connected);
            return;
        }

        // All other errors
        _logger.LogError("IBKR error {Code} for req {Id}: {Msg} {Json}",
            errorCode, id, errorMsg, advancedOrderRejectJson ?? "");
    }

    #endregion

    #region Unused Callbacks (stubs required by EWrapper interface)

    public override void accountDownloadEnd(string account) { }
    public override void accountSummary(int reqId, string account, string tag, string value, string currency) { }
    public override void accountSummaryEnd(int reqId) { }
    public override void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) { }
    public override void accountUpdateMultiEnd(int reqId) { }
    public override void bondContractDetails(int reqId, ContractDetails contract) { }
    public override void commissionAndFeesReport(CommissionAndFeesReport commissionAndFeesReport) { }
    public override void completedOrder(Contract contract, Order order, OrderState orderState) { }
    public override void completedOrdersEnd() { }
    public override void contractDetails(int reqId, ContractDetails contractDetails) { }
    public override void contractDetailsEnd(int reqId) { }
    public override void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) { }
    public override void displayGroupList(int reqId, string groups) { }
    public override void displayGroupUpdated(int reqId, string contractInfo) { }
    public override void execDetails(int reqId, Contract contract, Execution execution) { }
    public override void execDetailsEnd(int reqId) { }
    public override void familyCodes(FamilyCode[] familyCodes) { }
    public override void fundamentalData(int reqId, string data) { }
    public override void histogramData(int reqId, HistogramEntry[] data) { }
    public override void historicalData(int reqId, Bar bar) { }
    public override void historicalDataEnd(int reqId, string start, string end) { }
    public override void historicalDataUpdate(int reqId, Bar bar) { }
    public override void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { }
    public override void historicalNewsEnd(int requestId, bool hasMore) { }
    public override void historicalSchedule(int reqId, string startDateTime, string endDateTime, string timeZone, HistoricalSession[] sessions) { }
    public override void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { }
    public override void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { }
    public override void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { }
    public override void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) { }
    public override void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) { }
    public override void newsArticle(int requestId, int articleType, string articleText) { }
    public override void newsProviders(NewsProvider[] newsProviders) { }
    public override void openOrder(int orderId, Contract contract, Order order, OrderState orderState) { }
    public override void openOrderEnd() { }
    public override void orderBound(long orderId, int apiClientId, int apiOrderId) { }
    public override void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice, long permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice) { }
    public override void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) { }
    public override void pnlSingle(int reqId, decimal pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) { }
    public override void position(string account, Contract contract, decimal pos, double avgCost) { }
    public override void positionEnd() { }
    public override void positionMulti(int reqId, string account, string modelCode, Contract contract, decimal pos, double avgCost) { }
    public override void positionMultiEnd(int reqId) { }
    public override void realtimeBar(int reqId, long time, double open, double high, double low, double close, decimal volume, decimal wap, int count) { }
    public override void receiveFA(int faDataType, string faXmlData) { }
    public override void replaceFAEnd(int reqId, string text) { }
    public override void rerouteMktDataReq(int reqId, int conId, string exchange) { }
    public override void rerouteMktDepthReq(int reqId, int conId, string exchange) { }
    public override void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) { }
    public override void scannerDataEnd(int reqId) { }
    public override void scannerParameters(string xml) { }
    public override void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) { }
    public override void securityDefinitionOptionParameterEnd(int reqId) { }
    public override void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) { }
    public override void softDollarTiers(int reqId, SoftDollarTier[] tiers) { }
    public override void symbolSamples(int reqId, ContractDescription[] contractDescriptions) { }
    public override void tickByTickAllLast(int reqId, int tickType, long time, double price, decimal size, TickAttribLast tickAttribLast, string exchange, string specialConditions) { }
    public override void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, decimal bidSize, decimal askSize, TickAttribBidAsk tickAttribBidAsk) { }
    public override void tickByTickMidPoint(int reqId, long time, double midPoint) { }
    public override void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }
    public override void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { }
    public override void updateAccountTime(string timestamp) { }
    public override void updateAccountValue(string key, string value, string currency, string accountName) { }
    public override void updateMktDepth(int tickerId, int position, int operation, int side, double price, decimal size) { }
    public override void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth) { }
    public override void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) { }
    public override void updatePortfolio(Contract contract, decimal position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName) { }
    public override void verifyAndAuthCompleted(bool isSuccessful, string errorText) { }
    public override void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) { }
    public override void verifyCompleted(bool isSuccessful, string errorText) { }
    public override void verifyMessageAPI(string apiData) { }
    public override void wshEventData(int reqId, string dataJson) { }
    public override void wshMetaData(int reqId, string dataJson) { }
    public override void userInfo(int reqId, string whiteBrandingId) { }
    public override void headTimestamp(int reqId, string headTimestamp) { }

    #endregion
}
