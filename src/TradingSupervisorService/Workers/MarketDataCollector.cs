using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;
using TradingSupervisorService.Ibkr;
using TradingSupervisorService.Repositories;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Phase 7.1 Market-data collector.
///
/// Subscribes to SPX, VIX and VIX3M on the CBOE index feed via IBKR and:
///   * every <see cref="_quoteIntervalSeconds"/> (default 15s) queues a
///     <see cref="OutboxEventTypes.MarketQuote"/> event per symbol (OHLCV + close)
///     AND a combined <see cref="OutboxEventTypes.VixSnapshot"/> event
///   * every <see cref="_accountIntervalSeconds"/> (default 60s) fetches the
///     IBKR account summary and queues an <see cref="OutboxEventTypes.AccountEquity"/> event.
///
/// Design notes (mirrors IvtsMonitorWorker):
///   * Waits for IBKR connection before subscribing.
///   * Errors inside a cycle are logged but do NOT crash the worker (retry next cycle).
///   * All numeric formatting uses CultureInfo.InvariantCulture (see ERR-015).
///   * If a symbol has not yet received a fresh tick, its market_quote event is skipped
///     for that cycle rather than emitting stale data (graceful degradation).
///
/// IBKR tick-type reference for tickPrice callbacks:
///   4 = LAST, 6 = HIGH (day), 7 = LOW (day), 9 = CLOSE (prev), 14 = OPEN (day).
/// IBKR tick-type reference for tickSize callbacks:
///   8 = VOLUME (day).
/// </summary>
public sealed class MarketDataCollector : BackgroundService
{
    private readonly ILogger<MarketDataCollector> _logger;
    private readonly IIbkrClient _ibkrClient;
    private readonly IOutboxRepository _outboxRepo;
    private readonly TwsCallbackHandler _callbackHandler;

    private readonly bool _enabled;
    private readonly int _quoteIntervalSeconds;
    private readonly int _accountIntervalSeconds;

    // Request IDs for our market-data subscriptions. Use a range distinct
    // from IvtsMonitor (5001-5004) to avoid collisions.
    private const int ReqIdSpx = 6001;
    private const int ReqIdVix = 6002;
    private const int ReqIdVix3M = 6003;
    private const int ReqIdAccountSummary = 6100;

    // Symbol metadata per reqId
    private readonly Dictionary<int, string> _reqIdToSymbol = new()
    {
        { ReqIdSpx, "SPX" },
        { ReqIdVix, "VIX" },
        { ReqIdVix3M, "VIX3M" }
    };

    // Live OHLCV state per symbol (keyed by symbol for easier reads)
    private readonly ConcurrentDictionary<string, SymbolState> _symbolStates = new();

    // Latest account-equity values, keyed by IBKR tag (e.g. "NetLiquidation", "BuyingPower")
    private readonly ConcurrentDictionary<string, decimal> _accountTags = new(StringComparer.Ordinal);
    private string _accountId = string.Empty;

    private bool _started = false;
    private readonly object _startLock = new();

