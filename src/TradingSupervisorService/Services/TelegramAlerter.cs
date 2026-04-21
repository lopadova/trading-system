using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using SharedKernel.Observability;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TradingSupervisorService.Services;

/// <summary>
/// Configuration for Telegram alerting service.
/// Immutable record validated on construction.
/// </summary>
public sealed record TelegramConfig
{
    /// <summary>
    /// Telegram Bot API token (from @BotFather).
    /// </summary>
    public string BotToken { get; init; } = string.Empty;

    /// <summary>
    /// Target chat ID to send alerts to (can be user ID or group chat ID).
    /// </summary>
    public long ChatId { get; init; }

    /// <summary>
    /// Whether Telegram alerting is enabled.
    /// If false, alerts are logged but not sent.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Maximum number of retry attempts for failed sends.
    /// </summary>
    public int MaxRetryAttempts { get; init; } = 3;

    /// <summary>
    /// Initial retry delay in seconds (doubles on each retry).
    /// </summary>
    public int RetryDelaySeconds { get; init; } = 5;

    /// <summary>
    /// Maximum rate limit: messages per minute.
    /// Telegram limit is 30/second, we default to 20/minute for safety.
    /// </summary>
    public int MaxMessagesPerMinute { get; init; } = 20;

    /// <summary>
    /// Validates configuration.
    /// Returns error message if invalid, null if valid.
    /// </summary>
    public string? Validate()
    {
        if (!Enabled)
            return null;  // Skip validation if disabled

        if (string.IsNullOrWhiteSpace(BotToken))
            return "Telegram:BotToken is required when Telegram:Enabled is true";

        if (ChatId == 0)
            return "Telegram:ChatId is required when Telegram:Enabled is true";

        if (MaxRetryAttempts < 0 || MaxRetryAttempts > 10)
            return "Telegram:MaxRetryAttempts must be between 0 and 10";

        if (RetryDelaySeconds < 1 || RetryDelaySeconds > 300)
            return "Telegram:RetryDelaySeconds must be between 1 and 300";

        return null;
    }
}

/// <summary>
/// Telegram alerting service implementation. Also implements <see cref="IAlerter"/>
/// (severity-aware routing) so it can participate in the composite alerter pipeline.
/// <para>Severity routing:</para>
/// <list type="bullet">
///   <item><description><b>Critical / Error</b> — send immediately (bypasses queue, still rate-limited).</description></item>
///   <item><description><b>Warning</b> — buffered; flushed as a digest every <c>DigestFlushMinutes</c> (default 15).</description></item>
///   <item><description><b>Info</b> — logged only, never shipped to Telegram.</description></item>
/// </list>
/// <para>
/// The digest buffer is size-capped at 100 entries; when it fills we immediately flush
/// to avoid unbounded memory. The digest timer is lazy — it only arms when the first
/// Warning lands, and disarms after the flush.
/// </para>
/// Thread-safe for concurrent queueing.
/// </summary>
public sealed class TelegramAlerter : ITelegramAlerter, IAlerter, IDisposable
{
    private readonly TelegramConfig _config;
    private readonly ILogger<TelegramAlerter> _logger;
    private readonly TelegramBotClient? _botClient;
    private readonly ConcurrentQueue<TelegramAlert> _alertQueue;

    // Rate limiting: track message timestamps in last minute
    private readonly ConcurrentQueue<DateTime> _messageTimestamps;

    // Digest buffer for Warning-level alerts. Guarded by _digestLock.
    // Tuple of (title, message, addedUtc) — addedUtc used purely for eventual eviction,
    // not for timing (timing is done by _digestTimer).
    private readonly List<(string title, string message, DateTime addedUtc)> _digestBuffer = new();
    private readonly object _digestLock = new();
    private readonly Timer _digestTimer;
    private readonly TimeSpan _digestFlushInterval;

    // Max digest entries before forced flush (prevents memory blow-up on a noisy system).
    private const int DigestMaxEntries = 100;

