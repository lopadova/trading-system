using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using TradingSupervisorService.Ibkr;
using TradingSupervisorService.Repositories;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Background service that monitors position Greeks in real-time.
/// Reads active positions with Greeks data from options.db and creates alerts
/// when risk thresholds are breached (high delta, gamma, theta, vega).
/// Runs on a configurable interval (default: 60 seconds).
///
/// Phase 7.1 upgrade: when LiveTicks are enabled AND the live-wiring dependencies
/// (IIbkrClient, TwsCallbackHandler, IDbConnectionFactory) are supplied, the worker
/// also subscribes each open position to the IBKR tick stream with generic ticks
/// "106,100" (option IV + greeks). Tick callbacks update a local
/// <c>position_greeks_cache</c> table and queue a <see cref="OutboxEventTypes.PositionGreeks"/>
/// Outbox event. Threshold detection then prefers the cached (fresher) values over
/// the stale options.db mirror.
///
/// The live dependencies are optional: when omitted (as in most unit tests) the
/// worker degrades to the original DB-only threshold-check behavior, preserving
/// backwards compatibility (see ERR-005 on constructor evolution discipline).
/// </summary>
public sealed class GreeksMonitorWorker : BackgroundService
{
    private readonly ILogger<GreeksMonitorWorker> _logger;
    private readonly IPositionsRepository _positionsRepo;
    private readonly IAlertRepository _alertRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly IIbkrClient? _ibkrClient;
    private readonly TwsCallbackHandler? _callbackHandler;
    private readonly IDbConnectionFactory? _dbFactory;
    private readonly int _intervalSeconds;
    private readonly bool _enabled;
    private readonly bool _liveTicksEnabled;

    // Risk thresholds (configurable)
    private readonly double _deltaThreshold;
    private readonly double _gammaThreshold;
    private readonly double _thetaThreshold;  // absolute value (theta is negative)
    private readonly double _vegaThreshold;

    // Live-tick state --------------------------------------------------------

    // Base reqId for position tick subscriptions. Uses range 7000+ to avoid
    // colliding with IvtsMonitor (5001-5004) and MarketDataCollector (6001-6100).
    private const int PositionReqIdBase = 7000;
    private int _nextPositionReqId = PositionReqIdBase;
    private readonly object _reqIdLock = new();

    // position_id → reqId (so we know which IBKR subscription feeds which position)
    private readonly ConcurrentDictionary<string, int> _positionToReqId = new();
    // reqId → position_id (reverse lookup for tick callback)
    private readonly ConcurrentDictionary<int, string> _reqIdToPosition = new();
    // position_id → latest cached Greeks (used by threshold check; never null for a known position)
    private readonly ConcurrentDictionary<string, CachedGreeks> _liveGreeks = new();
    // position_id → contract symbol (diagnostics)
    private readonly ConcurrentDictionary<string, string> _positionSymbols = new();

    private bool _callbackSubscribed = false;

