using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;
using SharedKernel.MarketData;

namespace TradingSupervisorService.Services;

/// <summary>
/// Market data service with real-time IBKR subscriptions and thread-safe caching.
/// Provides price updates, IV data, and DTE calculations for options.
/// </summary>
public sealed class MarketDataService : IMarketDataService, IDisposable
{
    private readonly ILogger<MarketDataService> _logger;
    private readonly IIbkrClient _ibkrClient;

    // Thread-safe storage for market data snapshots
    private readonly ConcurrentDictionary<int, MarketDataSnapshot> _snapshotsByRequestId = new();
    private readonly ConcurrentDictionary<string, int> _requestIdsBySymbol = new();
    private readonly ConcurrentDictionary<int, string> _symbolsByRequestId = new();
    private readonly ConcurrentDictionary<int, string> _expirationDatesByRequestId = new();

    // Thread-safe request ID counter
    private int _nextRequestId = 1000;
    private readonly object _requestIdLock = new();

    private bool _disposed = false;

    public event EventHandler<(int RequestId, MarketDataSnapshot Snapshot)>? MarketDataUpdated;

    public MarketDataService(ILogger<MarketDataService> logger, IIbkrClient ibkrClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ibkrClient = ibkrClient ?? throw new ArgumentNullException(nameof(ibkrClient));

        _logger.LogInformation("MarketDataService initialized");
    }