    public TelegramAlerter(IConfiguration configuration, ILogger<TelegramAlerter> logger)
    {
        _logger = logger;
        _alertQueue = new ConcurrentQueue<TelegramAlert>();
        _messageTimestamps = new ConcurrentQueue<DateTime>();

        // Digest flush interval — default 15 min per spec.
        int flushMinutes = configuration.GetValue<int>("Telegram:DigestFlushMinutes", 15);
        _digestFlushInterval = TimeSpan.FromMinutes(flushMinutes > 0 ? flushMinutes : 15);

        // Timer starts disabled; armed lazily in BufferWarning() on first entry.
        _digestTimer = new Timer(OnDigestFlushTick, state: null, Timeout.Infinite, Timeout.Infinite);

        // Load and validate configuration
        _config = new TelegramConfig
        {
            BotToken = configuration.GetValue<string>("Telegram:BotToken") ?? string.Empty,
            ChatId = configuration.GetValue<long>("Telegram:ChatId"),
            Enabled = configuration.GetValue<bool>("Telegram:Enabled", true),
            MaxRetryAttempts = configuration.GetValue<int>("Telegram:MaxRetryAttempts", 3),
            RetryDelaySeconds = configuration.GetValue<int>("Telegram:RetryDelaySeconds", 5),
            MaxMessagesPerMinute = configuration.GetValue<int>("Telegram:MaxMessagesPerMinute", 20)
        };

        string? validationError = _config.Validate();
        if (validationError != null)
        {
            _logger.LogWarning("Telegram configuration invalid: {Error}. Alerting disabled.", validationError);
            _config = _config with { Enabled = false };
            return;
        }

        if (!_config.Enabled)
        {
            _logger.LogInformation("Telegram alerting is disabled in configuration");
            return;
        }

        // Initialize Telegram Bot client
        try
        {
            _botClient = new TelegramBotClient(_config.BotToken);
            _logger.LogInformation("TelegramAlerter initialized. ChatId={ChatId}, MaxRetries={MaxRetries}",
                _config.ChatId, _config.MaxRetryAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Telegram Bot client. Alerting disabled.");
            _config = _config with { Enabled = false };
        }
    }

    /// <summary>
    /// Queues an alert to be sent via Telegram.
    /// Thread-safe. Returns immediately.
    /// </summary>
    public Task QueueAlertAsync(TelegramAlert alert)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Telegram disabled. Alert queued to logs only: {Message}", alert.Message);
            return Task.CompletedTask;
        }

        _alertQueue.Enqueue(alert);
        _logger.LogDebug("Alert queued for Telegram. QueueSize={QueueSize}, AlertId={AlertId}",
            _alertQueue.Count, alert.AlertId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends an alert immediately, bypassing queue.
    /// Blocks until sent or fails. Use for critical alerts only.
    /// </summary>
    public async Task<bool> SendImmediateAsync(TelegramAlert alert, CancellationToken ct)
    {
        if (!_config.Enabled || _botClient == null)
        {
            _logger.LogWarning("Telegram disabled. Cannot send immediate alert: {Message}", alert.Message);
            return false;
        }

        // Wait for rate limit if needed
        if (!await WaitForRateLimitAsync(ct))
            return false;

        return await SendAlertInternalAsync(alert, ct);
    }

    /// <summary>
    /// Gets count of pending alerts in queue.
    /// </summary>
    public int GetPendingCount() => _alertQueue.Count;

    /// <summary>
    /// Internal method to process queued alerts.
    /// Called by TelegramWorker background service.
    /// </summary>
    public async Task<int> ProcessQueueAsync(CancellationToken ct)
    {
        if (!_config.Enabled || _botClient == null)
            return 0;

        int processed = 0;
        while (_alertQueue.TryDequeue(out TelegramAlert? alert) && !ct.IsCancellationRequested)
        {
            // Wait for rate limit
            if (!await WaitForRateLimitAsync(ct))
                break;

            bool success = await SendAlertInternalAsync(alert, ct);
            if (success)
            {
                processed++;
            }
            else
            {
                // Re-queue if retries remaining
                if (alert.RetryCount < _config.MaxRetryAttempts)
                {
                    int delay = _config.RetryDelaySeconds * (int)Math.Pow(2, alert.RetryCount);
                    DateTime nextRetry = DateTime.UtcNow.AddSeconds(delay);

                    TelegramAlert retryAlert = alert with
                    {
                        RetryCount = alert.RetryCount + 1,
                        NextRetryAtUtc = nextRetry
                    };

                    _alertQueue.Enqueue(retryAlert);
                    _logger.LogWarning("Alert send failed. Re-queued for retry {Retry}/{Max} at {NextRetry}",
                        retryAlert.RetryCount, _config.MaxRetryAttempts, nextRetry);
                }
                else
                {
                    _logger.LogError("Alert send failed after {MaxRetries} attempts. Dropping alert: {AlertId}",
                        _config.MaxRetryAttempts, alert.AlertId);
                }
            }
        }

        return processed;
    }

    /// <summary>
    /// Sends a single alert via Telegram Bot API.
    /// Returns true if sent successfully, false otherwise.
    /// </summary>
    private async Task<bool> SendAlertInternalAsync(TelegramAlert alert, CancellationToken ct)
    {
        if (_botClient == null)
            return false;

        // Skip if not yet time to retry
        if (alert.NextRetryAtUtc.HasValue && DateTime.UtcNow < alert.NextRetryAtUtc.Value)
        {
            _alertQueue.Enqueue(alert);  // Re-queue for later
            return false;
        }

        try
        {
            string message = FormatMessage(alert);

            Message sentMessage = await _botClient.SendTextMessageAsync(
                chatId: new ChatId(_config.ChatId),
                text: message,
                parseMode: ParseMode.Markdown,
                cancellationToken: ct
            );

            // Track timestamp for rate limiting
            _messageTimestamps.Enqueue(DateTime.UtcNow);

            _logger.LogInformation("Telegram alert sent successfully. AlertId={AlertId}, MessageId={MessageId}, Severity={Severity}",
                alert.AlertId, sentMessage.MessageId, alert.Severity);

            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Telegram send cancelled. AlertId={AlertId}", alert.AlertId);
            throw;  // Propagate cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram alert. AlertId={AlertId}, Retry={Retry}/{Max}",
                alert.AlertId, alert.RetryCount, _config.MaxRetryAttempts);
            return false;
        }
    }

