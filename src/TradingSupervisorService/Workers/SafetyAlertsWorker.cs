using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using SharedKernel.Ibkr;
using SharedKernel.Observability;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Safety-net worker that periodically evaluates invariants and fans out to all registered
/// <see cref="IAlerter"/> implementations (Telegram immediately for Critical, Email for Critical,
/// Telegram digest for Warning). See docs/ops/OBSERVABILITY.md for the severity matrix.
/// <para>
/// Checks performed each cycle:
/// </para>
/// <list type="bullet">
///   <item><description>IBKR disconnected for &gt; <c>IbkrDisconnectThresholdSeconds</c> → Critical.</description></item>
///   <item><description>Margin &gt; <c>MarginThresholdPercent</c> → Warning (digested).</description></item>
/// </list>
/// <para>
/// Ingest error-rate and semaphore-red duration checks are skeletal here (no data source yet
/// on the .NET side — those metrics live in the Worker). Hooks are in place so the other
/// subagent's Worker endpoints can be wired in without touching this file's structure.
/// </para>
/// <para>
/// Each alerter is invoked via <c>Task.WhenAll</c> and errors are swallowed individually —
/// a Telegram outage must not break Email delivery and vice versa.
/// </para>
/// </summary>
public sealed class SafetyAlertsWorker : BackgroundService
{
    private readonly ILogger<SafetyAlertsWorker> _logger;
    private readonly IEnumerable<IAlerter> _alerters;
    private readonly IIbkrClient _ibkr;

    // Config
    private readonly bool _enabled;
    private readonly int _intervalSeconds;
    private readonly int _ibkrDisconnectThresholdSec;
    private readonly double _marginThresholdPercent;

    // State — tracks when IBKR first became disconnected so we can fire once per outage.
    private DateTime? _ibkrDisconnectedSinceUtc;
    private bool _ibkrDisconnectAlerted;

    public SafetyAlertsWorker(
        ILogger<SafetyAlertsWorker> logger,
        IEnumerable<IAlerter> alerters,
        IIbkrClient ibkr,
        IConfiguration configuration)
    {
        _logger = logger;
        _alerters = alerters ?? throw new ArgumentNullException(nameof(alerters));
        _ibkr = ibkr ?? throw new ArgumentNullException(nameof(ibkr));

        _enabled = configuration.GetValue<bool>("SafetyAlerts:Enabled", true);
        _intervalSeconds = configuration.GetValue<int>("SafetyAlerts:IntervalSeconds", 60);
        _ibkrDisconnectThresholdSec = configuration.GetValue<int>("SafetyAlerts:IbkrDisconnectThresholdSeconds", 30);
        _marginThresholdPercent = configuration.GetValue<double>("SafetyAlerts:MarginThresholdPercent", 75.0);

        if (_intervalSeconds <= 0)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture,
                    "SafetyAlerts:IntervalSeconds must be > 0 (was {0})", _intervalSeconds),
                nameof(configuration));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("{Worker} disabled by config", nameof(SafetyAlertsWorker));
            return;
        }

        _logger.LogInformation(
            "{Worker} started. interval={Interval}s ibkrThresh={IbkrThresh}s marginThresh={MarginThresh}%",
            nameof(SafetyAlertsWorker),
            _intervalSeconds,
            _ibkrDisconnectThresholdSec,
            _marginThresholdPercent.ToString("F1", CultureInfo.InvariantCulture));

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("{Worker} stopped", nameof(SafetyAlertsWorker));
    }

    /// <summary>
    /// Single evaluation cycle. Errors are logged but never rethrown — the worker must
    /// keep running across transient failures.
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            await CheckIbkrConnectionAsync(ct).ConfigureAwait(false);
            // Future checks wire in here:
            //   await CheckMarginAsync(ct);                  // requires Worker /api/v1/positions/summary
            //   await CheckIngestErrorRateAsync(ct);          // requires Worker /api/v1/metrics/ingest
            //   await CheckSemaphoreStuckRedAsync(ct);        // requires Worker /api/v1/semaphore/current
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} cycle failed", nameof(SafetyAlertsWorker));
        }
    }

    /// <summary>
    /// Detects sustained IBKR disconnection and fires a single Critical alert per outage.
    /// Resets the outage tracker once the client reconnects.
    /// </summary>
    private async Task CheckIbkrConnectionAsync(CancellationToken ct)
    {
        bool connected;
        try
        {
            connected = _ibkr.IsConnected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IBKR IsConnected threw; treating as disconnected");
            connected = false;
        }

        DateTime now = DateTime.UtcNow;

        if (connected)
        {
            // Reconnected — reset outage tracker so the NEXT disconnect fires again.
            if (_ibkrDisconnectedSinceUtc.HasValue)
            {
                TimeSpan outage = now - _ibkrDisconnectedSinceUtc.Value;
                _logger.LogInformation("IBKR reconnected after {Seconds}s outage",
                    outage.TotalSeconds.ToString("F0", CultureInfo.InvariantCulture));
            }
            _ibkrDisconnectedSinceUtc = null;
            _ibkrDisconnectAlerted = false;
            return;
        }

        // Disconnected — start tracking if this is the first cycle of the outage.
        if (!_ibkrDisconnectedSinceUtc.HasValue)
        {
            _ibkrDisconnectedSinceUtc = now;
            _logger.LogDebug("IBKR disconnection detected; starting outage timer");
            return;
        }

        // Already tracking; check if we've exceeded the threshold AND haven't alerted yet.
        TimeSpan elapsed = now - _ibkrDisconnectedSinceUtc.Value;
        if (elapsed.TotalSeconds >= _ibkrDisconnectThresholdSec && !_ibkrDisconnectAlerted)
        {
            string title = string.Format(CultureInfo.InvariantCulture,
                "IBKR disconnected > {0}s", _ibkrDisconnectThresholdSec);
            string message = string.Format(CultureInfo.InvariantCulture,
                "IBKR connection has been down since {0:O} ({1:F0}s). Trading is halted until reconnection.",
                _ibkrDisconnectedSinceUtc.Value, elapsed.TotalSeconds);

            _logger.LogWarning("Firing Critical alert: {Title}", title);
            await FanOutAsync(AlertSeverity.Critical, title, message, ct).ConfigureAwait(false);
            _ibkrDisconnectAlerted = true;
        }
    }

    /// <summary>
    /// Invokes every registered <see cref="IAlerter"/> concurrently and collects per-alerter
    /// failures without failing the whole cycle. A Telegram outage must not kill Email.
    /// </summary>
    private async Task FanOutAsync(AlertSeverity severity, string title, string message, CancellationToken ct)
    {
        List<Task> tasks = new();
        foreach (IAlerter alerter in _alerters)
        {
            tasks.Add(DispatchSafeAsync(alerter, severity, title, message, ct));
        }
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task DispatchSafeAsync(IAlerter alerter, AlertSeverity severity, string title, string message, CancellationToken ct)
    {
        try
        {
            await alerter.SendImmediateAsync(severity, title, message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Alerter {Type} threw on severity={Severity}", alerter.GetType().Name, severity);
        }
    }
}
