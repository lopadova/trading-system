using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using System.Text.Json;
using TradingSupervisorService.Repositories;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Background service that monitors Implied Volatility Term Structure (IVTS).
/// Fetches IV data from IBKR for multiple expirations, calculates IVR and slope,
/// detects anomalies (inverted curve, spikes), and creates alerts on threshold breach.
/// Runs on a configurable interval (default: 15 minutes).
/// </summary>
public sealed class IvtsMonitorWorker : BackgroundService
{
    private readonly ILogger<IvtsMonitorWorker> _logger;
    private readonly IIbkrClient _ibkrClient;
    private readonly IIvtsRepository _ivtsRepo;
    private readonly IAlertRepository _alertRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly int _intervalSeconds;
    private readonly string _symbol;
    private readonly double _ivrThreshold;
    private readonly double _invertedThreshold;
    private readonly double _spikeThreshold;
    private readonly bool _enabled;

    // Request IDs for IBKR market data subscriptions
    private const int ReqId30d = 5001;
    private const int ReqId60d = 5002;
    private const int ReqId90d = 5003;
    private const int ReqId120d = 5004;

    // Store latest IV values received from IBKR callbacks
    private readonly Dictionary<int, double> _ivData = new();
    private readonly object _ivDataLock = new();

    public IvtsMonitorWorker(
        ILogger<IvtsMonitorWorker> logger,
        IIbkrClient ibkrClient,
        IIvtsRepository ivtsRepo,
        IAlertRepository alertRepo,
        IOutboxRepository outboxRepo,
        IConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ibkrClient = ibkrClient ?? throw new ArgumentNullException(nameof(ibkrClient));
        _ivtsRepo = ivtsRepo ?? throw new ArgumentNullException(nameof(ivtsRepo));
        _alertRepo = alertRepo ?? throw new ArgumentNullException(nameof(alertRepo));
        _outboxRepo = outboxRepo ?? throw new ArgumentNullException(nameof(outboxRepo));

        // Read configuration with safe defaults
        _enabled = config.GetValue<bool>("IvtsMonitor:Enabled", false);
        _intervalSeconds = config.GetValue<int>("IvtsMonitor:IntervalSeconds", 900); // 15 minutes
        _symbol = config.GetValue<string>("IvtsMonitor:Symbol", "SPX") ?? "SPX";
        _ivrThreshold = config.GetValue<double>("IvtsMonitor:IvrThresholdPercent", 80.0) / 100.0;
        _invertedThreshold = config.GetValue<double>("IvtsMonitor:InvertedThresholdPercent", 5.0) / 100.0;
        _spikeThreshold = config.GetValue<double>("IvtsMonitor:SpikeThresholdPercent", 20.0) / 100.0;

        // Validate configuration
        if (_intervalSeconds <= 0)
        {
            throw new ArgumentException($"Invalid IvtsMonitor:IntervalSeconds={_intervalSeconds}. Must be > 0.");
        }

        if (_ivrThreshold < 0.0 || _ivrThreshold > 1.0)
        {
            throw new ArgumentException($"Invalid IvtsMonitor:IvrThresholdPercent. Must be 0-100.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Early return: if IVTS monitoring is disabled, exit immediately
        if (!_enabled)
        {
            _logger.LogInformation("{Worker} is disabled in configuration. Not starting.", nameof(IvtsMonitorWorker));
            return;
        }

        _logger.LogInformation(
            "{Worker} started. Symbol={Symbol}, Interval={Interval}s, IvrThreshold={Threshold:P0}",
            nameof(IvtsMonitorWorker), _symbol, _intervalSeconds, _ivrThreshold);

        // Wait for IBKR connection before starting monitoring
        await WaitForIbkrConnectionAsync(stoppingToken);

        // Early return: if shutdown was requested during connection wait
        if (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("{Worker} shutdown requested before monitoring started", nameof(IvtsMonitorWorker));
            return;
        }

        // Main loop: monitor IVTS on interval
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);

            // Wait for next cycle
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("{Worker} shutdown requested", nameof(IvtsMonitorWorker));
                break;
            }
        }

