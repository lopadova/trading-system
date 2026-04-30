// STUB IMPLEMENTATION - Replace with actual IBApi from TWS/IB Gateway installation
// This is a minimal stub to allow compilation. User must install actual IBApi.dll later.

namespace IBApi;

#pragma warning disable CA1040, CA1716, CA1819, CS8618, IDE0060

public interface EWrapper
{
    void error(Exception e);
    void error(string str);
    void error(int id, int errorCode, string errorMsg, string advancedOrderRejectJson);
    // TWS API 10.19+ added an `errorTime` Unix-millis timestamp parameter between `id` and `errorCode`.
    // Our callback handlers override this newer overload — expose both in the stub.
    void error(int id, long errorTime, int errorCode, string errorMsg, string advancedOrderRejectJson);
    void connectionClosed();
    void currentTime(long time);
    void tickPrice(int tickerId, int field, double price, TickAttrib attribs);
    void tickSize(int tickerId, int field, decimal size);
    void tickString(int tickerId, int tickType, string value);
    void tickGeneric(int tickerId, int field, double value);
    void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate);
    void tickOptionComputation(int tickerId, int field, int tickAttrib, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice);
    void tickSnapshotEnd(int tickerId);
    void nextValidId(int orderId);
    void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract);
    void managedAccounts(string accountsList);
    void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData);
    void updateAccountValue(string key, string value, string currency, string accountName);
    void updatePortfolio(Contract contract, decimal position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName);
    void updateAccountTime(string timestamp);
    void accountDownloadEnd(string account);
    void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice);
    // TWS API 10.19+ widened permId to long. Our handlers override the 64-bit variant.
    void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice, long permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice);
    void openOrder(int orderId, Contract contract, Order order, OrderState orderState);
    void openOrderEnd();
    void contractDetails(int reqId, ContractDetails contractDetails);
    void contractDetailsEnd(int reqId);
    void execDetails(int reqId, Contract contract, Execution execution);
    void execDetailsEnd(int reqId);
    void commissionReport(CommissionReport commissionReport);
    // TWS API 10.19+ renamed commissionReport → commissionAndFeesReport;
    // the real SDK provides both for a transition window, so the stub does too.
    void commissionAndFeesReport(CommissionAndFeesReport commissionAndFeesReport);
    void fundamentalData(int reqId, string data);
    void accountSummary(int reqId, string account, string tag, string value, string currency);
    void accountSummaryEnd(int reqId);
    void familyCodes(FamilyCode[] familyCodes);
    void historicalDataUpdate(int reqId, Bar bar);
    void historicalData(int reqId, Bar bar);
    void historicalDataEnd(int reqId, string start, string end);
    void marketDataType(int reqId, int marketDataType);
    void updateMktDepth(int tickerId, int position, int operation, int side, double price, decimal size);
    void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth);
    void updateNewsBulletin(int msgId, int msgType, string message, string origExchange);
    void position(string account, Contract contract, decimal pos, double avgCost);
    void positionEnd();
    void realtimeBar(int reqId, long time, double open, double high, double low, double close, decimal volume, decimal WAP, int count);
    void scannerParameters(string xml);
    void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr);
    void scannerDataEnd(int reqId);
    void receiveFA(int faDataType, string faXmlData);
    void bondContractDetails(int reqId, ContractDetails contract);
    void verifyMessageAPI(string apiData);
    void verifyCompleted(bool isSuccessful, string errorText);
    void verifyAndAuthMessageAPI(string apiData, string xyzChallenge);
    void verifyAndAuthCompleted(bool isSuccessful, string errorText);
    void displayGroupList(int reqId, string groups);
    void displayGroupUpdated(int reqId, string contractInfo);
    void connectAck();
    void positionMulti(int requestId, string account, string modelCode, Contract contract, decimal pos, double avgCost);
    void positionMultiEnd(int requestId);
    void accountUpdateMulti(int requestId, string account, string modelCode, string key, string value, string currency);
    void accountUpdateMultiEnd(int requestId);
    void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes);
    void securityDefinitionOptionParameterEnd(int reqId);
    void softDollarTiers(int reqId, SoftDollarTier[] tiers);
    void familyCodeList(FamilyCode[] familyCodes);
    void symbolSamples(int reqId, ContractDescription[] contractDescriptions);
    void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions);
    void tickNews2(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData);
    void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap);
    void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions);
    void newsProviders(NewsProvider[] newsProviders);
    void newsArticle(int requestId, int articleType, string articleText);
    void historicalNews(int requestId, string time, string providerCode, string articleId, string headline);
    void historicalNewsEnd(int requestId, bool hasMore);
    void headTimestamp(int reqId, string headTimestamp);
    void histogramData(int reqId, HistogramEntry[] data);
    void rerouteMktDataReq(int reqId, int conId, string exchange);
    void rerouteMktDepthReq(int reqId, int conId, string exchange);
    void marketRule(int marketRuleId, PriceIncrement[] priceIncrements);
    void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL);
    void pnlSingle(int reqId, decimal pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value);
    void historicalTicks(int reqId, HistoricalTick[] ticks, bool done);
    void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done);
    void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done);
    void tickByTickAllLast(int reqId, int tickType, long time, double price, decimal size, TickAttribLast tickAttribLast, string exchange, string specialConditions);
    void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, decimal bidSize, decimal askSize, TickAttribBidAsk tickAttribBidAsk);
    void tickByTickMidPoint(int reqId, long time, double midPoint);
    void orderBound(long orderId, int apiClientId, int apiOrderId);
    void completedOrder(Contract contract, Order order, OrderState orderState);
    void completedOrdersEnd();
    void replaceFAEnd(int reqId, string text);
    void wshMetaData(int reqId, string dataJson);
    void wshEventData(int reqId, string dataJson);
    void historicalSchedule(int reqId, string startDateTime, string endDateTime, string timeZone, HistoricalSession[] sessions);
    void userInfo(int reqId, string whiteBrandingId);
}