    /// <summary>
    /// Formats alert as Telegram markdown message.
    /// Includes emoji based on severity.
    /// </summary>
    private static string FormatMessage(TelegramAlert alert)
    {
        string emoji = alert.Severity switch
        {
            AlertSeverity.Info => "ℹ️",
            AlertSeverity.Warning => "⚠️",
            AlertSeverity.Error => "❌",
            AlertSeverity.Critical => "🚨",
            _ => "📢"
        };

        StringBuilder sb = new();
        sb.AppendLine($"{emoji} *{alert.Severity}* — {EscapeMarkdown(alert.Type.ToString())}");
        sb.AppendLine();
        sb.AppendLine($"*{EscapeMarkdown(alert.Message)}*");

        if (!string.IsNullOrWhiteSpace(alert.Details))
        {
            sb.AppendLine();
            sb.AppendLine(EscapeMarkdown(alert.Details));
        }

        sb.AppendLine();
        sb.AppendLine($"🔹 Source: `{alert.SourceService}`");
        sb.AppendLine($"🕐 Time: `{alert.CreatedAtUtc:yyyy-MM-dd HH:mm:ss} UTC`");

        if (alert.RetryCount > 0)
        {
            sb.AppendLine($"🔄 Retry: {alert.RetryCount}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes Telegram markdown special characters.
    /// Prevents formatting errors in messages.
    /// </summary>
    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Escape Telegram markdown v1 special characters
        return text
            .Replace("_", "\\_")
            .Replace("*", "\\*")
            .Replace("[", "\\[")
            .Replace("`", "\\`");
    }

    // ──────────────────────────────────────────────────────────────────────
    // IAlerter implementation — severity-aware routing for the composite pipeline
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Severity-routed entry point:
    /// <list type="bullet">
    ///   <item><description>Critical / Error → immediate send (via existing <see cref="SendImmediateAsync(TelegramAlert, CancellationToken)"/> path).</description></item>
    ///   <item><description>Warning → append to digest buffer (timer will flush every N minutes).</description></item>
    ///   <item><description>Info → log only (no Telegram traffic).</description></item>
    /// </list>
    /// Never throws — transient errors are swallowed and logged.
    /// </summary>
    public async Task SendImmediateAsync(AlertSeverity severity, string title, string message, CancellationToken ct)
    {
        try
        {
            if (severity == AlertSeverity.Info)
            {
                _logger.LogInformation("[InfoAlert] {Title}: {Message}", title, message);
                return;
            }

            if (severity == AlertSeverity.Warning)
            {
                BufferWarning(title, message);
                return;
            }

            // Error / Critical → immediate send
            TelegramAlert alert = new()
            {
                AlertId = Guid.NewGuid().ToString(),
                Severity = severity,
                Type = AlertType.Error,
                Message = title,
                Details = message,
                SourceService = "TradingSupervisorService",
                CreatedAtUtc = DateTime.UtcNow
            };

            await SendImmediateAsync(alert, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TelegramAlerter.SendImmediateAsync(severity) failed. severity={Severity} title={Title}",
                severity, title);
        }
    }

    /// <summary>
    /// Explicit digest send — the <see cref="SafetyAlertsWorker"/> or timer uses this to
    /// ship a pre-buffered batch as a single Telegram message.
    /// </summary>
    public async Task SendDigestAsync(AlertSeverity severity, IReadOnlyList<(string title, string message)> entries, CancellationToken ct)
    {
        if (entries == null || entries.Count == 0)
        {
            return;
        }

        try
        {
            string combined = FormatDigest(severity, entries);
            TelegramAlert alert = new()
            {
                AlertId = Guid.NewGuid().ToString(),
                Severity = severity,
                Type = AlertType.Error,
                Message = string.Format(CultureInfo.InvariantCulture, "{0} alert digest ({1} items)", severity, entries.Count),
                Details = combined,
                SourceService = "TradingSupervisorService",
                CreatedAtUtc = DateTime.UtcNow
            };

            await SendImmediateAsync(alert, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TelegramAlerter.SendDigestAsync failed. count={Count}", entries.Count);
        }
    }

    /// <summary>
    /// Appends a Warning-level alert to the digest buffer. Arms the flush timer
    /// on the first entry; forces a flush if the buffer hits <see cref="DigestMaxEntries"/>.
    /// </summary>
    private void BufferWarning(string title, string message)
    {
        bool shouldArmTimer = false;
        bool shouldForceFlush = false;

        lock (_digestLock)
        {
            // First entry arms the timer.
            if (_digestBuffer.Count == 0)
            {
                shouldArmTimer = true;
            }

            _digestBuffer.Add((title, message, DateTime.UtcNow));

            if (_digestBuffer.Count >= DigestMaxEntries)
            {
                shouldForceFlush = true;
            }
        }

        if (shouldArmTimer)
        {
            // Start the one-shot flush timer. When it fires, we flush and disarm.
            _digestTimer.Change(_digestFlushInterval, Timeout.InfiniteTimeSpan);
            _logger.LogDebug("Digest timer armed, flush in {Minutes} min", _digestFlushInterval.TotalMinutes.ToString("F0", CultureInfo.InvariantCulture));
        }

        if (shouldForceFlush)
        {
            _logger.LogWarning("Digest buffer hit max entries ({Max}) — forcing flush", DigestMaxEntries);
            // Fire immediately by rescheduling the timer to now.
            _digestTimer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>
    /// Timer callback: drains the buffer and dispatches the digest.
    /// Swallows all errors — never allowed to bubble out of a timer callback.
    /// </summary>
    private void OnDigestFlushTick(object? state)
    {
        List<(string title, string message)> snapshot;
        lock (_digestLock)
        {
            if (_digestBuffer.Count == 0)
            {
                return;
            }
            snapshot = _digestBuffer.Select(e => (e.title, e.message)).ToList();
            _digestBuffer.Clear();
        }

        // Fire-and-forget with logging. The timer is one-shot; next BufferWarning re-arms.
        _ = Task.Run(async () =>
        {
            try
            {
                await SendDigestAsync(AlertSeverity.Warning, snapshot, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Digest flush failed. count={Count}", snapshot.Count);
            }
        });
    }

    /// <summary>
    /// Formats a digest body — one line per entry, sanitized for Telegram markdown.
    /// </summary>
    private static string FormatDigest(AlertSeverity severity, IReadOnlyList<(string title, string message)> entries)
    {
        StringBuilder sb = new();
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "*{0} digest* — {1} items", severity, entries.Count));
        sb.AppendLine();
        foreach ((string title, string message) in entries)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "• *{0}*: {1}", EscapeMarkdown(title), EscapeMarkdown(message)));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Dispose stops the digest timer; any remaining buffered entries are dropped.
    /// </summary>
    public void Dispose()
    {
        _digestTimer.Dispose();
    }

    /// <summary>
    /// Waits if rate limit would be exceeded.
    /// Returns false if wait was cancelled, true if safe to proceed.
    /// </summary>
    private async Task<bool> WaitForRateLimitAsync(CancellationToken ct)
    {
        // Clean old timestamps (older than 1 minute)
        DateTime oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        while (_messageTimestamps.TryPeek(out DateTime oldest) && oldest < oneMinuteAgo)
        {
            _messageTimestamps.TryDequeue(out _);
        }

        // Wait if at rate limit
        while (_messageTimestamps.Count >= _config.MaxMessagesPerMinute)
        {
            _logger.LogWarning("Telegram rate limit reached ({Count}/{Max}). Waiting 5 seconds...",
                _messageTimestamps.Count, _config.MaxMessagesPerMinute);

            try
            {
                await Task.Delay(5000, ct);
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            // Clean old timestamps again
            oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
            while (_messageTimestamps.TryPeek(out DateTime oldest) && oldest < oneMinuteAgo)
            {
                _messageTimestamps.TryDequeue(out _);
            }
        }

        return true;
    }
}