    /// <summary>
    /// Primary DI-friendly constructor. Live dependencies are optional so existing
    /// unit tests (which pre-date Phase 7.1) keep compiling unchanged.
    /// </summary>
    public GreeksMonitorWorker(
        ILogger<GreeksMonitorWorker> logger,
        IPositionsRepository positionsRepo,
        IAlertRepository alertRepo,
        IOutboxRepository outboxRepo,
        IConfiguration config,
        IIbkrClient? ibkrClient = null,
        TwsCallbackHandler? callbackHandler = null,
        IDbConnectionFactory? dbFactory = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _positionsRepo = positionsRepo ?? throw new ArgumentNullException(nameof(positionsRepo));
        _alertRepo = alertRepo ?? throw new ArgumentNullException(nameof(alertRepo));
        _outboxRepo = outboxRepo ?? throw new ArgumentNullException(nameof(outboxRepo));
        _ibkrClient = ibkrClient;
        _callbackHandler = callbackHandler;
        _dbFactory = dbFactory;

        // Read configuration with safe defaults
        _enabled = config.GetValue<bool>("GreeksMonitor:Enabled", true);
        _intervalSeconds = config.GetValue<int>("GreeksMonitor:IntervalSeconds", 60);
        _deltaThreshold = config.GetValue<double>("GreeksMonitor:DeltaThreshold", 0.70);  // 70 delta
        _gammaThreshold = config.GetValue<double>("GreeksMonitor:GammaThreshold", 0.05);  // 0.05 gamma
        _thetaThreshold = config.GetValue<double>("GreeksMonitor:ThetaThreshold", 50.0);  // $50/day decay
        _vegaThreshold = config.GetValue<double>("GreeksMonitor:VegaThreshold", 100.0);   // $100 per 1% IV change
        _liveTicksEnabled = config.GetValue<bool>("GreeksMonitor:LiveTicksEnabled", true)
                            && _ibkrClient is not null
                            && _callbackHandler is not null
                            && _dbFactory is not null;

        // Validate configuration (negative-first)
        if (_intervalSeconds <= 0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:IntervalSeconds={_intervalSeconds}. Must be > 0.");
        }

        if (_deltaThreshold < 0.0 || _deltaThreshold > 1.0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:DeltaThreshold={_deltaThreshold}. Must be 0.0-1.0.");
        }

        if (_gammaThreshold <= 0.0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:GammaThreshold={_gammaThreshold}. Must be > 0.");
        }

        if (_thetaThreshold <= 0.0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:ThetaThreshold={_thetaThreshold}. Must be > 0.");
        }

        if (_vegaThreshold <= 0.0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:VegaThreshold={_vegaThreshold}. Must be > 0.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Early return: if Greeks monitoring is disabled, exit immediately
        if (!_enabled)
        {
            _logger.LogInformation("{Worker} is disabled in configuration. Not starting.", nameof(GreeksMonitorWorker));
            return;
        }

        _logger.LogInformation(
            "{Worker} started. Interval={Interval}s, DeltaThreshold={Delta:F2}, GammaThreshold={Gamma:F3}, ThetaThreshold=${Theta:F0}, VegaThreshold=${Vega:F0}, LiveTicks={Live}",
            nameof(GreeksMonitorWorker), _intervalSeconds, _deltaThreshold, _gammaThreshold, _thetaThreshold, _vegaThreshold, _liveTicksEnabled);

        // Hook up tickOptionComputation callback if live ticks are enabled
        if (_liveTicksEnabled && _callbackHandler is not null)
        {
            _callbackHandler.TickOptionComputationReceived += OnTickOptionComputation;
            _callbackSubscribed = true;
        }

        try
        {
            // Main loop: monitor Greeks on interval
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCycleAsync(stoppingToken);

                // Wait for next cycle (with cancellation support)
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown - exit loop
                    break;
                }
            }
        }
        finally
        {
            if (_callbackSubscribed && _callbackHandler is not null)
            {
                _callbackHandler.TickOptionComputationReceived -= OnTickOptionComputation;
                _callbackSubscribed = false;
            }

            // Best-effort cancel of any open position subscriptions
            if (_liveTicksEnabled && _ibkrClient is not null)
            {
                foreach (int reqId in _reqIdToPosition.Keys.ToList())
                {
                    try
                    {
                        _ibkrClient.CancelMarketData(reqId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error cancelling greeks reqId={ReqId} (non-fatal)", reqId);
                    }
                }
            }

            _logger.LogInformation("{Worker} stopped", nameof(GreeksMonitorWorker));
        }
    }

    /// <summary>
    /// Executes one monitoring cycle.
    /// Fetches all active positions with Greeks, checks thresholds, creates alerts if needed.
    /// Does NOT throw - any errors are logged and the worker continues to next cycle.
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            // Fetch all active positions with Greeks data
            IReadOnlyList<ActivePositionRecord> positions = await _positionsRepo.GetActivePositionsWithGreeksAsync(ct);

            // Phase 7.1: keep IBKR tick subscriptions in sync with the active-positions set
            // (new positions → subscribe, closed positions → cancel).
            if (_liveTicksEnabled)
            {
                SyncPositionSubscriptions(positions);
            }

            // Early return: no positions to monitor
            if (positions.Count == 0)
            {
                _logger.LogDebug("{Worker} cycle: no active positions with Greeks data", nameof(GreeksMonitorWorker));
                return;
            }

            _logger.LogDebug("{Worker} cycle: monitoring {Count} positions", nameof(GreeksMonitorWorker), positions.Count);

            // Check each position for risk threshold breaches
            int alertsCreated = 0;

