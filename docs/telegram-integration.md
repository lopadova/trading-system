# Telegram Alert Integration

Complete guide for configuring and using Telegram alerts in the Trading System.

## Overview
The Telegram Alert Integration sends critical system events and trading alerts to a configured Telegram chat via the Telegram Bot API. Alerts are queued for asynchronous processing with automatic retry logic and rate limiting.

## Features
- **Queue-based processing**: Non-blocking alert queueing with background worker
- **Retry logic**: Exponential backoff (5s → 10s → 20s → 40s...) with configurable max retries
- **Rate limiting**: Sliding window rate limiter (default 20 messages/minute)
- **Graceful degradation**: Service continues running if Telegram is unavailable or misconfigured
- **Markdown formatting**: Rich message formatting with severity-based emoji
- **Immediate send**: Critical alerts can bypass queue for instant delivery

## Setup

### 1. Create Telegram Bot
1. Open Telegram and search for **@BotFather**
2. Send `/newbot` command
3. Follow prompts to name your bot (e.g., "Trading System Alerts")
4. Copy the bot token (format: `123456789:ABCdefGHIjklMNOpqrsTUVwxyz`)

### 2. Get Chat ID
**For personal messages:**
1. Start a conversation with your bot
2. Send any message to it
3. Visit: `https://api.telegram.org/bot<YOUR_BOT_TOKEN>/getUpdates`
4. Find `"chat":{"id":123456789}` in the response

**For group chat:**
1. Add the bot to your group
2. Send a message mentioning the bot: `@YourBotName hello`
3. Visit the same getUpdates URL
4. Find the chat ID (will be negative for groups, e.g., `-987654321`)

### 3. Configure appsettings.json
Edit `src/TradingSupervisorService/appsettings.json`:

```json
{
  "Telegram": {
    "Enabled": true,
    "BotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
    "ChatId": 123456789,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 5,
    "MaxMessagesPerMinute": 20,
    "ProcessIntervalSeconds": 5
  }
}
```

**Configuration Options:**
- `Enabled` (bool): Enable/disable Telegram alerting. Default: `true`
- `BotToken` (string): Telegram Bot API token from @BotFather. Required if enabled.
- `ChatId` (long): Target chat ID (user or group). Required if enabled.
- `MaxRetryAttempts` (int): Max send retries before dropping alert. Range: 0-10. Default: `3`
- `RetryDelaySeconds` (int): Initial retry delay (doubles each retry). Range: 1-300. Default: `5`
- `MaxMessagesPerMinute` (int): Rate limit for API calls. Default: `20`
- `ProcessIntervalSeconds` (int): Queue processing interval. Default: `5`

### 4. Restart Service
```bash
# If running as Windows Service
net stop TradingSupervisorService
net start TradingSupervisorService

# If running in development
dotnet run --project src/TradingSupervisorService
```

## Usage

### Queue an Alert (Non-blocking)
Most alerts should be queued for background processing:

```csharp
using TradingSupervisorService.Services;
using SharedKernel.Domain;

// Inject ITelegramAlerter in constructor
public class MyService
{
    private readonly ITelegramAlerter _telegramAlerter;

    public MyService(ITelegramAlerter telegramAlerter)
    {
        _telegramAlerter = telegramAlerter;
    }

    public async Task OnSystemEventAsync()
    {
        await _telegramAlerter.QueueAlertAsync(new TelegramAlert
        {
            AlertId = Guid.NewGuid().ToString(),
            Severity = AlertSeverity.Warning,
            Type = AlertType.SystemHealth,
            Message = "CPU usage high",
            Details = "Current: 85%\nThreshold: 80%",
            SourceService = "TradingSupervisorService",
            CreatedAtUtc = DateTime.UtcNow
        });
    }
}
```

### Send Critical Alert Immediately (Blocking)
For critical events that require immediate notification:

```csharp
public async Task OnCriticalEventAsync(CancellationToken ct)
{
    bool sent = await _telegramAlerter.SendImmediateAsync(new TelegramAlert
    {
        AlertId = Guid.NewGuid().ToString(),
        Severity = AlertSeverity.Critical,
        Type = AlertType.RiskManagement,
        Message = "CRITICAL: Max daily loss exceeded",
        Details = "Trading halted automatically.",
        SourceService = "OptionsExecutionService",
        CreatedAtUtc = DateTime.UtcNow
    }, ct);

    if (!sent)
    {
        // Fallback: log to file, send email, etc.
        _logger.LogError("Failed to send critical Telegram alert");
    }
}
```

### Check Queue Status
```csharp
int pendingAlerts = _telegramAlerter.GetPendingCount();
_logger.LogInformation("Telegram queue has {Count} pending alerts", pendingAlerts);
```

## Alert Severity and Emoji
Alerts are formatted with severity-based emoji:

| Severity | Emoji | When to Use |
|----------|-------|-------------|
| `Info` | ℹ️ | Routine operations, confirmations |
| `Warning` | ⚠️ | Potential issues, threshold breaches |
| `Error` | ❌ | Actionable problems, failed operations |
| `Critical` | 🚨 | Severe issues requiring immediate attention |

