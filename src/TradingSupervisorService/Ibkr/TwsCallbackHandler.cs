using IBApi;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;
using TradingSupervisorService.Services;

namespace TradingSupervisorService.Ibkr;

/// <summary>
/// EWrapper implementation for IBKR TWS API callbacks.
/// Thread-safe callback handler with logging for all IBKR messages.
/// </summary>
public sealed class TwsCallbackHandler : EWrapper
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

    public void connectAck()
    {
        _logger.LogInformation("IBKR connection acknowledged");
        _onConnectionStateChanged(ConnectionState.Connected);
    }

    public void connectionClosed()
    {
        _logger.LogWarning("IBKR connection closed");
        _onConnectionStateChanged(ConnectionState.Disconnected);
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
        _logger.LogInformation("IBKR next valid order ID: {OrderId}", orderId);
    }

    #endregion

    #region Market Data Callbacks

    public void tickPrice(int tickerId, int field, double price, TickAttrib attribs)
    {
        _logger.LogDebug("Tick price: reqId={ReqId} field={Field} price={Price} canAutoExecute={CanAutoExecute} pastLimit={PastLimit}",
            tickerId, field, price, attribs.CanAutoExecute, attribs.PastLimit);

        // Forward to MarketDataService if available
        _marketDataService?.OnTickPrice(tickerId, field, price);
    }

    public void tickSize(int tickerId, int field, decimal size)
    {
        _logger.LogDebug("Tick size: reqId={ReqId} field={Field} size={Size}", tickerId, field, size);

        // Forward to MarketDataService if available
        _marketDataService?.OnTickSize(tickerId, field, size);
    }

    public void tickString(int tickerId, int tickType, string value)
    {
        _logger.LogDebug("Tick string: reqId={ReqId} type={Type} value={Value}", tickerId, tickType, value);
    }

    public void tickGeneric(int tickerId, int tickType, double value)
    {
        _logger.LogDebug("Tick generic: reqId={ReqId} type={Type} value={Value}", tickerId, tickType, value);
    }

    public void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints,
        double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate)
    {
        _logger.LogDebug("Tick EFP: reqId={ReqId}", tickerId);
    }

    public void tickSnapshotEnd(int reqId)
    {
        _logger.LogDebug("Tick snapshot end: reqId={ReqId}", reqId);
    }

    public void marketDataType(int reqId, int marketDataType)
    {
        _logger.LogDebug("Market data type: reqId={ReqId} type={Type}", reqId, marketDataType);
    }

    public void tickOptionComputation(int tickerId, int field, int tickAttrib, double impliedVolatility,
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

    public void error(Exception e)
    {
        _logger.LogError(e, "IBKR exception");
    }

    public void error(string str)
    {
        _logger.LogError("IBKR error string: {Error}", str);
    }

    public void error(int id, int errorCode, string errorMsg, string advancedOrderRejectJson)
    {
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

    public void accountDownloadEnd(string account) { }
    public void accountSummary(int reqId, string account, string tag, string value, string currency) { }
    public void accountSummaryEnd(int reqId) { }
    public void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) { }
    public void accountUpdateMultiEnd(int reqId) { }
    public void bondContractDetails(int reqId, ContractDetails contract) { }
    public void commissionReport(CommissionReport commissionReport) { }
    public void completedOrder(Contract contract, Order order, OrderState orderState) { }
    public void completedOrdersEnd() { }
    public void contractDetails(int reqId, ContractDetails contractDetails) { }
    public void contractDetailsEnd(int reqId) { }
    public void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) { }
    public void displayGroupList(int reqId, string groups) { }
    public void displayGroupUpdated(int reqId, string contractInfo) { }
    public void execDetails(int reqId, Contract contract, Execution execution) { }
    public void execDetailsEnd(int reqId) { }
    public void familyCodes(FamilyCode[] familyCodes) { }
    public void fundamentalData(int reqId, string data) { }
    public void histogramData(int reqId, HistogramEntry[] data) { }
    public void historicalData(int reqId, Bar bar) { }
    public void historicalDataEnd(int reqId, string start, string end) { }
    public void historicalDataUpdate(int reqId, Bar bar) { }
    public void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { }
    public void historicalNewsEnd(int requestId, bool hasMore) { }
    public void historicalSchedule(int reqId, string startDateTime, string endDateTime, string timeZone, HistoricalSession[] sessions) { }
    public void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { }
    public void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { }
    public void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { }
    public void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) { }
    public void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) { }
    public void newsArticle(int requestId, int articleType, string articleText) { }
    public void newsProviders(NewsProvider[] newsProviders) { }
    public void openOrder(int orderId, Contract contract, Order order, OrderState orderState) { }
    public void openOrderEnd() { }
    public void orderBound(long orderId, int apiClientId, int apiOrderId) { }
    public void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice) { }
    public void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) { }
    public void pnlSingle(int reqId, decimal pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) { }
    public void position(string account, Contract contract, decimal pos, double avgCost) { }
    public void positionEnd() { }
    public void positionMulti(int reqId, string account, string modelCode, Contract contract, decimal pos, double avgCost) { }
    public void positionMultiEnd(int reqId) { }
    public void realtimeBar(int reqId, long time, double open, double high, double low, double close, decimal volume, decimal wap, int count) { }
    public void receiveFA(int faDataType, string faXmlData) { }
    public void replaceFAEnd(int reqId, string text) { }
    public void rerouteMktDataReq(int reqId, int conId, string exchange) { }
    public void rerouteMktDepthReq(int reqId, int conId, string exchange) { }
    public void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) { }
    public void scannerDataEnd(int reqId) { }
    public void scannerParameters(string xml) { }
    public void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) { }
    public void securityDefinitionOptionParameterEnd(int reqId) { }
    public void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) { }
    public void softDollarTiers(int reqId, SoftDollarTier[] tiers) { }
    public void symbolSamples(int reqId, ContractDescription[] contractDescriptions) { }
    public void tickByTickAllLast(int reqId, int tickType, long time, double price, decimal size, TickAttribLast tickAttribLast, string exchange, string specialConditions) { }
    public void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, decimal bidSize, decimal askSize, TickAttribBidAsk tickAttribBidAsk) { }
    public void tickByTickMidPoint(int reqId, long time, double midPoint) { }
    public void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }
    public void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { }
    public void updateAccountTime(string timestamp) { }
    public void updateAccountValue(string key, string value, string currency, string accountName) { }
    public void updateMktDepth(int tickerId, int position, int operation, int side, double price, decimal size) { }
    public void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth) { }
    public void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) { }
    public void updatePortfolio(Contract contract, decimal position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName) { }
    public void verifyAndAuthCompleted(bool isSuccessful, string errorText) { }
    public void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) { }
    public void verifyCompleted(bool isSuccessful, string errorText) { }
    public void verifyMessageAPI(string apiData) { }
    public void wshEventData(int reqId, string dataJson) { }
    public void wshMetaData(int reqId, string dataJson) { }
    public void userInfo(int reqId, string whiteBrandingId) { }
    public void headTimestamp(int reqId, string headTimestamp) { }
    public void familyCodeList(FamilyCode[] familyCodes) { }
    public void tickNews2(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }

    #endregion
}
