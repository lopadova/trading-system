using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel.Configuration;
using SharedKernel.Domain;
using SharedKernel.Observability;
using SharedKernel.Safety;

namespace OptionsExecutionService.Services;

/// <summary>
/// Reads the composite operating-risk semaphore from the Cloudflare Worker's
/// <c>GET /api/risk/semaphore</c> endpoint and caches the result in-memory for
/// 60 seconds. This is the FIRST gate consulted by <c>OrderPlacer</c> and —
/// combined with the daily-PnL flag — is the last line of defence against a
/// runaway strategy placing orders during a red-regime market. All failure
/// modes fall asymmetrically:
/// <list type="bullet">
///   <item><description>Network error (DNS, socket timeout, 5xx) ⇒ default to <see cref="SemaphoreStatus.Orange"/>
///   (fail-cautious — block NEW entries, allow exits).</description></item>
///   <item><description>Auth failure (401/403) ⇒ default to <see cref="SemaphoreStatus.Red"/>
///   + Critical alert (fail-closed — a broken security posture is not a safe
///   state to place trades from).</description></item>
///   <item><description>Timeout inside <see cref="IsRed"/> (>3 seconds) ⇒ return <c>true</c>
///   (fail-closed — never let a hung Worker approve a trade by default).</description></item>
/// </list>
/// The cache TTL is 60s by design: latency-sensitive enough that the operator's
/// "flip to RED" signal lands in the gate within at most one minute, but long
/// enough that a burst of orders in the same strategy pass doesn't hammer the
/// Worker with duplicate requests.
/// </summary>
public sealed class SemaphoreGate
{
    /// <summary>In-memory cache TTL. Deliberately short so operator overrides propagate fast.</summary>
    public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    /// <summary>Hard timeout for the sync <see cref="IsRed"/> wrapper. Fail-closed if exceeded.</summary>
    public static readonly TimeSpan IsRedTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Per-request HTTP timeout for the async fetch path. Short — we'd rather fall back than block.</summary>
    public static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Number of fetch attempts per <see cref="GetCurrentStatusAsync"/> call. One retry
    /// after the initial attempt (total 2) is a good trade: absorbs a single blip
    /// without delaying the hot path enough to trip <see cref="IsRedTimeout"/>.
    /// Auth errors (401/403) and explicit caller cancellations are NOT retried —
    /// retrying those would be either useless (auth doesn't self-heal) or wrong
    /// (caller already gave up).
    /// </summary>
    internal const int MaxFetchAttempts = 2;

    /// <summary>
    /// Base backoff between retry attempts. Combined with per-call jitter this
    /// stays well under <see cref="IsRedTimeout"/> (3s) even at MaxFetchAttempts−1
    /// retries — total worst case ≈ HttpTimeout + backoff + HttpTimeout ≈ 2s + 0.25s + 2s,
    /// which is why <see cref="IsRed"/> wraps the whole thing in a hard timeout.
    /// </summary>
    internal static readonly TimeSpan RetryBackoff = TimeSpan.FromMilliseconds(150);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CloudflareOptions _cloudflare;
    private readonly ILogger<SemaphoreGate> _logger;
    private readonly IAlerter _alerter;
    private readonly TimeProvider _timeProvider;

    // Cache state (read/written under _cacheLock). Using a simple lock rather
    // than ReaderWriterLockSlim — contention on a single-entry cache is negligible.
    private readonly Lock _cacheLock = new();
    private SemaphoreStatus? _cachedValue;
    private DateTimeOffset _cachedAt = DateTimeOffset.MinValue;

