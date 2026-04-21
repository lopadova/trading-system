using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Configuration;
using SharedKernel.Domain;
using SharedKernel.Observability;
using SharedKernel.Safety;

namespace OptionsExecutionService.Services;

/// <summary>
/// Background service that polls the Worker's <c>GET /api/performance/today</c>
/// endpoint every <c>Safety:PollIntervalSeconds</c> and, when the absolute
/// daily drawdown exceeds <c>Safety:MaxDailyDrawdownPct</c>, flips the durable
/// <c>trading_paused</c> safety flag so <see cref="OptionsExecutionService.Orders.OrderPlacer"/>
/// stops accepting new orders until the operator manually unpauses.
/// <para>
/// Design choices:
/// </para>
/// <list type="bullet">
///   <item><description>No auto-unpause. A pause survives a restart AND a quiet recovery; the
///   operator must explicitly delete the flag (<c>DELETE FROM safety_flags WHERE key='trading_paused'</c>).
///   This is deliberate — a drawdown that auto-unpauses is a drawdown that happens twice.</description></item>
///   <item><description>Idempotent. Re-flipping the flag while already set is a no-op on the
///   user experience; we just skip the alert spam.</description></item>
///   <item><description>Magnitude-only compare. Uses <c>|pnlPct|</c> so the logic is the same
///   whether the Worker returns +/- drawdown semantics. If we ever want to
///   ignore intraday rallies, change this to <c>pnlPct &lt; -threshold</c>.</description></item>
/// </list>
/// </summary>
public sealed class DailyPnLWatcher : BackgroundService
{
    /// <summary>Safety-flag key monitored by <c>OrderPlacer</c>.</summary>
    public const string TradingPausedKey = "trading_paused";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CloudflareOptions _cloudflare;
    private readonly SafetyOptions _safety;
    private readonly ISafetyFlagStore _flagStore;
    private readonly IAlerter _alerter;
    private readonly ILogger<DailyPnLWatcher> _logger;