// Stub types with minimal properties for compilation
public class EClientSocket
{
    public EClientSocket(EWrapper wrapper, EReaderSignal signal) { }
    public bool IsConnected() => false;
    public void eConnect(string host, int port, int clientId, bool extraAuth = false) { }
    public void eDisconnect() { }
    public void reqCurrentTime() { }
    public void reqIds(int numIds) { }
    public void reqPositions() { }
    public void reqAccountSummary(int requestId, string group, string tags) { }
    public void reqMktData(int requestId, Contract contract, string genericTickList, bool snapshot, bool regulatorySnapshot, List<TagValue>? mktDataOptions) { }
    public void cancelMktData(int requestId) { }
    public void placeOrder(int orderId, Contract contract, Order order) { }
    public void cancelOrder(int orderId, string manualOrderCancelTime = "") { }
    // TWS API 10.19+ switched cancelOrder's second arg to an OrderCancel object.
    public void cancelOrder(int orderId, OrderCancel orderCancel) { }
    public void reqOpenOrders() { }
}

public class EReaderSignal
{
    public void issueSignal() { }
    public bool waitForSignal() => false;
}

public class EReaderMonitorSignal : EReaderSignal { }

public class EReader
{
    public EReader(EClientSocket client, EReaderSignal signal) { }
    public void Start() { }
    public void processMsgs() { }
}

public class TagValue
{
    public string Tag { get; set; } = "";
    public string Value { get; set; } = "";
}

public class TickAttrib
{
    public bool CanAutoExecute { get; set; }
    public bool PastLimit { get; set; }
}
public class TickAttribLast { }
public class TickAttribBidAsk { }
public class DeltaNeutralContract { }

public class Contract
{
    public int ConId { get; set; }
    public string Symbol { get; set; } = "";
    public string SecType { get; set; } = "";
    public string Exchange { get; set; } = "";
    public string Currency { get; set; } = "USD";
    public double Strike { get; set; }
    public string LastTradeDateOrContractMonth { get; set; } = "";
    public string Right { get; set; } = ""; // "C" or "P"
}

public class Order
{
    public string Action { get; set; } = "";
    public decimal TotalQuantity { get; set; }
    public string OrderType { get; set; } = "";
    public string Account { get; set; } = "";
    public double LmtPrice { get; set; }
}

public class OrderState
{
    public string Status { get; set; } = "";
}

public class ContractDetails
{
    public Contract Contract { get; set; } = new Contract();
}

public class Execution
{
    public int OrderId { get; set; }
    public string ExecId { get; set; } = "";
    public string Time { get; set; } = "";
    public string Side { get; set; } = "";
    public decimal Shares { get; set; }
    public decimal Price { get; set; }  // decimal for financial precision (not double)
    public string Exchange { get; set; } = "";
    public int PermId { get; set; }
    public int ClientId { get; set; }
}

public class CommissionReport
{
    public string ExecId { get; set; } = "";
    public double Commission { get; set; }
}

// TWS API 10.19+ renamed CommissionReport → CommissionAndFeesReport with an
// extended field set (fees, yield, yieldRedemptionDate). Stub exposes enough
// surface for compile-time compat; runtime uses the real CSharpAPI.dll.
public class CommissionAndFeesReport
{
    public string ExecId { get; set; } = "";
    public double CommissionAndFees { get; set; }
    public string Currency { get; set; } = "";
    public double RealizedPNL { get; set; }
    public double Yield { get; set; }
    public int YieldRedemptionDate { get; set; }
}

