// STUB IMPLEMENTATION - Replace with actual IBApi from TWS/IB Gateway installation
// This is a minimal stub to allow compilation. User must install actual IBApi.dll later.

namespace IBApi;

#pragma warning disable CA1040, CA1716, CA1819, CS8618, IDE0060

public interface EWrapper
{
    void error(Exception e);
    void error(string str);
    void error(int id, int errorCode, string errorMsg, string advancedOrderRejectJson);
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
    public string Side { get; set; } = "";
    public decimal Shares { get; set; }
    public double Price { get; set; }
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

#pragma warning restore CA1040, CA1716, CA1819, CS8618, IDE0060