            foreach (ActivePositionRecord dbPosition in positions)
            {
                // Prefer live-tick Greeks when we have them (fresher than the options.db mirror)
                ActivePositionRecord position = MergeWithLiveGreeks(dbPosition);

                // Check Delta threshold (directional risk)
                if (position.Delta.HasValue && Math.Abs(position.Delta.Value) > _deltaThreshold)
                {
                    await CreateDeltaAlertAsync(position, ct);
                    alertsCreated++;
                }

                // Check Gamma threshold (convexity risk)
                if (position.Gamma.HasValue && position.Gamma.Value > _gammaThreshold)
                {
                    await CreateGammaAlertAsync(position, ct);
                    alertsCreated++;
                }

                // Check Theta threshold (time decay risk)
                // Theta is usually negative, so compare absolute value
                if (position.Theta.HasValue && Math.Abs(position.Theta.Value) > _thetaThreshold)
                {
                    await CreateThetaAlertAsync(position, ct);
                    alertsCreated++;
                }

                // Check Vega threshold (volatility risk)
                if (position.Vega.HasValue && position.Vega.Value > _vegaThreshold)
                {
                    await CreateVegaAlertAsync(position, ct);
                    alertsCreated++;
                }
            }

            if (alertsCreated > 0)
            {
                _logger.LogInformation("{Worker} cycle completed: {AlertCount} alerts created from {PositionCount} positions",
                    nameof(GreeksMonitorWorker), alertsCreated, positions.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown - do not log as error
            _logger.LogDebug("{Worker} cycle cancelled (shutdown requested)", nameof(GreeksMonitorWorker));
        }
        catch (Exception ex)
        {
            // Log error but DO NOT rethrow - worker must survive cycle failures
            _logger.LogError(ex, "{Worker} cycle failed. Will retry in {Interval}s",
                nameof(GreeksMonitorWorker), _intervalSeconds);
        }
    }