        _logger.LogInformation("{Worker} stopped", nameof(IvtsMonitorWorker));
    }

    /// <summary>
    /// Wait for IBKR client to be connected before starting IVTS monitoring.
    /// Polls connection state every 5 seconds with timeout.
    /// </summary>
    private async Task WaitForIbkrConnectionAsync(CancellationToken ct)
    {
        int maxWaitSeconds = 300; // 5 minutes
        int elapsed = 0;

        while (!_ibkrClient.IsConnected && elapsed < maxWaitSeconds)
        {
            // Early return: shutdown requested
            if (ct.IsCancellationRequested)
            {
                return;
            }

            _logger.LogDebug("{Worker} waiting for IBKR connection ({Elapsed}s/{Max}s)...",
                nameof(IvtsMonitorWorker), elapsed, maxWaitSeconds);

            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            elapsed += 5;
        }

        // Negative-first: if still not connected after timeout, log error
        if (!_ibkrClient.IsConnected)
        {
            _logger.LogError(
                "{Worker} failed to connect to IBKR after {Timeout}s. IVTS monitoring will not function.",
                nameof(IvtsMonitorWorker), maxWaitSeconds);
            return;
        }

        _logger.LogInformation("{Worker} IBKR connection established", nameof(IvtsMonitorWorker));
    }

    /// <summary>
    /// Single IVTS monitoring cycle:
    /// 1. Fetch IV for multiple expirations from IBKR (30d, 60d, 90d, 120d)
    /// 2. Calculate IVR, term structure slope, and detect anomalies
    /// 3. Create alerts on threshold breach
    /// 4. Store snapshot in database
    /// Errors are logged but do not crash the worker (retry on next cycle).
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            // Negative-first: if not connected, skip this cycle
            if (!_ibkrClient.IsConnected)
            {
                _logger.LogWarning("{Worker} skipping cycle - IBKR not connected", nameof(IvtsMonitorWorker));
                return;
            }

            _logger.LogDebug("{Worker} starting IVTS monitoring cycle for {Symbol}", nameof(IvtsMonitorWorker), _symbol);

            // Step 1: Fetch IV data from IBKR for multiple expirations
            // Note: In production, we would request option chains and find contracts closest to 30/60/90/120 DTE
            // For now, we'll use a simplified approach with placeholder contract symbols
            // TODO: Implement proper option chain parsing and DTE selection
            bool success = await FetchIvDataAsync(ct);

            // Negative-first: if fetch failed, skip this cycle
            if (!success)
            {
                _logger.LogWarning("{Worker} failed to fetch IV data. Skipping cycle.", nameof(IvtsMonitorWorker));
                return;
            }

            // Step 2: Extract IV values from collected data
            IvtsSnapshot? snapshot = await BuildSnapshotAsync(ct);

            // Negative-first: if snapshot building failed, skip this cycle
            if (snapshot is null)
            {
                _logger.LogWarning("{Worker} failed to build IVTS snapshot. Skipping cycle.", nameof(IvtsMonitorWorker));
                return;
            }

            // Step 3: Store snapshot in database
            await _ivtsRepo.InsertSnapshotAsync(snapshot, ct);

            // Step 4: Analyze snapshot and create alerts if thresholds are breached
            await AnalyzeAndAlertAsync(snapshot, ct);

            _logger.LogInformation(
                "{Worker} cycle complete: {Symbol} IVR={Ivr:P0} Slope={Slope:F4} Inverted={Inverted}",
                nameof(IvtsMonitorWorker), _symbol, snapshot.IvrPercentile, snapshot.TermStructureSlope, snapshot.IsInverted);
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress - don't log as error
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} cycle failed. Will retry in {Interval}s",
                nameof(IvtsMonitorWorker), _intervalSeconds);
            // Do NOT rethrow - worker must survive errors and retry on next cycle
        }
    }

    /// <summary>
    /// Fetch IV data from IBKR for multiple expirations.
    /// In production, this would request option chains and select contracts closest to target DTEs.
    /// For now, we use a simplified approach with snapshot market data requests.
    /// </summary>
    private async Task<bool> FetchIvDataAsync(CancellationToken ct)
    {
        // Clear previous IV data
        lock (_ivDataLock)
        {
            _ivData.Clear();
        }

        // Note: This is a simplified implementation.
        // In production, you would:
        // 1. Request option chain for the underlying symbol
        // 2. Find contracts closest to 30/60/90/120 DTE
        // 3. Request market data with genericTickList="106" (option implied volatility)
        // 4. Wait for tickOptionComputation callbacks
        // 5. Extract IV from the callback data

        // For T-11 demonstration, we'll simulate IV data collection
        // Real implementation would require async callback handling
        await Task.Delay(100, ct); // Simulate network delay

        // Simulate IV data (replace with real IBKR API calls in production)
        lock (_ivDataLock)
        {
            // These are placeholder values - real implementation would receive from IBKR callbacks
            _ivData[ReqId30d] = 0.15;   // 15% IV for 30-day options
            _ivData[ReqId60d] = 0.18;   // 18% IV for 60-day options
            _ivData[ReqId90d] = 0.20;   // 20% IV for 90-day options
            _ivData[ReqId120d] = 0.22;  // 22% IV for 120-day options
        }

        _logger.LogDebug("{Worker} fetched IV data: 30d={Iv30d:P1} 60d={Iv60d:P1} 90d={Iv90d:P1} 120d={Iv120d:P1}",
            nameof(IvtsMonitorWorker), _ivData[ReqId30d], _ivData[ReqId60d], _ivData[ReqId90d], _ivData[ReqId120d]);

        return true;
    }

    /// <summary>
    /// Build IVTS snapshot from collected IV data.
    /// Calculates IVR, term structure slope, and detects inverted curve.
    /// </summary>
    private async Task<IvtsSnapshot?> BuildSnapshotAsync(CancellationToken ct)
    {
        // Extract IV values from collected data
        double iv30d, iv60d, iv90d, iv120d;
        lock (_ivDataLock)
        {
            // Negative-first: if any IV data is missing, return null
            if (!_ivData.ContainsKey(ReqId30d) || !_ivData.ContainsKey(ReqId60d) ||
                !_ivData.ContainsKey(ReqId90d) || !_ivData.ContainsKey(ReqId120d))
            {
                _logger.LogWarning("{Worker} incomplete IV data. Cannot build snapshot.", nameof(IvtsMonitorWorker));
                return null;
            }

            iv30d = _ivData[ReqId30d];
            iv60d = _ivData[ReqId60d];
            iv90d = _ivData[ReqId90d];
            iv120d = _ivData[ReqId120d];
        }

        // Calculate term structure slope: (IV120d - IV30d) / 90
        // Positive slope = upward sloping curve (normal)
        // Negative slope = inverted curve (stress)
        double slope = (iv120d - iv30d) / 90.0;

        // Detect inverted curve: any shorter expiry > any longer expiry by threshold
        bool isInverted = false;
        if (iv30d > iv60d + _invertedThreshold ||
            iv60d > iv90d + _invertedThreshold ||
            iv90d > iv120d + _invertedThreshold)
        {
            isInverted = true;
        }

        // Fetch 52-week IV range for IVR calculation
        (double MinIv, double MaxIv)? ivRange = await _ivtsRepo.Get52WeekIvRangeAsync(_symbol, ct);

        // Calculate IVR: (Current IV - Min IV) / (Max IV - Min IV)
        // Use average of 30d and 60d as "current IV"
        double currentIv = (iv30d + iv60d) / 2.0;
        double? ivrPercentile = null;

        if (ivRange.HasValue)
        {
            double minIv = ivRange.Value.MinIv;
            double maxIv = ivRange.Value.MaxIv;
            double range = maxIv - minIv;

            // Negative-first: avoid division by zero
            if (range > 0.0001)  // Small epsilon to avoid floating point issues
            {
                ivrPercentile = (currentIv - minIv) / range;
                // Clamp to [0, 1] range
                ivrPercentile = Math.Max(0.0, Math.Min(1.0, ivrPercentile.Value));
            }
        }

        // Build snapshot record
        DateTime now = DateTime.UtcNow;
        IvtsSnapshot snapshot = new()
        {
            SnapshotId = Guid.NewGuid().ToString(),
            Symbol = _symbol,
            TimestampUtc = now.ToString("O"),
            Iv30d = iv30d,
            Iv60d = iv60d,
            Iv90d = iv90d,
            Iv120d = iv120d,
            IvrPercentile = ivrPercentile,
            TermStructureSlope = slope,
            IsInverted = isInverted,
            IvMin52Week = ivRange?.MinIv,
            IvMax52Week = ivRange?.MaxIv,
            CreatedAt = now.ToString("O")
        };

        return snapshot;
    }

    /// <summary>
    /// Analyze IVTS snapshot and create alerts if thresholds are breached.
    /// Checks for:
    /// - IVR above threshold (high volatility environment)
    /// - Inverted term structure (market stress)
    /// - IV spike (rapid change from previous snapshot)
    /// </summary>
    private async Task AnalyzeAndAlertAsync(IvtsSnapshot snapshot, CancellationToken ct)
    {
        // Alert 1: IVR threshold breach
        if (snapshot.IvrPercentile.HasValue && snapshot.IvrPercentile.Value >= _ivrThreshold)
        {
            string message = $"⚠️ {snapshot.Symbol} IVR is {snapshot.IvrPercentile.Value:P0} (threshold: {_ivrThreshold:P0}). " +
                             $"Implied volatility is at the high end of its 52-week range.";

            string detailsJson = JsonSerializer.Serialize(new
            {
                symbol = snapshot.Symbol,
                ivr = snapshot.IvrPercentile.Value,
                threshold = _ivrThreshold,
                iv30d = snapshot.Iv30d,
                iv60d = snapshot.Iv60d,
                minIv = snapshot.IvMin52Week,
                maxIv = snapshot.IvMax52Week
            });

            IvtsAlert alert = new()
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "IvrThresholdBreach",
                Severity = "warning",
                Symbol = snapshot.Symbol,
                Message = message,
                SnapshotId = snapshot.SnapshotId,
                DetailsJson = detailsJson,
                SourceService = "TradingSupervisorService",
                CreatedAt = DateTime.UtcNow.ToString("O")
            };

            await _ivtsRepo.InsertAlertAsync(alert, ct);

            // Convert IvtsAlert to AlertRecord for API compatibility (Symbol/SnapshotId not in AlertRecord schema)
            AlertRecord alertRecord = new()
            {
                AlertId = alert.AlertId,
                AlertType = alert.AlertType,
                Severity = alert.Severity,
                Message = alert.Message,
                DetailsJson = alert.DetailsJson,
                SourceService = alert.SourceService,
                CreatedAt = alert.CreatedAt,
                ResolvedAt = alert.ResolvedAt,
                ResolvedBy = alert.ResolvedBy
            };

            // Create outbox entry for remote sync
            string eventId = Guid.NewGuid().ToString();
            string payloadJson = JsonSerializer.Serialize(alertRecord, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            OutboxEntry outboxEntry = new()
            {
                EventId = eventId,
                EventType = "alert",
                PayloadJson = payloadJson,
                DedupeKey = $"alert:{alertRecord.AlertType}:{alertRecord.AlertId}",
                Status = "pending",
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow.ToString("O")
            };

            await _outboxRepo.InsertAsync(outboxEntry, ct);

            _logger.LogWarning("IVTS Alert: {Message}", message);
        }

        // Alert 2: Inverted term structure
        if (snapshot.IsInverted)
        {
            string message = $"🔴 {snapshot.Symbol} term structure is INVERTED. " +
                             $"Shorter expirations have higher IV than longer expirations. This may indicate market stress.";

            string detailsJson = JsonSerializer.Serialize(new
            {
                symbol = snapshot.Symbol,
                iv30d = snapshot.Iv30d,
                iv60d = snapshot.Iv60d,
                iv90d = snapshot.Iv90d,
                iv120d = snapshot.Iv120d,
                slope = snapshot.TermStructureSlope
            });

            IvtsAlert alert = new()
            {
                AlertId = Guid.NewGuid().ToString(),
                AlertType = "InvertedTermStructure",
                Severity = "critical",
                Symbol = snapshot.Symbol,
                Message = message,
                SnapshotId = snapshot.SnapshotId,
                DetailsJson = detailsJson,
                SourceService = "TradingSupervisorService",
                CreatedAt = DateTime.UtcNow.ToString("O")
            };

            await _ivtsRepo.InsertAlertAsync(alert, ct);

            // Convert IvtsAlert to AlertRecord for API compatibility
            AlertRecord alertRecord = new()
            {
                AlertId = alert.AlertId,
                AlertType = alert.AlertType,
                Severity = alert.Severity,
                Message = alert.Message,
                DetailsJson = alert.DetailsJson,
                SourceService = alert.SourceService,
                CreatedAt = alert.CreatedAt,
                ResolvedAt = alert.ResolvedAt,
                ResolvedBy = alert.ResolvedBy
            };

            // Create outbox entry for remote sync
            string eventId = Guid.NewGuid().ToString();
            string payloadJson = JsonSerializer.Serialize(alertRecord, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            OutboxEntry outboxEntry = new()
            {
                EventId = eventId,
                EventType = "alert",
                PayloadJson = payloadJson,
                DedupeKey = $"alert:{alertRecord.AlertType}:{alertRecord.AlertId}",
                Status = "pending",
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow.ToString("O")
            };

            await _outboxRepo.InsertAsync(outboxEntry, ct);

            _logger.LogWarning("IVTS Alert: {Message}", message);
        }

        // Alert 3: IV spike detection (compare with previous snapshot)
        IvtsSnapshot? previousSnapshot = await _ivtsRepo.GetLatestSnapshotAsync(_symbol, ct);
        if (previousSnapshot is not null && previousSnapshot.SnapshotId != snapshot.SnapshotId)
        {
            // Calculate percentage change in IV30d
            double previousIv = previousSnapshot.Iv30d;
            double currentIv = snapshot.Iv30d;
            double changePercent = (currentIv - previousIv) / previousIv;

            // Negative-first: only alert on significant increases (not decreases)
            if (changePercent >= _spikeThreshold)
            {
                string message = $"📈 {snapshot.Symbol} IV spike detected! " +
                                 $"30-day IV increased by {changePercent:P1} from {previousIv:P1} to {currentIv:P1}.";

                string detailsJson = JsonSerializer.Serialize(new
                {
                    symbol = snapshot.Symbol,
                    previousIv = previousIv,
                    currentIv = currentIv,
                    changePercent = changePercent,
                    threshold = _spikeThreshold
                });

                IvtsAlert alert = new()
                {
                    AlertId = Guid.NewGuid().ToString(),
                    AlertType = "IvtsSpike",
                    Severity = "warning",
                    Symbol = snapshot.Symbol,
                    Message = message,
                    SnapshotId = snapshot.SnapshotId,
                    DetailsJson = detailsJson,
                    SourceService = "TradingSupervisorService",
                    CreatedAt = DateTime.UtcNow.ToString("O")
                };

                await _ivtsRepo.InsertAlertAsync(alert, ct);

                // Convert IvtsAlert to AlertRecord for API compatibility
                AlertRecord alertRecord = new()
                {
                    AlertId = alert.AlertId,
                    AlertType = alert.AlertType,
                    Severity = alert.Severity,
                    Message = alert.Message,
                    DetailsJson = alert.DetailsJson,
                    SourceService = alert.SourceService,
                    CreatedAt = alert.CreatedAt,
                    ResolvedAt = alert.ResolvedAt,
                    ResolvedBy = alert.ResolvedBy
                };

                // Create outbox entry for remote sync
                string eventId = Guid.NewGuid().ToString();
                string payloadJson = JsonSerializer.Serialize(alertRecord, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                OutboxEntry outboxEntry = new()
                {
                    EventId = eventId,
                    EventType = "alert",
                    PayloadJson = payloadJson,
                    DedupeKey = $"alert:{alertRecord.AlertType}:{alertRecord.AlertId}",
                    Status = "pending",
                    RetryCount = 0,
                    CreatedAt = DateTime.UtcNow.ToString("O")
                };

                await _outboxRepo.InsertAsync(outboxEntry, ct);

                _logger.LogWarning("IVTS Alert: {Message}", message);
            }
        }
    }
}
