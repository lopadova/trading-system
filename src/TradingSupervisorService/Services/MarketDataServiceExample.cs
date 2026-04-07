using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;
using SharedKernel.MarketData;

namespace TradingSupervisorService.Services;

/// <summary>
/// Example usage of MarketDataService for integration reference.
/// NOT used in production - for documentation only.
/// </summary>
public sealed class MarketDataServiceExample
{
    private readonly ILogger<MarketDataServiceExample> _logger;
    private readonly IMarketDataService _marketDataService;

    public MarketDataServiceExample(
        ILogger<MarketDataServiceExample> logger,
        IMarketDataService marketDataService)
    {
        _logger = logger;
        _marketDataService = marketDataService;
    }

    /// <summary>
    /// Example 1: Subscribe to SPX index price updates.
    /// </summary>
    public void SubscribeToSPX()
    {
        // Subscribe to SPX (no Greeks needed for index)
        int reqId = _marketDataService.Subscribe(
            symbol: "SPX",
            secType: "IND",
            exchange: "CBOE",
            currency: "USD",
            includeGreeks: false);

        _logger.LogInformation("Subscribed to SPX with reqId={ReqId}", reqId);

        // Later: retrieve latest price
        MarketDataSnapshot? snapshot = _marketDataService.GetSnapshot(reqId);
        if (snapshot?.LastPrice.HasValue == true)
        {
            _logger.LogInformation("SPX last price: {Price:F2}", snapshot.LastPrice.Value);
        }
    }

    /// <summary>
    /// Example 2: Subscribe to VIX3M for volatility monitoring.
    /// </summary>
    public void SubscribeToVIX3M()
    {
        int reqId = _marketDataService.Subscribe(
            symbol: "VIX3M",
            secType: "IND",
            exchange: "CBOE",
            currency: "USD",
            includeGreeks: false);

        _logger.LogInformation("Subscribed to VIX3M with reqId={ReqId}", reqId);
    }

    /// <summary>
    /// Example 3: Subscribe to SPX option with Greeks for IVTS strategy.
    /// </summary>
    public void SubscribeToSPXOption()
    {
        // Subscribe to 30 DTE SPX put option with Greeks
        DateTime expirationDate = DateTime.UtcNow.Date.AddDays(30);
        string expirationDateStr = expirationDate.ToString("yyyyMMdd");

        int reqId = _marketDataService.SubscribeOption(
            symbol: "SPX",
            exchange: "SMART",
            right: "P",
            strike: 4500.0,
            expirationDate: expirationDateStr,
            includeGreeks: true);

        _logger.LogInformation("Subscribed to SPX option with reqId={ReqId}", reqId);

        // Later: retrieve Greeks and IV
        MarketDataSnapshot? snapshot = _marketDataService.GetSnapshot(reqId);
        if (snapshot != null)
        {
            _logger.LogInformation(
                "Option data: DTE={DTE} IV={IV:P2} Delta={Delta:F4} Theta={Theta:F4}",
                snapshot.DaysToExpiration,
                snapshot.ImpliedVolatility,
                snapshot.Delta,
                snapshot.Theta);
        }
    }

    /// <summary>
    /// Example 4: Listen to real-time price updates via events.
    /// </summary>
    public void ListenToRealTimeUpdates()
    {
        // Subscribe to event
        _marketDataService.MarketDataUpdated += OnMarketDataUpdated;

        // Subscribe to a symbol
        _marketDataService.Subscribe("SPX", "IND", "CBOE");

        // Unsubscribe later
        // _marketDataService.MarketDataUpdated -= OnMarketDataUpdated;
    }

    private void OnMarketDataUpdated(object? sender, (int RequestId, MarketDataSnapshot Snapshot) args)
    {
        MarketDataSnapshot snapshot = args.Snapshot;

        _logger.LogInformation(
            "Price update for {Symbol}: Last={Last:F2} Bid={Bid:F2} Ask={Ask:F2} Spread={Spread:F2}",
            snapshot.Symbol,
            snapshot.LastPrice ?? 0,
            snapshot.BidPrice ?? 0,
            snapshot.AskPrice ?? 0,
            snapshot.Spread ?? 0);

        // Check if data is stale (older than 60 seconds)
        if (snapshot.IsStale(maxAgeSeconds: 60))
        {
            _logger.LogWarning("Market data for {Symbol} is stale", snapshot.Symbol);
        }
    }