    /// <summary>
    /// Creates an alert for high delta (directional risk).
    /// Delta > threshold indicates high directional exposure.
    /// </summary>
    private async Task CreateDeltaAlertAsync(ActivePositionRecord position, CancellationToken ct)
    {
        // Build alert details JSON
        var details = new
        {
            position_id = position.PositionId,
            campaign_id = position.CampaignId,
            symbol = position.Symbol,
            contract_symbol = position.ContractSymbol,
            quantity = position.Quantity,
            delta = position.Delta,
            threshold = _deltaThreshold,
            underlying_price = position.UnderlyingPrice,
            greeks_updated_at = position.GreeksUpdatedAt
        };

        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "GreeksDelta",
            Severity = AlertSeverity.Warning.ToString().ToLowerInvariant(),
            Message = string.Format(CultureInfo.InvariantCulture,
                "High delta risk: position {0} has delta {1:F2} (threshold {2:F2})",
                position.ContractSymbol, position.Delta, _deltaThreshold),
            DetailsJson = JsonSerializer.Serialize(details),
            SourceService = "TradingSupervisorService",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        await _alertRepo.InsertAsync(alert, ct);

        // Create outbox entry for remote sync
        string eventId = Guid.NewGuid().ToString();
        string payloadJson = JsonSerializer.Serialize(alert, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        OutboxEntry outboxEntry = new()
        {
            EventId = eventId,
            EventType = "alert",
            PayloadJson = payloadJson,
            DedupeKey = $"alert:{alert.AlertType}:{alert.AlertId}",
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _outboxRepo.InsertAsync(outboxEntry, ct);

        _logger.LogWarning(
            "Delta threshold breach: position {PositionId} ({Symbol}) delta={Delta:F2} > threshold={Threshold:F2}",
            position.PositionId, position.Symbol, position.Delta, _deltaThreshold);
    }

    /// <summary>
    /// Creates an alert for high gamma (convexity risk).
    /// High gamma means delta changes rapidly with underlying price movement.
    /// </summary>
    private async Task CreateGammaAlertAsync(ActivePositionRecord position, CancellationToken ct)
    {
        var details = new
        {
            position_id = position.PositionId,
            campaign_id = position.CampaignId,
            symbol = position.Symbol,
            contract_symbol = position.ContractSymbol,
            quantity = position.Quantity,
            gamma = position.Gamma,
            threshold = _gammaThreshold,
            underlying_price = position.UnderlyingPrice,
            greeks_updated_at = position.GreeksUpdatedAt
        };

        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "GreeksGamma",
            Severity = AlertSeverity.Warning.ToString().ToLowerInvariant(),
            Message = string.Format(CultureInfo.InvariantCulture,
                "High gamma risk: position {0} has gamma {1:F3} (threshold {2:F3})",
                position.ContractSymbol, position.Gamma, _gammaThreshold),
            DetailsJson = JsonSerializer.Serialize(details),
            SourceService = "TradingSupervisorService",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        await _alertRepo.InsertAsync(alert, ct);

        // Create outbox entry for remote sync
        string eventId = Guid.NewGuid().ToString();
        string payloadJson = JsonSerializer.Serialize(alert, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        OutboxEntry outboxEntry = new()
        {
            EventId = eventId,
            EventType = "alert",
            PayloadJson = payloadJson,
            DedupeKey = $"alert:{alert.AlertType}:{alert.AlertId}",
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _outboxRepo.InsertAsync(outboxEntry, ct);

        _logger.LogWarning(
            "Gamma threshold breach: position {PositionId} ({Symbol}) gamma={Gamma:F3} > threshold={Threshold:F3}",
            position.PositionId, position.Symbol, position.Gamma, _gammaThreshold);
    }

    /// <summary>
    /// Creates an alert for high theta (time decay risk).
    /// High absolute theta means position loses significant value each day.
    /// </summary>
    private async Task CreateThetaAlertAsync(ActivePositionRecord position, CancellationToken ct)
    {
        var details = new
        {
            position_id = position.PositionId,
            campaign_id = position.CampaignId,
            symbol = position.Symbol,
            contract_symbol = position.ContractSymbol,
            quantity = position.Quantity,
            theta = position.Theta,
            threshold = _thetaThreshold,
            underlying_price = position.UnderlyingPrice,
            greeks_updated_at = position.GreeksUpdatedAt
        };

        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "GreeksTheta",
            Severity = AlertSeverity.Warning.ToString().ToLowerInvariant(),
            Message = string.Format(CultureInfo.InvariantCulture,
                "High theta decay: position {0} has theta ${1:F0}/day (threshold ${2:F0})",
                position.ContractSymbol, position.Theta, _thetaThreshold),
            DetailsJson = JsonSerializer.Serialize(details),
            SourceService = "TradingSupervisorService",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        await _alertRepo.InsertAsync(alert, ct);

        // Create outbox entry for remote sync
        string eventId = Guid.NewGuid().ToString();
        string payloadJson = JsonSerializer.Serialize(alert, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        OutboxEntry outboxEntry = new()
        {
            EventId = eventId,
            EventType = "alert",
            PayloadJson = payloadJson,
            DedupeKey = $"alert:{alert.AlertType}:{alert.AlertId}",
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _outboxRepo.InsertAsync(outboxEntry, ct);

        _logger.LogWarning(
            "Theta threshold breach: position {PositionId} ({Symbol}) theta=${Theta:F0}/day > threshold=${Threshold:F0}",
            position.PositionId, position.Symbol, Math.Abs(position.Theta!.Value), _thetaThreshold);
    }

    /// <summary>
    /// Creates an alert for high vega (volatility risk).
    /// High vega means position value is highly sensitive to IV changes.
    /// </summary>
    private async Task CreateVegaAlertAsync(ActivePositionRecord position, CancellationToken ct)
    {
        var details = new
        {
            position_id = position.PositionId,
            campaign_id = position.CampaignId,
            symbol = position.Symbol,
            contract_symbol = position.ContractSymbol,
            quantity = position.Quantity,
            vega = position.Vega,
            threshold = _vegaThreshold,
            implied_volatility = position.ImpliedVolatility,
            underlying_price = position.UnderlyingPrice,
            greeks_updated_at = position.GreeksUpdatedAt
        };

        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "GreeksVega",
            Severity = AlertSeverity.Warning.ToString().ToLowerInvariant(),
            Message = string.Format(CultureInfo.InvariantCulture,
                "High vega risk: position {0} has vega ${1:F0} (threshold ${2:F0})",
                position.ContractSymbol, position.Vega, _vegaThreshold),
            DetailsJson = JsonSerializer.Serialize(details),
            SourceService = "TradingSupervisorService",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        await _alertRepo.InsertAsync(alert, ct);

        // Create outbox entry for remote sync
        string eventId = Guid.NewGuid().ToString();
        string payloadJson = JsonSerializer.Serialize(alert, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        OutboxEntry outboxEntry = new()
        {
            EventId = eventId,
            EventType = "alert",
            PayloadJson = payloadJson,
            DedupeKey = $"alert:{alert.AlertType}:{alert.AlertId}",
            Status = "pending",
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _outboxRepo.InsertAsync(outboxEntry, ct);

        _logger.LogWarning(
            "Vega threshold breach: position {PositionId} ({Symbol}) vega=${Vega:F0} > threshold=${Threshold:F0}",
            position.PositionId, position.Symbol, position.Vega, _vegaThreshold);
    }

    // =========================================================================
    // Phase 7.1: Live-tick Greeks wiring
    // =========================================================================

    /// <summary>
    /// Returns a copy of <paramref name="dbPosition"/> where Greeks are overwritten
    /// by the live-cache values when available. Falls through to the DB values
    /// (original behavior) when no live tick has arrived for this position.
    /// </summary>
    private ActivePositionRecord MergeWithLiveGreeks(ActivePositionRecord dbPosition)
    {
        if (!_liveGreeks.TryGetValue(dbPosition.PositionId, out CachedGreeks? cached))
        {
            return dbPosition;
        }

        // Consider "fresh" if the last tick is within 2 cycles
        TimeSpan maxAge = TimeSpan.FromSeconds(_intervalSeconds * 2);
        if (DateTime.UtcNow - cached.TimestampUtc > maxAge)
        {
            return dbPosition;
        }

        return dbPosition with
        {
            Delta = cached.Delta ?? dbPosition.Delta,
            Gamma = cached.Gamma ?? dbPosition.Gamma,
            Theta = cached.Theta ?? dbPosition.Theta,
            Vega = cached.Vega ?? dbPosition.Vega,
            ImpliedVolatility = cached.Iv ?? dbPosition.ImpliedVolatility,
            UnderlyingPrice = cached.UnderlyingPrice ?? dbPosition.UnderlyingPrice,
            GreeksUpdatedAt = cached.TimestampUtc.ToString("O")
        };
    }

    /// <summary>
    /// Reconciles our tick subscription set with the current open-positions set.
    /// </summary>
    private void SyncPositionSubscriptions(IReadOnlyList<ActivePositionRecord> positions)
    {
        HashSet<string> currentIds = new(positions.Select(p => p.PositionId), StringComparer.Ordinal);

        // (1) Subscribe new positions
        foreach (ActivePositionRecord pos in positions)
        {
            if (_positionToReqId.ContainsKey(pos.PositionId))
            {
                continue;
            }

            if (_ibkrClient is null || !_ibkrClient.IsConnected)
            {
                _logger.LogDebug(
                    "{Worker} skipping greeks subscribe for {PositionId}: IBKR not connected",
                    nameof(GreeksMonitorWorker), pos.PositionId);
                continue;
            }

            int reqId;
            lock (_reqIdLock)
            {
                reqId = _nextPositionReqId++;
            }

            try
            {
                // genericTickList "106,100": 106=option implied vol, 100=option greeks
                _ibkrClient.RequestMarketData(
                    requestId: reqId,
                    symbol: pos.Symbol,
                    secType: "OPT",
                    exchange: "SMART",
                    currency: "USD",
                    genericTickList: "106,100",
                    snapshot: false);

                _positionToReqId[pos.PositionId] = reqId;
                _reqIdToPosition[reqId] = pos.PositionId;
                _positionSymbols[pos.PositionId] = pos.ContractSymbol;

                _logger.LogInformation(
                    "{Worker} subscribed greeks for position {PositionId} ({Contract}) reqId={ReqId}",
                    nameof(GreeksMonitorWorker), pos.PositionId, pos.ContractSymbol, reqId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "{Worker} failed to subscribe greeks for position {PositionId} ({Contract})",
                    nameof(GreeksMonitorWorker), pos.PositionId, pos.ContractSymbol);
            }
        }

        // (2) Unsubscribe closed positions
        List<string> closed = _positionToReqId.Keys.Where(id => !currentIds.Contains(id)).ToList();
        foreach (string posId in closed)
        {
            if (!_positionToReqId.TryRemove(posId, out int reqId))
            {
                continue;
            }
            _reqIdToPosition.TryRemove(reqId, out _);
            _liveGreeks.TryRemove(posId, out _);
            _positionSymbols.TryRemove(posId, out _);

            try
            {
                _ibkrClient?.CancelMarketData(reqId);
                _logger.LogInformation(
                    "{Worker} cancelled greeks subscription for closed position {PositionId} reqId={ReqId}",
                    nameof(GreeksMonitorWorker), posId, reqId);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error cancelling greeks reqId={ReqId} (non-fatal)", reqId);
            }
        }
    }

    /// <summary>
    /// IBKR tickOptionComputation callback handler.
    /// Runs on the IBKR reader thread — MUST be fast and swallow its own exceptions
    /// so a buggy downstream does not crash the connection thread.
    /// </summary>
    private void OnTickOptionComputation(
        object? sender,
        (int ReqId, int Field, double Iv, double Delta, double Gamma, double Vega, double Theta, double UndPrice) e)
    {
        try
        {
            if (!_reqIdToPosition.TryGetValue(e.ReqId, out string? positionId))
            {
                return;
            }

            // IBKR uses sentinel values for unavailable data: e.g. delta == -2, iv <= 0.
            // Apply the same filters MarketDataService uses (see ERR-015 area).
            double? iv = e.Iv > 0 ? e.Iv : (double?)null;
            double? delta = e.Delta > -2 ? e.Delta : (double?)null;
            double? gamma = e.Gamma >= 0 ? e.Gamma : (double?)null;
            double? vega = e.Vega >= 0 ? e.Vega : (double?)null;
            double? theta = e.Theta > -999 ? e.Theta : (double?)null;
            double? undPrice = e.UndPrice > 0 ? e.UndPrice : (double?)null;

            DateTime tsUtc = DateTime.UtcNow;

            _liveGreeks[positionId] = new CachedGreeks
            {
                Iv = iv,
                Delta = delta,
                Gamma = gamma,
                Vega = vega,
                Theta = theta,
                UnderlyingPrice = undPrice,
                TimestampUtc = tsUtc
            };

            // Fire-and-forget persistence + Outbox queuing. Failures in the task are
            // logged inside PersistGreeksAsync; we do NOT want to block the reader thread.
            _ = PersistAndQueueGreeksAsync(positionId, iv, delta, gamma, vega, theta, undPrice, tsUtc);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} OnTickOptionComputation failed (reqId={ReqId})",
                nameof(GreeksMonitorWorker), e.ReqId);
        }
    }

    private async Task PersistAndQueueGreeksAsync(
        string positionId,
        double? iv, double? delta, double? gamma, double? vega, double? theta, double? underlyingPrice,
        DateTime tsUtc)
    {
        try
        {
            // 1) Insert into position_greeks_cache
            if (_dbFactory is not null)
            {
                const string sql = """
                    INSERT OR IGNORE INTO position_greeks_cache
                        (position_id, snapshot_ts, delta, gamma, theta, vega, iv, underlying_price)
                    VALUES
                        (@PositionId, @SnapshotTs, @Delta, @Gamma, @Theta, @Vega, @Iv, @Underlying)
                    """;

                await using SqliteConnection conn = await _dbFactory.OpenAsync(CancellationToken.None);
                CommandDefinition cmd = new(sql, new
                {
                    PositionId = positionId,
                    SnapshotTs = tsUtc.ToString("O"),
                    Delta = delta,
                    Gamma = gamma,
                    Theta = theta,
                    Vega = vega,
                    Iv = iv,
                    Underlying = underlyingPrice
                });
                await conn.ExecuteAsync(cmd);
            }

            // 2) Queue Outbox event for Worker/D1 ingestion
            object payload = new
            {
                position_id = positionId,
                snapshot_ts = tsUtc.ToString("O"),
                delta,
                gamma,
                theta,
                vega,
                iv,
                underlying_price = underlyingPrice
            };

            string payloadJson = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            OutboxEntry entry = new()
            {
                EventId = Guid.NewGuid().ToString(),
                EventType = OutboxEventTypes.PositionGreeks,
                PayloadJson = payloadJson,
                // Dedupe at the second granularity — one row per (position, second)
                DedupeKey = string.Format(CultureInfo.InvariantCulture,
                    "position_greeks:{0}:{1}",
                    positionId,
                    tsUtc.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture)),
                Status = "pending",
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow.ToString("O")
            };

            await _outboxRepo.InsertAsync(entry, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "{Worker} failed to persist/queue live greeks for position {PositionId}",
                nameof(GreeksMonitorWorker), positionId);
        }
    }

    /// <summary>Cached live-Greeks snapshot per position.</summary>
    private sealed record CachedGreeks
    {
        public double? Iv { get; init; }
        public double? Delta { get; init; }
        public double? Gamma { get; init; }
        public double? Vega { get; init; }
        public double? Theta { get; init; }
        public double? UnderlyingPrice { get; init; }
        public DateTime TimestampUtc { get; init; }
    }
}