## Message Format Example
```
🚨 Critical — RiskManagement

Max daily loss exceeded

Current loss: -$5,000
Max allowed: -$3,000
Trading halted automatically.

🔹 Source: `OptionsExecutionService`
🕐 Time: `2026-04-05 14:30:00 UTC`
```

## Retry Logic
Failed sends are retried with exponential backoff:

1. **First attempt** fails → retry in 5 seconds
2. **Second attempt** fails → retry in 10 seconds
3. **Third attempt** fails → retry in 20 seconds
4. **Fourth attempt** fails → alert dropped (logged as error)

## Rate Limiting
The service enforces a sliding window rate limit (default 20 messages/minute) to comply with Telegram API limits:
- Telegram official limit: 30 messages/second per bot
- Our conservative limit: 20 messages/minute (configurable)
- If limit reached, worker waits 5 seconds before retrying

## Error Handling
The service is designed to **never crash** from Telegram errors:

1. **Invalid configuration**: Service disables with warning log
2. **Network failures**: Alerts are retried with exponential backoff
3. **Rate limit exceeded**: Worker waits and retries
4. **Markdown errors**: User content is escaped automatically
5. **Max retries exceeded**: Alert is dropped with error log

## Monitoring

### Logs
All Telegram operations are logged:

```
[INFO] TelegramAlerter initialized. ChatId=123456789, MaxRetries=3
[DEBUG] Alert queued for Telegram. QueueSize=5, AlertId=abc123
[INFO] Telegram alert sent successfully. AlertId=abc123, MessageId=456, Severity=Warning
[WARNING] Telegram rate limit reached (20/20). Waiting 5 seconds...
[ERROR] Failed to send Telegram alert. AlertId=xyz789, Retry=3/3
```

### Queue Size
Monitor queue size in logs to detect backlog:

```
[INFO] TelegramWorker processed 15 alerts. Remaining=0
```

## Troubleshooting

### Alerts not being sent
1. Check `Telegram:Enabled` is `true` in appsettings.json
2. Verify bot token and chat ID are correct
3. Check service logs for errors
4. Test bot manually: send message to bot and verify response
5. Check network connectivity to api.telegram.org

### "Telegram configuration invalid" warning
- Missing or empty `BotToken`
- `ChatId` is 0
- Invalid `MaxRetryAttempts` or `RetryDelaySeconds`

### Messages formatted incorrectly
- User content with special characters (`_`, `*`, `[`, `` ` ``) is automatically escaped
- If formatting issues persist, check Telegram API docs for markdown syntax

### Rate limit errors
- Reduce `MaxMessagesPerMinute` in configuration
- Check if multiple services are using the same bot (shared rate limit)
- Consider batching related alerts into single message

## Security Best Practices
1. **Never commit bot token** to source control
2. Use User Secrets for development: `dotnet user-secrets set "Telegram:BotToken" "your-token"`
3. For production, use environment variables or Azure Key Vault
4. Restrict bot permissions: disable unused features in @BotFather
5. Monitor bot activity in Telegram: check for unauthorized chats

## Integration Points
Telegram alerts integrate with:

- **AlertRepository**: Send DB alerts to Telegram
- **HeartbeatWorker**: Alert on service health issues
- **IBKR Connection Manager**: Alert on connection failures
- **Log Reader**: Forward critical log errors
- **Order Placer**: Alert on order rejections
- **Risk Manager**: Alert on limit breaches

## Testing

### Unit Tests
Run tests with .NET SDK installed:
```bash
dotnet test --filter TestId=TEST-05-* --logger "console;verbosity=detailed"
```

### Manual Testing
1. Set `Enabled: false` initially
2. Start service and verify it runs without errors
3. Set `Enabled: true` with valid config
4. Restart service
5. Trigger an alert via code or API
6. Check Telegram chat for message
7. Verify logs show successful send

### Test Alert Script
Create `scripts/test-telegram.sh`:
```bash
#!/bin/bash
BOT_TOKEN="your-bot-token"
CHAT_ID="your-chat-id"
MESSAGE="Test alert from Trading System"

curl -X POST "https://api.telegram.org/bot${BOT_TOKEN}/sendMessage" \
  -d "chat_id=${CHAT_ID}" \
  -d "text=${MESSAGE}"
```

## Performance
- **Queue throughput**: Processes queue every 5 seconds (configurable)
- **Send latency**: ~100-500ms per message (network dependent)
- **Memory usage**: Minimal (ConcurrentQueue holds alerts in memory until sent)
- **CPU usage**: Negligible (async I/O, no compute-intensive operations)

## Future Enhancements
Potential improvements for future tasks:
- [ ] Add support for inline keyboards (buttons)
- [ ] Add support for photo/file attachments
- [ ] Implement message templates for common alerts
- [ ] Add Telegram command handlers (e.g., `/status`, `/positions`)
- [ ] Support multiple chat IDs (different alerts to different chats)
- [ ] Add alert grouping (batch similar alerts into one message)

---
*Last updated: 2026-04-05 by T-05 agent*