    /// <summary>
    /// Example 5: Calculate DTE for option expiration dates.
    /// </summary>
    public void CalculateDTEExample()
    {
        // Calculate DTE for a specific expiration date
        string expirationDate = "20260515"; // May 15, 2026

        int? dte = _marketDataService.CalculateDTE(expirationDate);
        if (dte.HasValue)
        {
            _logger.LogInformation("DTE for {ExpDate}: {DTE} days", expirationDate, dte.Value);

            // Check if option is near expiration
            if (dte.Value <= 7)
            {
                _logger.LogWarning("Option is near expiration (DTE={DTE})", dte.Value);
            }
        }
    }

    /// <summary>
    /// Example 6: Campaign manager integration - check entry conditions.
    /// </summary>
    public bool CheckEntryConditionsForIVTS(double minIVRank, int minDTE, int maxDTE)
    {
        // Get SPX price
        MarketDataSnapshot? spxSnapshot = _marketDataService.GetSnapshotBySymbol("SPX");
        if (spxSnapshot == null || !spxSnapshot.HasPriceData)
        {
            _logger.LogWarning("SPX price not available");
            return false;
        }

        // Get VIX3M for IV rank calculation (simplified)
        MarketDataSnapshot? vixSnapshot = _marketDataService.GetSnapshotBySymbol("VIX3M");
        if (vixSnapshot == null || !vixSnapshot.LastPrice.HasValue)
        {
            _logger.LogWarning("VIX3M not available");
            return false;
        }

        // Check IV rank (simplified - real implementation would use historical data)
        double currentVIX = vixSnapshot.LastPrice.Value;
        double estimatedIVRank = currentVIX / 50.0; // Simplified calculation

        if (estimatedIVRank < minIVRank)
        {
            _logger.LogInformation("IV rank {IVRank:P2} below minimum {MinIVRank:P2}", estimatedIVRank, minIVRank);
            return false;
        }

        // Check DTE for target option
        DateTime targetExpiration = DateTime.UtcNow.Date.AddDays(30);
        int? dte = _marketDataService.CalculateDTE(targetExpiration.ToString("yyyyMMdd"));

        if (!dte.HasValue || dte.Value < minDTE || dte.Value > maxDTE)
        {
            _logger.LogInformation("DTE {DTE} outside range [{MinDTE}, {MaxDTE}]", dte, minDTE, maxDTE);
            return false;
        }

        _logger.LogInformation(
            "Entry conditions met: SPX={SPX:F2} IVRank={IVRank:P2} DTE={DTE}",
            spxSnapshot.LastPrice ?? 0,
            estimatedIVRank,
            dte.Value);

        return true;
    }

    /// <summary>
    /// Example 7: Monitor bid/ask spread for liquidity check.
    /// </summary>
    public bool CheckOptionLiquidity(int requestId, double maxSpreadPercent)
    {
        MarketDataSnapshot? snapshot = _marketDataService.GetSnapshot(requestId);
        if (snapshot == null || !snapshot.Spread.HasValue || !snapshot.MidPrice.HasValue)
        {
            _logger.LogWarning("Spread data not available for reqId={ReqId}", requestId);
            return false;
        }

        double spreadPercent = (snapshot.Spread.Value / snapshot.MidPrice.Value) * 100.0;

        if (spreadPercent > maxSpreadPercent)
        {
            _logger.LogWarning(
                "Spread too wide for {Symbol}: {SpreadPercent:F2}% (max {MaxSpread:F2}%)",
                snapshot.Symbol,
                spreadPercent,
                maxSpreadPercent);
            return false;
        }

        _logger.LogInformation(
            "Liquidity check passed for {Symbol}: spread={SpreadPercent:F2}%",
            snapshot.Symbol,
            spreadPercent);

        return true;
    }

    /// <summary>
    /// Example 8: Cleanup when shutting down.
    /// </summary>
    public void Cleanup()
    {
        // Get all active subscriptions
        IReadOnlyDictionary<int, string> subscriptions = _marketDataService.GetActiveSubscriptions();

        _logger.LogInformation("Cleaning up {Count} market data subscriptions", subscriptions.Count);

        // Unsubscribe from all
        foreach (int reqId in subscriptions.Keys)
        {
            _marketDataService.Unsubscribe(reqId);
        }

        _logger.LogInformation("All subscriptions canceled");
    }
}