    public MarketDataCollector(
        ILogger<MarketDataCollector> logger,
        IIbkrClient ibkrClient,
        IOutboxRepository outboxRepo,
        TwsCallbackHandler callbackHandler,
        IConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ibkrClient = ibkrClient ?? throw new ArgumentNullException(nameof(ibkrClient));
        _outboxRepo = outboxRepo ?? throw new ArgumentNullException(nameof(outboxRepo));
        _callbackHandler = callbackHandler ?? throw new ArgumentNullException(nameof(callbackHandler));

        // Read configuration with safe defaults
        _enabled = config.GetValue<bool>("MarketDataCollector:Enabled", true);
        _quoteIntervalSeconds = config.GetValue<int>("MarketDataCollector:QuoteIntervalSeconds", 15);
        _accountIntervalSeconds = config.GetValue<int>("MarketDataCollector:AccountIntervalSeconds", 60);

        // Negative-first validation
        if (_quoteIntervalSeconds <= 0)
        {
            throw new ArgumentException(
                $"Invalid MarketDataCollector:QuoteIntervalSeconds={_quoteIntervalSeconds}. Must be > 0.");
        }

        if (_accountIntervalSeconds <= 0)
        {
            throw new ArgumentException(
                $"Invalid MarketDataCollector:AccountIntervalSeconds={_accountIntervalSeconds}. Must be > 0.");
        }

        // Pre-seed symbol states
        foreach (string sym in _reqIdToSymbol.Values)
        {
            _symbolStates[sym] = new SymbolState();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Early return: worker disabled
        if (!_enabled)
        {
            _logger.LogInformation(
                "{Worker} is disabled in configuration. Not starting.", nameof(MarketDataCollector));
            return;
        }

        _logger.LogInformation(
            "{Worker} started. QuoteInterval={Quote}s AccountInterval={Account}s",
            nameof(MarketDataCollector), _quoteIntervalSeconds, _accountIntervalSeconds);

        // Subscribe to callback hooks for tick data + account summary
        _callbackHandler.TickPriceReceived += OnTickPrice;
        _callbackHandler.TickSizeReceived += OnTickSize;
        _callbackHandler.AccountSummaryReceived += OnAccountSummary;

        try
        {
            // Wait for IBKR connection before issuing subscriptions
            await WaitForIbkrConnectionAsync(stoppingToken);

            if (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation(
                    "{Worker} shutdown requested before subscriptions started.", nameof(MarketDataCollector));
                return;
            }

            // Best-effort subscribe (graceful-degradation: log and continue on failure)
            TrySubscribeAll();

            // Run the two loops (quote cadence + account cadence) concurrently
            Task quoteLoop = RunQuoteLoopAsync(stoppingToken);
            Task accountLoop = RunAccountLoopAsync(stoppingToken);

            await Task.WhenAll(quoteLoop, accountLoop);
        }
        finally
        {
            // Always unhook event handlers on shutdown
            _callbackHandler.TickPriceReceived -= OnTickPrice;
            _callbackHandler.TickSizeReceived -= OnTickSize;
            _callbackHandler.AccountSummaryReceived -= OnAccountSummary;

            // Best-effort cancel of subscriptions
            TryCancelAll();

            _logger.LogInformation("{Worker} stopped", nameof(MarketDataCollector));
        }
    }

    // ---------------------------------------------------------------------
    // Loop: quote cadence (15s default) — emit market_quote + vix_snapshot
    // ---------------------------------------------------------------------
    private async Task RunQuoteLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_quoteIntervalSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Do not throw out of the loop — monitoring workers must survive cycle errors
            try
            {
                await EmitMarketQuotesAsync(ct);
                await EmitVixSnapshotAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw; // cooperative shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Worker} quote-cycle failed; will retry in {Sec}s",
                    nameof(MarketDataCollector), _quoteIntervalSeconds);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Loop: account cadence (60s default) — emit account_equity
    // ---------------------------------------------------------------------
    private async Task RunAccountLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_accountIntervalSeconds), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                // Re-request fresh account summary (IBKR returns async via callbacks)
                if (_ibkrClient.IsConnected)
                {
                    _ibkrClient.RequestAccountSummary(ReqIdAccountSummary);
                }
                else
                {
                    _logger.LogWarning(
                        "{Worker} account cycle: IBKR not connected, skipping request",
                        nameof(MarketDataCollector));
                }