    public DailyPnLWatcher(
        IHttpClientFactory httpClientFactory,
        IOptions<CloudflareOptions> cloudflare,
        IOptions<SafetyOptions> safety,
        ISafetyFlagStore flagStore,
        IAlerter alerter,
        ILogger<DailyPnLWatcher> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _cloudflare = cloudflare?.Value ?? throw new ArgumentNullException(nameof(cloudflare));
        _safety = safety?.Value ?? throw new ArgumentNullException(nameof(safety));
        _flagStore = flagStore ?? throw new ArgumentNullException(nameof(flagStore));
        _alerter = alerter ?? throw new ArgumentNullException(nameof(alerter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(30, _safety.PollIntervalSeconds));
        _logger.LogInformation(
            "DailyPnLWatcher started. Interval={Interval}s, MaxDrawdown={Pct}%",
            interval.TotalSeconds, _safety.MaxDailyDrawdownPct);

        // First tick is a "warm" one — no initial 5-min wait.
        // We still want resilience to transient failures, so each tick wraps
        // its own try/catch to avoid killing the BackgroundService.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunTickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutdown path — exit cleanly.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DailyPnLWatcher tick failed — will retry in {Interval}s", interval.TotalSeconds);
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("DailyPnLWatcher stopped");
    }

    /// <summary>
    /// One poll cycle: fetch equity, compute drawdown pct, flip flag if over threshold.
    /// </summary>
    private async Task RunTickAsync(CancellationToken ct)
    {
        // No Worker URL → can't poll; silent no-op on dev.
        if (string.IsNullOrWhiteSpace(_cloudflare.WorkerUrl))
        {
            _logger.LogDebug("DailyPnLWatcher: Cloudflare:WorkerUrl blank — skipping tick");
            return;
        }

        PerformanceToday? today = await FetchAsync(ct).ConfigureAwait(false);
        if (today is null)
        {
            // Transport-level failure — already logged by FetchAsync.
            return;
        }

        // When yesterday's close is zero or missing, %-math is undefined; skip.
        if (today.YesterdayClose is null or 0)
        {
            _logger.LogInformation(
                "DailyPnLWatcher: yesterdayClose missing/zero — skipping threshold check (value={Value})",
                today.YesterdayClose);
            return;
        }

        decimal pnlPctMagnitude = Math.Abs(today.PnlPct);
        _logger.LogInformation(
            "DailyPnLWatcher: account={Account}, yesterday={Yesterday}, pnlPct={Pct}%",
            today.AccountValue.ToString("F2", CultureInfo.InvariantCulture),
            today.YesterdayClose?.ToString("F2", CultureInfo.InvariantCulture),
            today.PnlPct.ToString("F3", CultureInfo.InvariantCulture));

        if (pnlPctMagnitude < _safety.MaxDailyDrawdownPct)
        {
            return;
        }

        // Already paused? Avoid alert spam.
        bool alreadyPaused = await _flagStore.IsSetAsync(TradingPausedKey, ct).ConfigureAwait(false);
        if (alreadyPaused)
        {
            _logger.LogInformation(
                "DailyPnLWatcher: drawdown {Pct}% above {Threshold}% but trading_paused already set — no-op",
                pnlPctMagnitude.ToString("F3", CultureInfo.InvariantCulture),
                _safety.MaxDailyDrawdownPct.ToString("F3", CultureInfo.InvariantCulture));
            return;
        }

        // Flip the flag + alert. Set before alerting — if the alert path is slow
        // we still want the gate to be up-to-date ASAP.
        await _flagStore.SetAsync(TradingPausedKey, "1", ct).ConfigureAwait(false);

        string title = "DailyPnLWatcher: trading paused";
        string body = string.Format(
            CultureInfo.InvariantCulture,
            "Daily drawdown {0:F2}% exceeds budget {1:F2}%. Trading paused. Operator must unpause manually " +
            "(DELETE FROM safety_flags WHERE key='trading_paused'). account={2:F2} yesterdayClose={3:F2}",
            pnlPctMagnitude,
            _safety.MaxDailyDrawdownPct,
            today.AccountValue,
            today.YesterdayClose ?? 0m);

        _logger.LogCritical("{Title}: {Body}", title, body);
        await _alerter.SendImmediateAsync(AlertSeverity.Critical, title, body, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// GET <c>/api/performance/today</c>. Returns null on any non-success path so
    /// the watcher can skip the tick rather than tripping on a transient error.
    /// </summary>
    private async Task<PerformanceToday?> FetchAsync(CancellationToken ct)
    {
        string endpoint = $"{_cloudflare.WorkerUrl.TrimEnd('/')}/api/performance/today";
        try
        {
            HttpClient http = _httpClientFactory.CreateClient(nameof(DailyPnLWatcher));
            http.Timeout = TimeSpan.FromSeconds(10);

            using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
            if (!string.IsNullOrEmpty(_cloudflare.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _cloudflare.ApiKey);
            }

            using HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Endpoint not deployed yet — expected during rollout. Log once-per-tick.
                _logger.LogInformation("DailyPnLWatcher: Worker endpoint 404 — performance/today not deployed?");
                return null;
            }
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("DailyPnLWatcher: Worker returned {StatusCode}", (int)response.StatusCode);
                return null;
            }

            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            PerformanceToday? parsed = JsonSerializer.Deserialize<PerformanceToday>(body, s_jsonOptions);
            return parsed;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DailyPnLWatcher: fetch failed for {Endpoint}", endpoint);
            return null;
        }
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Shape of the Worker's <c>/api/performance/today</c> payload.
    /// Property names match the snake_case / camelCase the Worker chose; we are
    /// permissive via <c>PropertyNameCaseInsensitive</c>.
    /// </summary>
    private sealed class PerformanceToday
    {
        [JsonPropertyName("accountValue")]
        public decimal AccountValue { get; set; }

        [JsonPropertyName("cash")]
        public decimal Cash { get; set; }

        [JsonPropertyName("pnl")]
        public decimal Pnl { get; set; }

        [JsonPropertyName("pnlPct")]
        public decimal PnlPct { get; set; }

        [JsonPropertyName("yesterdayClose")]
        public decimal? YesterdayClose { get; set; }
    }
}