    public int Subscribe(string symbol, string secType, string exchange, string currency = "USD", bool includeGreeks = false)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MarketDataService));
        }

        // Validate inputs (negative-first conditionals)
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(secType))
        {
            throw new ArgumentException("SecType cannot be empty", nameof(secType));
        }

        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException("Exchange cannot be empty", nameof(exchange));
        }

        // Check if already subscribed to this symbol
        if (_requestIdsBySymbol.TryGetValue(symbol, out int existingReqId))
        {
            _logger.LogDebug("Already subscribed to {Symbol}, returning existing reqId={ReqId}", symbol, existingReqId);
            return existingReqId;
        }

        // Allocate new request ID
        int requestId = GetNextRequestId();

        // Request market data from IBKR
        string genericTickList = includeGreeks ? "106" : "";

        try
        {
            _ibkrClient.RequestMarketData(requestId, symbol, secType, exchange, currency, genericTickList, snapshot: false);

            // Register subscription
            _symbolsByRequestId[requestId] = symbol;
            _requestIdsBySymbol[symbol] = requestId;

            // Initialize empty snapshot
            MarketDataSnapshot initialSnapshot = MarketDataSnapshot.Empty(symbol, secType, requestId);
            _snapshotsByRequestId[requestId] = initialSnapshot;

            _logger.LogInformation(
                "Subscribed to market data: symbol={Symbol} secType={SecType} exchange={Exchange} reqId={ReqId} greeks={Greeks}",
                symbol, secType, exchange, requestId, includeGreeks);

            return requestId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to market data for {Symbol}", symbol);
            // Cleanup partial state
            _symbolsByRequestId.TryRemove(requestId, out _);
            _requestIdsBySymbol.TryRemove(symbol, out _);
            throw;
        }
    }

    public int SubscribeOption(
        string symbol,
        string exchange,
        string right,
        double strike,
        string expirationDate,
        bool includeGreeks = true)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MarketDataService));
        }

        // Validate inputs (negative-first conditionals)
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(exchange))
        {
            throw new ArgumentException("Exchange cannot be empty", nameof(exchange));
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            throw new ArgumentException("Right cannot be empty", nameof(right));
        }

        if (strike <= 0)
        {
            throw new ArgumentException("Strike must be positive", nameof(strike));
        }

        if (!IsValidExpirationDate(expirationDate))
        {
            throw new ArgumentException($"Invalid expiration date format: {expirationDate}. Expected YYYYMMDD.", nameof(expirationDate));
        }

        // Allocate new request ID
        int requestId = GetNextRequestId();

        // Build option contract symbol for tracking
        string optionSymbol = $"{symbol}_{expirationDate}_{right}_{strike}";

        // Store expiration date for DTE calculation
        _expirationDatesByRequestId[requestId] = expirationDate;

        _logger.LogInformation(
            "Subscribing to option: symbol={Symbol} strike={Strike} exp={Exp} right={Right} reqId={ReqId}",
            symbol, strike, expirationDate, right, requestId);

        // Note: IIbkrClient.RequestMarketData doesn't support option-specific parameters yet
        // This is a simplified version. Full implementation would require extending IIbkrClient
        // to accept Contract with LastTradeDateOrContractMonth, Strike, Right fields.
        // For now, use the basic RequestMarketData with the option symbol.

        try
        {
            // Generic tick list "106" includes option Greeks
            string genericTickList = includeGreeks ? "106" : "";
            _ibkrClient.RequestMarketData(requestId, symbol, "OPT", exchange, "USD", genericTickList, snapshot: false);

            // Register subscription
            _symbolsByRequestId[requestId] = optionSymbol;
            _requestIdsBySymbol[optionSymbol] = requestId;

            // Initialize empty snapshot with expiration info
            MarketDataSnapshot initialSnapshot = MarketDataSnapshot.Empty(optionSymbol, "OPT", requestId) with
            {
                ExpirationDate = ParseExpirationDate(expirationDate),
                DaysToExpiration = CalculateDTE(expirationDate)
            };
            _snapshotsByRequestId[requestId] = initialSnapshot;

            _logger.LogInformation(
                "Subscribed to option market data: {OptionSymbol} reqId={ReqId}",
                optionSymbol, requestId);

            return requestId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to subscribe to option market data for {OptionSymbol}", optionSymbol);
            // Cleanup partial state
            _symbolsByRequestId.TryRemove(requestId, out _);
            _requestIdsBySymbol.TryRemove(optionSymbol, out _);
            _expirationDatesByRequestId.TryRemove(requestId, out _);
            throw;
        }
    }

    public void Unsubscribe(int requestId)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MarketDataService));
        }

        if (!_symbolsByRequestId.TryGetValue(requestId, out string? symbol))
        {
            _logger.LogWarning("Cannot unsubscribe: requestId={ReqId} not found", requestId);
            return;
        }

        try
        {
            _ibkrClient.CancelMarketData(requestId);

            // Remove from all tracking dictionaries
            _symbolsByRequestId.TryRemove(requestId, out _);
            _requestIdsBySymbol.TryRemove(symbol, out _);
            _snapshotsByRequestId.TryRemove(requestId, out _);
            _expirationDatesByRequestId.TryRemove(requestId, out _);

            _logger.LogInformation("Unsubscribed from market data: symbol={Symbol} reqId={ReqId}", symbol, requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unsubscribe from market data: reqId={ReqId}", requestId);
            throw;
        }
    }

    public MarketDataSnapshot? GetSnapshot(int requestId)
    {
        return _snapshotsByRequestId.TryGetValue(requestId, out MarketDataSnapshot? snapshot)
            ? snapshot
            : null;
    }

    public MarketDataSnapshot? GetSnapshotBySymbol(string symbol)
    {
        // Validate input (negative-first conditional)
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        if (!_requestIdsBySymbol.TryGetValue(symbol, out int requestId))
        {
            return null;
        }

        return GetSnapshot(requestId);
    }

    public IReadOnlyDictionary<int, string> GetActiveSubscriptions()
    {
        return _symbolsByRequestId.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public int? CalculateDTE(string expirationDate)
    {
        DateTime? expDate = ParseExpirationDate(expirationDate);
        if (!expDate.HasValue)
        {
            return null;
        }

        // DTE = days from today to expiration (inclusive)
        // If expiration is today, DTE = 0
        int dte = (int)(expDate.Value.Date - DateTime.UtcNow.Date).TotalDays;
        return Math.Max(0, dte); // Never negative
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _logger.LogInformation("Disposing MarketDataService");

        // Cancel all subscriptions
        foreach (int requestId in _symbolsByRequestId.Keys.ToList())
        {
            try
            {
                _ibkrClient.CancelMarketData(requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling market data subscription: reqId={ReqId}", requestId);
            }
        }

        // Clear all caches
        _snapshotsByRequestId.Clear();
        _requestIdsBySymbol.Clear();
        _symbolsByRequestId.Clear();
        _expirationDatesByRequestId.Clear();

        _disposed = true;
    }

    #region Callback Integration (to be wired from TwsCallbackHandler)

    /// <summary>
    /// Updates the snapshot with new price data from IBKR tickPrice callback.
    /// This method should be called from TwsCallbackHandler.
    /// </summary>
    public void OnTickPrice(int requestId, int field, double price)
    {
        if (!_snapshotsByRequestId.TryGetValue(requestId, out MarketDataSnapshot? currentSnapshot))
        {
            _logger.LogWarning("Received tick price for unknown requestId={ReqId}", requestId);
            return;
        }

        // Update snapshot based on tick field
        // TickType: 1=BID, 2=ASK, 4=LAST, 6=HIGH, 7=LOW, 9=CLOSE
        MarketDataSnapshot updatedSnapshot = field switch
        {
            1 => currentSnapshot with { BidPrice = price, TimestampUtc = DateTime.UtcNow },
            2 => currentSnapshot with { AskPrice = price, TimestampUtc = DateTime.UtcNow },
            4 => currentSnapshot with { LastPrice = price, TimestampUtc = DateTime.UtcNow },
            _ => currentSnapshot with { TimestampUtc = DateTime.UtcNow }
        };

        _snapshotsByRequestId[requestId] = updatedSnapshot;

        _logger.LogDebug(
            "Price update: reqId={ReqId} symbol={Symbol} field={Field} price={Price:F2}",
            requestId, currentSnapshot.Symbol, field, price);

        // Raise event for subscribers
        MarketDataUpdated?.Invoke(this, (requestId, updatedSnapshot));
    }

    /// <summary>
    /// Updates the snapshot with new size data from IBKR tickSize callback.
    /// </summary>
    public void OnTickSize(int requestId, int field, decimal size)
    {
        if (!_snapshotsByRequestId.TryGetValue(requestId, out MarketDataSnapshot? currentSnapshot))
        {
            return;
        }

        // TickType: 0=BID_SIZE, 3=ASK_SIZE, 5=LAST_SIZE, 8=VOLUME
        MarketDataSnapshot updatedSnapshot = field switch
        {
            0 => currentSnapshot with { BidSize = size, TimestampUtc = DateTime.UtcNow },
            3 => currentSnapshot with { AskSize = size, TimestampUtc = DateTime.UtcNow },
            _ => currentSnapshot with { TimestampUtc = DateTime.UtcNow }
        };

        _snapshotsByRequestId[requestId] = updatedSnapshot;
    }

    /// <summary>
    /// Updates the snapshot with option Greeks from IBKR tickOptionComputation callback.
    /// </summary>
    public void OnTickOptionComputation(
        int requestId,
        int field,
        double impliedVolatility,
        double delta,
        double optPrice,
        double gamma,
        double vega,
        double theta,
        double undPrice)
    {
        if (!_snapshotsByRequestId.TryGetValue(requestId, out MarketDataSnapshot? currentSnapshot))
        {
            return;
        }

        // Update snapshot with Greeks
        // Filter out invalid values (IBKR sends -1 or -2 for unavailable data)
        MarketDataSnapshot updatedSnapshot = currentSnapshot with
        {
            ImpliedVolatility = impliedVolatility > 0 ? impliedVolatility : currentSnapshot.ImpliedVolatility,
            Delta = delta > -2 ? delta : currentSnapshot.Delta,
            Gamma = gamma >= 0 ? gamma : currentSnapshot.Gamma,
            Vega = vega >= 0 ? vega : currentSnapshot.Vega,
            Theta = theta > -999 ? theta : currentSnapshot.Theta,
            UnderlyingPrice = undPrice > 0 ? undPrice : currentSnapshot.UnderlyingPrice,
            TimestampUtc = DateTime.UtcNow
        };

        _snapshotsByRequestId[requestId] = updatedSnapshot;

        _logger.LogDebug(
            "Greeks update: reqId={ReqId} symbol={Symbol} IV={IV:F4} delta={Delta:F4} gamma={Gamma:F4}",
            requestId, currentSnapshot.Symbol, impliedVolatility, delta, gamma);

        // Raise event for subscribers
        MarketDataUpdated?.Invoke(this, (requestId, updatedSnapshot));
    }

    #endregion

    #region Private Methods

    private int GetNextRequestId()
    {
        lock (_requestIdLock)
        {
            return _nextRequestId++;
        }
    }

    private static bool IsValidExpirationDate(string expirationDate)
    {
        return ParseExpirationDate(expirationDate).HasValue;
    }

    private static DateTime? ParseExpirationDate(string expirationDate)
    {
        // Expiration date format: YYYYMMDD
        if (string.IsNullOrWhiteSpace(expirationDate))
        {
            return null;
        }

        if (expirationDate.Length != 8)
        {
            return null;
        }

        if (!DateTime.TryParseExact(
            expirationDate,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime parsedDate))
        {
            return null;
        }

        return parsedDate;
    }

    #endregion
}