public class Bar { }
public class SoftDollarTier { }
public class FamilyCode { }
public class ContractDescription { }
public class DepthMktDataDescription { }
public class NewsProvider { }
public class HistogramEntry { }
public class PriceIncrement { }
public class HistoricalTick { }
public class HistoricalTickBidAsk { }
public class HistoricalTickLast { }
public class HistoricalSession { }
public class OrderCancel
{
    public string ManualOrderCancelTime { get; set; } = "";
    public string ExtOperator { get; set; } = "";
    public int ManualOrderIndicator { get; set; }
}

/// <summary>
/// Convenience base class matching the real IBKR SDK's DefaultEWrapper: implements every
/// EWrapper method with an empty body so consumer classes can override only what they need
/// (pattern used by TwsCallbackHandler in both TradingSupervisorService and OptionsExecutionService).
/// Keep this stub's method set in sync with the EWrapper interface above — the real SDK includes
/// both, so we mirror both at compile time.
/// </summary>
public class DefaultEWrapper : EWrapper
{
    public virtual void error(Exception e) { }
    public virtual void error(string str) { }
    public virtual void error(int id, int errorCode, string errorMsg, string advancedOrderRejectJson) { }
    public virtual void error(int id, long errorTime, int errorCode, string errorMsg, string advancedOrderRejectJson) { }
    public virtual void connectionClosed() { }
    public virtual void currentTime(long time) { }
    public virtual void tickPrice(int tickerId, int field, double price, TickAttrib attribs) { }
    public virtual void tickSize(int tickerId, int field, decimal size) { }
    public virtual void tickString(int tickerId, int tickType, string value) { }
    public virtual void tickGeneric(int tickerId, int field, double value) { }
    public virtual void tickEFP(int tickerId, int tickType, double basisPoints, string formattedBasisPoints, double impliedFuture, int holdDays, string futureLastTradeDate, double dividendImpact, double dividendsToLastTradeDate) { }
    public virtual void tickOptionComputation(int tickerId, int field, int tickAttrib, double impliedVolatility, double delta, double optPrice, double pvDividend, double gamma, double vega, double theta, double undPrice) { }
    public virtual void tickSnapshotEnd(int tickerId) { }
    public virtual void nextValidId(int orderId) { }
    public virtual void deltaNeutralValidation(int reqId, DeltaNeutralContract deltaNeutralContract) { }
    public virtual void managedAccounts(string accountsList) { }
    public virtual void tickNews(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }
    public virtual void updateAccountValue(string key, string value, string currency, string accountName) { }
    public virtual void updatePortfolio(Contract contract, decimal position, double marketPrice, double marketValue, double averageCost, double unrealizedPNL, double realizedPNL, string accountName) { }
    public virtual void updateAccountTime(string timestamp) { }
    public virtual void accountDownloadEnd(string account) { }
    public virtual void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice) { }
    public virtual void orderStatus(int orderId, string status, decimal filled, decimal remaining, double avgFillPrice, long permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice) { }
    public virtual void openOrder(int orderId, Contract contract, Order order, OrderState orderState) { }
    public virtual void openOrderEnd() { }
    public virtual void contractDetails(int reqId, ContractDetails contractDetails) { }
    public virtual void contractDetailsEnd(int reqId) { }
    public virtual void execDetails(int reqId, Contract contract, Execution execution) { }
    public virtual void execDetailsEnd(int reqId) { }
    public virtual void commissionReport(CommissionReport commissionReport) { }
    public virtual void commissionAndFeesReport(CommissionAndFeesReport commissionAndFeesReport) { }
    public virtual void fundamentalData(int reqId, string data) { }
    public virtual void accountSummary(int reqId, string account, string tag, string value, string currency) { }
    public virtual void accountSummaryEnd(int reqId) { }
    public virtual void familyCodes(FamilyCode[] familyCodes) { }
    public virtual void historicalDataUpdate(int reqId, Bar bar) { }
    public virtual void historicalData(int reqId, Bar bar) { }
    public virtual void historicalDataEnd(int reqId, string start, string end) { }
    public virtual void marketDataType(int reqId, int marketDataType) { }
    public virtual void updateMktDepth(int tickerId, int position, int operation, int side, double price, decimal size) { }
    public virtual void updateMktDepthL2(int tickerId, int position, string marketMaker, int operation, int side, double price, decimal size, bool isSmartDepth) { }
    public virtual void updateNewsBulletin(int msgId, int msgType, string message, string origExchange) { }
    public virtual void position(string account, Contract contract, decimal pos, double avgCost) { }
    public virtual void positionEnd() { }
    public virtual void realtimeBar(int reqId, long time, double open, double high, double low, double close, decimal volume, decimal WAP, int count) { }
    public virtual void scannerParameters(string xml) { }
    public virtual void scannerData(int reqId, int rank, ContractDetails contractDetails, string distance, string benchmark, string projection, string legsStr) { }
    public virtual void scannerDataEnd(int reqId) { }
    public virtual void receiveFA(int faDataType, string faXmlData) { }
    public virtual void bondContractDetails(int reqId, ContractDetails contract) { }
    public virtual void verifyMessageAPI(string apiData) { }
    public virtual void verifyCompleted(bool isSuccessful, string errorText) { }
    public virtual void verifyAndAuthMessageAPI(string apiData, string xyzChallenge) { }
    public virtual void verifyAndAuthCompleted(bool isSuccessful, string errorText) { }
    public virtual void displayGroupList(int reqId, string groups) { }
    public virtual void displayGroupUpdated(int reqId, string contractInfo) { }
    public virtual void connectAck() { }
    public virtual void positionMulti(int requestId, string account, string modelCode, Contract contract, decimal pos, double avgCost) { }
    public virtual void positionMultiEnd(int requestId) { }
    public virtual void accountUpdateMulti(int requestId, string account, string modelCode, string key, string value, string currency) { }
    public virtual void accountUpdateMultiEnd(int requestId) { }
    public virtual void securityDefinitionOptionParameter(int reqId, string exchange, int underlyingConId, string tradingClass, string multiplier, HashSet<string> expirations, HashSet<double> strikes) { }
    public virtual void securityDefinitionOptionParameterEnd(int reqId) { }
    public virtual void softDollarTiers(int reqId, SoftDollarTier[] tiers) { }
    public virtual void familyCodeList(FamilyCode[] familyCodes) { }
    public virtual void symbolSamples(int reqId, ContractDescription[] contractDescriptions) { }
    public virtual void mktDepthExchanges(DepthMktDataDescription[] depthMktDataDescriptions) { }
    public virtual void tickNews2(int tickerId, long timeStamp, string providerCode, string articleId, string headline, string extraData) { }
    public virtual void smartComponents(int reqId, Dictionary<int, KeyValuePair<string, char>> theMap) { }
    public virtual void tickReqParams(int tickerId, double minTick, string bboExchange, int snapshotPermissions) { }
    public virtual void newsProviders(NewsProvider[] newsProviders) { }
    public virtual void newsArticle(int requestId, int articleType, string articleText) { }
    public virtual void historicalNews(int requestId, string time, string providerCode, string articleId, string headline) { }
    public virtual void historicalNewsEnd(int requestId, bool hasMore) { }
    public virtual void headTimestamp(int reqId, string headTimestamp) { }
    public virtual void histogramData(int reqId, HistogramEntry[] data) { }
    public virtual void rerouteMktDataReq(int reqId, int conId, string exchange) { }
    public virtual void rerouteMktDepthReq(int reqId, int conId, string exchange) { }
    public virtual void marketRule(int marketRuleId, PriceIncrement[] priceIncrements) { }
    public virtual void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) { }
    public virtual void pnlSingle(int reqId, decimal pos, double dailyPnL, double unrealizedPnL, double realizedPnL, double value) { }
    public virtual void historicalTicks(int reqId, HistoricalTick[] ticks, bool done) { }
    public virtual void historicalTicksBidAsk(int reqId, HistoricalTickBidAsk[] ticks, bool done) { }
    public virtual void historicalTicksLast(int reqId, HistoricalTickLast[] ticks, bool done) { }
    public virtual void tickByTickAllLast(int reqId, int tickType, long time, double price, decimal size, TickAttribLast tickAttribLast, string exchange, string specialConditions) { }
    public virtual void tickByTickBidAsk(int reqId, long time, double bidPrice, double askPrice, decimal bidSize, decimal askSize, TickAttribBidAsk tickAttribBidAsk) { }
    public virtual void tickByTickMidPoint(int reqId, long time, double midPoint) { }
    public virtual void orderBound(long orderId, int apiClientId, int apiOrderId) { }
    public virtual void completedOrder(Contract contract, Order order, OrderState orderState) { }
    public virtual void completedOrdersEnd() { }
    public virtual void replaceFAEnd(int reqId, string text) { }
    public virtual void wshMetaData(int reqId, string dataJson) { }
    public virtual void wshEventData(int reqId, string dataJson) { }
    public virtual void historicalSchedule(int reqId, string startDateTime, string endDateTime, string timeZone, HistoricalSession[] sessions) { }
    public virtual void userInfo(int reqId, string whiteBrandingId) { }
}

#pragma warning restore CA1040, CA1716, CA1819, CS8618, IDE0060
