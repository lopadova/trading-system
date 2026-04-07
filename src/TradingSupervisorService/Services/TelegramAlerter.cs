using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
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
/// Telegram alerting service implementation.
/// Queues alerts and sends them via Telegram Bot API with retry logic and rate limiting.
/// Thread-safe for concurrent queueing.
/// </summary>
public sealed class TelegramAlerter : ITelegramAlerter
{
    private readonly TelegramConfig _config;
    private readonly ILogger<TelegramAlerter> _logger;
    private readonly TelegramBotClient? _botClient;
    private readonly ConcurrentQueue<TelegramAlert> _alertQueue;

    // Rate limiting: track message timestamps in last minute
    private readonly ConcurrentQueue<DateTime> _messageTimestamps;

    public TelegramAlerter(IConfiguration configuration, ILogger<TelegramAlerter> logger)
    {
        _logger = logger;
        _alertQueue = new ConcurrentQueue<TelegramAlert>();
        _messageTimestamps = new ConcurrentQueue<DateTime>();

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