                await EmitAccountEquityAsync(ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Worker} account-cycle failed; will retry in {Sec}s",
                    nameof(MarketDataCollector), _accountIntervalSeconds);
            }
        }
    }

    // ---------------------------------------------------------------------
    // Subscription lifecycle
    // ---------------------------------------------------------------------
    private void TrySubscribeAll()
    {
        // Guard against double-subscribe (worker could in theory restart within the host)
        lock (_startLock)
        {
            if (_started)
            {
                return;
            }
            _started = true;
        }

        foreach ((int reqId, string symbol) in _reqIdToSymbol)
        {
            try
            {
                // CBOE index feed. secType=IND, exchange=CBOE.
                // genericTickList empty -> default set (includes LAST/HIGH/LOW/OPEN/VOLUME via price+size ticks).
                _ibkrClient.RequestMarketData(
                    requestId: reqId,
                    symbol: symbol,
                    secType: "IND",
                    exchange: "CBOE",
                    currency: "USD",
                    genericTickList: "",
                    snapshot: false);

                _logger.LogInformation(
                    "{Worker} subscribed to {Symbol} (reqId={ReqId}, IND@CBOE)",
                    nameof(MarketDataCollector), symbol, reqId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "{Worker} failed to subscribe to {Symbol} (reqId={ReqId}); will run without this feed",
                    nameof(MarketDataCollector), symbol, reqId);
            }
        }

        // Initial account summary kick
        try
        {
            _ibkrClient.RequestAccountSummary(ReqIdAccountSummary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Worker} initial RequestAccountSummary failed", nameof(MarketDataCollector));
        }
    }

    private void TryCancelAll()
    {
        foreach (int reqId in _reqIdToSymbol.Keys)
        {
            try
            {
                _ibkrClient.CancelMarketData(reqId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Error cancelling market data reqId={ReqId} (non-fatal)", reqId);
            }
        }
    }

    // ---------------------------------------------------------------------
    // IBKR connection wait (mirrors IvtsMonitorWorker)
    // ---------------------------------------------------------------------
    private async Task WaitForIbkrConnectionAsync(CancellationToken ct)
    {
        const int maxWaitSeconds = 300;
        int elapsed = 0;

        while (!_ibkrClient.IsConnected && elapsed < maxWaitSeconds)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            _logger.LogDebug("{Worker} waiting for IBKR connection ({Elapsed}s/{Max}s)...",
                nameof(MarketDataCollector), elapsed, maxWaitSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            elapsed += 5;
        }

        if (!_ibkrClient.IsConnected)
        {
            _logger.LogError(
                "{Worker} failed to connect to IBKR after {Timeout}s. Market-data collection will not function.",
                nameof(MarketDataCollector), maxWaitSeconds);
            return;
        }

        _logger.LogInformation("{Worker} IBKR connection established", nameof(MarketDataCollector));
    }

    // ---------------------------------------------------------------------
    // Callbacks (invoked on IBKR reader thread — keep them fast & lock-free where possible)
    // ---------------------------------------------------------------------
    private void OnTickPrice(object? sender, (int ReqId, int Field, double Price) e)
    {
        if (!_reqIdToSymbol.TryGetValue(e.ReqId, out string? symbol))
        {
            return; // not one of our subscriptions
        }

        // Filter out IBKR sentinel values (-1 means "unavailable")
        if (e.Price < 0.0)
        {
            return;
        }

        SymbolState state = _symbolStates[symbol];

        switch (e.Field)
        {
            case 4:   // LAST
                state.SetLast(e.Price);
                break;
            case 6:   // HIGH (day)
                state.SetHigh(e.Price);
                break;
            case 7:   // LOW (day)
                state.SetLow(e.Price);
                break;
            case 9:   // CLOSE (prev day)
                state.SetPrevClose(e.Price);
                break;
            case 14:  // OPEN (day)
                state.SetOpen(e.Price);
                break;
            default:
                // ignore other fields (bid/ask/etc.)
                break;
        }
    }

    private void OnTickSize(object? sender, (int ReqId, int Field, decimal Size) e)
    {
        if (!_reqIdToSymbol.TryGetValue(e.ReqId, out string? symbol))
        {
            return;
        }

        // Field 8 = day volume
        if (e.Field == 8)
        {
            _symbolStates[symbol].SetVolume(e.Size);
        }
    }

    private void OnAccountSummary(object? sender, (int ReqId, string Account, string Tag, string Value, string Currency) e)
    {
        if (e.ReqId != ReqIdAccountSummary)
        {
            return;
        }

        _accountId = e.Account;

        if (decimal.TryParse(e.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v))
        {
            _accountTags[e.Tag] = v;
        }
    }

    // ---------------------------------------------------------------------
    // Outbox emission: market_quote per symbol (only if a fresh tick exists)
    // ---------------------------------------------------------------------
    private async Task EmitMarketQuotesAsync(CancellationToken ct)
    {
        string dateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        foreach ((string symbol, SymbolState state) in _symbolStates)
        {
            SymbolStateSnapshot snapshot = state.Snapshot();

            // Negative-first: no last price yet → skip (do NOT emit stale nulls)
            if (!snapshot.Last.HasValue)
            {
                _logger.LogDebug(
                    "{Worker} no tick yet for {Symbol}; skipping market_quote this cycle",
                    nameof(MarketDataCollector), symbol);
                continue;
            }

            // Build JSON payload with culture-invariant numeric keys for D1 ingest contract.
            object payload = new
            {
                symbol,
                date = dateUtc,
                open = snapshot.Open,
                high = snapshot.High,
                low = snapshot.Low,
                close = snapshot.Last,    // last traded price is the intra-day close-so-far
                prev_close = snapshot.PrevClose,
                volume = snapshot.Volume
            };

            string payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            OutboxEntry entry = new()
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = OutboxEventTypes.MarketQuote,
                PayloadJson = payloadJson,
                // Dedupe: one market_quote per (symbol, date); latest value wins via
                // UPSERT on the Worker side. Cycle-level dedupe is NOT needed because
                // we want each cycle to refresh D1, so include the timestamp second bucket.
                DedupeKey = string.Format(
                    CultureInfo.InvariantCulture,
                    "market_quote:{0}:{1}:{2}",
                    symbol,
                    dateUtc,
                    DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture)),
                Status = "pending",
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow.ToString("O")
            };

            await _outboxRepo.InsertAsync(entry, ct);

            _logger.LogDebug(
                "{Worker} queued market_quote {Symbol} last={Last} high={High} low={Low}",
                nameof(MarketDataCollector), symbol,
                snapshot.Last, snapshot.High, snapshot.Low);
        }
    }

    // ---------------------------------------------------------------------
    // Outbox emission: single vix_snapshot combining VIX + VIX3M
    // ---------------------------------------------------------------------
    private async Task EmitVixSnapshotAsync(CancellationToken ct)
    {
        SymbolStateSnapshot vix = _symbolStates["VIX"].Snapshot();
        SymbolStateSnapshot vix3M = _symbolStates["VIX3M"].Snapshot();

        // Negative-first: we only emit when at least one is populated
        if (!vix.Last.HasValue && !vix3M.Last.HasValue)
        {
            return;
        }

        string dateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        object payload = new
        {
            date = dateUtc,
            vix = vix.Last,
            vix3m = vix3M.Last
        };

        string payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        OutboxEntry entry = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = OutboxEventTypes.VixSnapshot,
            PayloadJson = payloadJson,
            DedupeKey = string.Format(
                CultureInfo.InvariantCulture,
                "vix_snapshot:{0}:{1}",
                dateUtc,
                DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture)),
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _outboxRepo.InsertAsync(entry, ct);

        _logger.LogDebug(
            "{Worker} queued vix_snapshot vix={Vix} vix3m={Vix3m}",
            nameof(MarketDataCollector), vix.Last, vix3M.Last);
    }

    // ---------------------------------------------------------------------
    // Outbox emission: account_equity
    // ---------------------------------------------------------------------
    private async Task EmitAccountEquityAsync(CancellationToken ct)
    {
        // Read latest values for the tags we care about. Missing tags → null.
        decimal? Get(string tag) =>
            _accountTags.TryGetValue(tag, out decimal v) ? v : (decimal?)null;

        decimal? netLiq = Get("NetLiquidation");
        decimal? cash = Get("TotalCashValue");
        decimal? buyingPower = Get("BuyingPower");
        decimal? maintMargin = Get("MaintMarginReq");
        decimal? initMargin = Get("FullInitMarginReq");
        decimal? marginUsed = maintMargin ?? initMargin;

        // Skip emission if we have literally nothing (no callback yet / IBKR stub).
        if (!netLiq.HasValue && !cash.HasValue && !buyingPower.HasValue && !marginUsed.HasValue)
        {
            _logger.LogDebug(
                "{Worker} account_equity: no account tags received yet; skipping emission",
                nameof(MarketDataCollector));
            return;
        }

        string dateUtc = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        object payload = new
        {
            date = dateUtc,
            account_id = string.IsNullOrEmpty(_accountId) ? null : _accountId,
            account_value = netLiq,
            cash = cash,
            buying_power = buyingPower,
            margin_used = marginUsed
        };

        string payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });

        OutboxEntry entry = new()
        {
            EventId = Guid.NewGuid().ToString(),
            EventType = OutboxEventTypes.AccountEquity,
            PayloadJson = payloadJson,
            DedupeKey = string.Format(
                CultureInfo.InvariantCulture,
                "account_equity:{0}:{1}",
                dateUtc,
                DateTime.UtcNow.ToString("HHmm", CultureInfo.InvariantCulture)),
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _outboxRepo.InsertAsync(entry, ct);

        _logger.LogInformation(
            "{Worker} queued account_equity netLiq={NetLiq} cash={Cash} buyingPower={BP} marginUsed={Margin}",
            nameof(MarketDataCollector), netLiq, cash, buyingPower, marginUsed);
    }

    // ---------------------------------------------------------------------
    // Thread-safe per-symbol OHLCV state
    // ---------------------------------------------------------------------
    private sealed class SymbolState
    {
        private readonly object _lock = new();
        private double? _last;
        private double? _open;
        private double? _high;
        private double? _low;
        private double? _prevClose;
        private decimal? _volume;

        public void SetLast(double v)
        {
            lock (_lock)
            {
                _last = v;
                // Also auto-update intra-day high/low if not yet provided by IBKR
                if (!_high.HasValue || v > _high.Value) _high = v;
                if (!_low.HasValue || v < _low.Value) _low = v;
            }
        }

        public void SetOpen(double v) { lock (_lock) _open = v; }
        public void SetHigh(double v) { lock (_lock) _high = v; }
        public void SetLow(double v) { lock (_lock) _low = v; }
        public void SetPrevClose(double v) { lock (_lock) _prevClose = v; }
        public void SetVolume(decimal v) { lock (_lock) _volume = v; }

        public SymbolStateSnapshot Snapshot()
        {
            lock (_lock)
            {
                return new SymbolStateSnapshot(_last, _open, _high, _low, _prevClose, _volume);
            }
        }
    }

    private readonly record struct SymbolStateSnapshot(
        double? Last,
        double? Open,
        double? High,
        double? Low,
        double? PrevClose,
        decimal? Volume);
}