    public SemaphoreGate(
        IHttpClientFactory httpClientFactory,
        IOptions<CloudflareOptions> cloudflare,
        ILogger<SemaphoreGate> logger,
        IAlerter alerter,
        TimeProvider? timeProvider = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _cloudflare = cloudflare?.Value ?? throw new ArgumentNullException(nameof(cloudflare));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _alerter = alerter ?? throw new ArgumentNullException(nameof(alerter));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Fetches the current semaphore status, using the cache when fresh.
    /// Never throws — classifies unexpected errors to a defensive default
    /// (see class summary for the fail-cautious / fail-closed matrix).
    /// </summary>
    public async Task<SemaphoreStatus> GetCurrentStatusAsync(CancellationToken ct)
    {
        // Fast path: cache hit under TTL.
        DateTimeOffset now = _timeProvider.GetUtcNow();
        lock (_cacheLock)
        {
            if (_cachedValue is not null && now - _cachedAt < CacheTtl)
            {
                return _cachedValue.Value;
            }
        }

        // Slow path: refresh from the Worker. Even if this fails, we cache the
        // defensive default so we don't repeatedly hammer a dead Worker.
        SemaphoreStatus fresh = await FetchFromWorkerAsync(ct).ConfigureAwait(false);

        lock (_cacheLock)
        {
            _cachedValue = fresh;
            _cachedAt = now;
        }

        return fresh;
    }

    /// <summary>
    /// Synchronous convenience for the order-placement hot path. Bounded by
    /// <see cref="IsRedTimeout"/>; on timeout returns <c>true</c> (fail-closed)
    /// so a hung Worker never accidentally approves a trade.
    /// </summary>
    public bool IsRed()
    {
        using CancellationTokenSource cts = new(IsRedTimeout);
        try
        {
            // Block intentionally — caller is synchronous PlaceOrderAsync (even though
            // that method is async, we want a clean bool accessor without plumbing
            // CancellationToken through the gate #1 check). Task.Run isolates the
            // sync-over-async hop from any captured SynchronizationContext.
            SemaphoreStatus status = Task.Run(() => GetCurrentStatusAsync(cts.Token), cts.Token)
                .GetAwaiter().GetResult();
            return status == SemaphoreStatus.Red;
        }
        catch (OperationCanceledException)
        {
            // Timeout → fail-closed. This is the critical "hung Worker" guard.
            _logger.LogWarning("SemaphoreGate.IsRed timed out after {Timeout}s — fail-closing (treating as RED)",
                IsRedTimeout.TotalSeconds);
            return true;
        }
        catch (Exception ex)
        {
            // Any unexpected exception: fail-closed, log loudly.
            _logger.LogError(ex, "SemaphoreGate.IsRed threw unexpectedly — fail-closing (treating as RED)");
            return true;
        }
    }

    /// <summary>
    /// Outer fetch driver. Classifies errors and, for transient ones only
    /// (network failure / HTTP timeout / 5xx), retries up to
    /// <see cref="MaxFetchAttempts"/> with a small jittered backoff before
    /// giving up to the defensive fallback. Terminal outcomes (URL blank,
    /// auth failure, parseable payload, unparseable payload, caller-cancelled)
    /// short-circuit the loop — retrying those is either useless or unsafe.
    /// <para>
    /// Error classification:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>401/403 → <see cref="SemaphoreStatus.Red"/> + Critical alert (auth broken; NO retry).</description></item>
    ///   <item><description>Timeout / network / 5xx → retry up to <see cref="MaxFetchAttempts"/>, then <see cref="SemaphoreStatus.Orange"/>.</description></item>
    ///   <item><description>200 with known payload → parsed status (NO retry — deterministic).</description></item>
    ///   <item><description>200 with unexpected payload → <see cref="SemaphoreStatus.Orange"/> (NO retry — bad payload won't change on retry).</description></item>
    /// </list>
    /// </summary>
    private async Task<SemaphoreStatus> FetchFromWorkerAsync(CancellationToken ct)
    {
        // Worker URL not configured — treat as Orange (dev mode; operator is expected
        // to have either set the URL or accepted the cautious default).
        if (string.IsNullOrWhiteSpace(_cloudflare.WorkerUrl))
        {
            _logger.LogWarning("SemaphoreGate: Cloudflare:WorkerUrl is blank — defaulting to ORANGE");
            return SemaphoreStatus.Orange;
        }

        string endpoint = $"{_cloudflare.WorkerUrl.TrimEnd('/')}/api/risk/semaphore";

        FetchAttemptResult last = default;
        for (int attempt = 1; attempt <= MaxFetchAttempts; attempt++)
        {
            last = await TryFetchOnceAsync(endpoint, attempt, ct).ConfigureAwait(false);
            if (!last.IsRetryable)
            {
                return last.Status;
            }

            if (attempt < MaxFetchAttempts && !ct.IsCancellationRequested)
            {
                // Full-jitter backoff (AWS recipe): random in [0, base] to avoid
                // retry storms when multiple gate checks converge after the same
                // outage. Short enough that 2 attempts stay under IsRedTimeout.
                TimeSpan jitter = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * RetryBackoff.TotalMilliseconds);
                try
                {
                    await Task.Delay(jitter, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Caller cancelled during backoff — return the fallback we already have.
                    return last.Status;
                }
            }
        }

        _logger.LogWarning(
            "SemaphoreGate: all {Attempts} fetch attempt(s) to {Endpoint} failed transiently — defaulting to {Status}",
            MaxFetchAttempts, endpoint, last.Status);
        return last.Status;
    }

    /// <summary>
    /// Single fetch attempt. Returns a struct carrying the fallback status and
    /// a retryable flag so the caller (and tests) can reason about loop behaviour.
    /// Never throws — all exception paths are translated into a fallback status.
    /// </summary>
    private async Task<FetchAttemptResult> TryFetchOnceAsync(string endpoint, int attempt, CancellationToken ct)
    {
        try
        {
            HttpClient http = _httpClientFactory.CreateClient(nameof(SemaphoreGate));
            http.Timeout = HttpTimeout;

            using HttpRequestMessage request = new(HttpMethod.Get, endpoint);
            if (!string.IsNullOrEmpty(_cloudflare.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _cloudflare.ApiKey);
            }

            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct)
                .ConfigureAwait(false);

            // Auth failures ⇒ fail-CLOSED. Security-critical; no retry (a 403 doesn't self-heal).
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogCritical(
                    "SemaphoreGate: Worker rejected X-Api-Key ({StatusCode}) — fail-closing to RED (attempt {Attempt})",
                    (int)response.StatusCode, attempt);
                await _alerter.SendImmediateAsync(
                    AlertSeverity.Critical,
                    "SemaphoreGate auth failure",
                    $"Worker returned {(int)response.StatusCode} for {endpoint}. All new orders will be blocked until resolved.",
                    ct).ConfigureAwait(false);
                return FetchAttemptResult.Terminal(SemaphoreStatus.Red);
            }

            // 5xx ⇒ transient, RETRY. Reachable but unhappy; a second try often clears it.
            if ((int)response.StatusCode >= 500)
            {
                _logger.LogWarning(
                    "SemaphoreGate: Worker returned {StatusCode} on attempt {Attempt}/{Max} — will retry",
                    (int)response.StatusCode, attempt, MaxFetchAttempts);
                return FetchAttemptResult.Retryable(SemaphoreStatus.Orange);
            }

            // Other non-2xx (4xx except auth) ⇒ terminal; retrying a 404/400 is a waste.
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "SemaphoreGate: Worker returned {StatusCode} — defaulting to ORANGE (attempt {Attempt}, no retry)",
                    (int)response.StatusCode, attempt);
                return FetchAttemptResult.Terminal(SemaphoreStatus.Orange);
            }

            // Parse the JSON payload. Payload shape: {"status": "green"|"orange"|"red", ...}.
            string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            SemaphoreStatus parsed = ParseStatusFromPayload(body);
            return FetchAttemptResult.Terminal(parsed);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Caller cancelled — NOT retryable (caller gave up).
            _logger.LogDebug("SemaphoreGate: caller cancelled on attempt {Attempt}; returning ORANGE", attempt);
            return FetchAttemptResult.Terminal(SemaphoreStatus.Orange);
        }
        catch (TaskCanceledException ex)
        {
            // HttpClient timeout surfaces as TaskCanceledException without ct cancellation — RETRY.
            _logger.LogWarning(ex,
                "SemaphoreGate: HTTP timeout to {Endpoint} on attempt {Attempt}/{Max} — will retry",
                endpoint, attempt, MaxFetchAttempts);
            return FetchAttemptResult.Retryable(SemaphoreStatus.Orange);
        }
        catch (HttpRequestException ex)
        {
            // Transport-level error (DNS, socket, TLS) — RETRY.
            _logger.LogWarning(ex,
                "SemaphoreGate: HTTP error to {Endpoint} on attempt {Attempt}/{Max} — will retry",
                endpoint, attempt, MaxFetchAttempts);
            return FetchAttemptResult.Retryable(SemaphoreStatus.Orange);
        }
        catch (Exception ex)
        {
            // Truly unexpected — log at error, default to Orange. NOT retried — we don't
            // know whether this class is safe to retry, so fail-cautious to a single fallback.
            _logger.LogError(ex, "SemaphoreGate: unexpected error on attempt {Attempt} — defaulting to ORANGE", attempt);
            return FetchAttemptResult.Terminal(SemaphoreStatus.Orange);
        }
    }

    /// <summary>
    /// Classification of a single fetch attempt — the outer loop uses
    /// <see cref="IsRetryable"/> to decide whether to spend another attempt
    /// or honour the fallback <see cref="Status"/> immediately.
    /// </summary>
    private readonly record struct FetchAttemptResult(SemaphoreStatus Status, bool IsRetryable)
    {
        public static FetchAttemptResult Terminal(SemaphoreStatus status) => new(status, false);
        public static FetchAttemptResult Retryable(SemaphoreStatus fallback) => new(fallback, true);
    }

    /// <summary>
    /// Parses the Worker's <c>/api/risk/semaphore</c> payload and extracts the
    /// top-level <c>status</c> field. Unknown / missing values ⇒ Orange.
    /// </summary>
    private SemaphoreStatus ParseStatusFromPayload(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogWarning("SemaphoreGate: empty body — defaulting to ORANGE");
            return SemaphoreStatus.Orange;
        }

        try
        {
            SemaphorePayload? payload = JsonSerializer.Deserialize<SemaphorePayload>(body, s_jsonOptions);
            if (payload is null || string.IsNullOrWhiteSpace(payload.Status))
            {
                _logger.LogWarning("SemaphoreGate: unparseable payload — defaulting to ORANGE. Body={Body}", body);
                return SemaphoreStatus.Orange;
            }

            return payload.Status.ToLowerInvariant() switch
            {
                "green" => SemaphoreStatus.Green,
                "orange" => SemaphoreStatus.Orange,
                "red" => SemaphoreStatus.Red,
                _ => LogUnknownAndFallback(payload.Status)
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "SemaphoreGate: JSON parse failure — defaulting to ORANGE. Body={Body}", body);
            return SemaphoreStatus.Orange;
        }
    }

    private SemaphoreStatus LogUnknownAndFallback(string raw)
    {
        _logger.LogWarning("SemaphoreGate: unknown status '{Status}' — defaulting to ORANGE", raw);
        return SemaphoreStatus.Orange;
    }

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Minimal shape of the Worker response — we only care about the top-level
    /// <c>status</c> field for the gate decision. Other fields (indicators,
    /// score, asOf) are ignored here but visible to the dashboard.
    /// </summary>
    private sealed class SemaphorePayload
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }
}
