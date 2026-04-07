using SharedKernel.Domain;
using TradingSupervisorService.Services;

namespace TradingSupervisorService.Examples;

/// <summary>
/// Example usage of TelegramAlerter service.
/// Shows how to queue alerts and send critical alerts immediately.
/// </summary>
public static class TelegramAlerterExample
{
    /// <summary>
    /// Example: Queue a non-critical alert.
    /// Alert will be sent asynchronously by TelegramWorker.
    /// </summary>
    public static async Task QueueInfoAlertExample(ITelegramAlerter alerter)
    {
        TelegramAlert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            Severity = AlertSeverity.Info,
            Type = AlertType.SystemHealth,
            Message = "Trading system started successfully",
            Details = "All services initialized. Mode: Paper Trading",
            SourceService = "TradingSupervisorService",
            CreatedAtUtc = DateTime.UtcNow
        };

        await alerter.QueueAlertAsync(alert);
    }

    /// <summary>
    /// Example: Send critical alert immediately.
    /// Blocks until sent or fails. Use only for critical alerts.
    /// </summary>
    public static async Task SendCriticalAlertExample(ITelegramAlerter alerter, CancellationToken ct)
    {
        TelegramAlert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            Severity = AlertSeverity.Critical,
            Type = AlertType.RiskManagement,
            Message = "CRITICAL: Max daily loss exceeded",
            Details = "Current loss: -$5,000\nMax allowed: -$3,000\nTrading halted automatically.",
            SourceService = "OptionsExecutionService",
            CreatedAtUtc = DateTime.UtcNow
        };

        bool sent = await alerter.SendImmediateAsync(alert, ct);
        if (!sent)
        {
            // Log failure - alert was not sent
            // Consider fallback notification method
        }
    }

    /// <summary>
    /// Example: Queue alert with retry tracking.
    /// Demonstrates how retry count is tracked for failed sends.
    /// </summary>
    public static async Task QueueAlertWithRetryExample(ITelegramAlerter alerter)
    {
        TelegramAlert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            Severity = AlertSeverity.Error,
            Type = AlertType.ConnectionStatus,
            Message = "IBKR connection lost",
            Details = "Attempting automatic reconnection...",
            SourceService = "TradingSupervisorService",
            CreatedAtUtc = DateTime.UtcNow,
            RetryCount = 0,  // First attempt
            NextRetryAtUtc = null  // No retry scheduled yet
        };

        await alerter.QueueAlertAsync(alert);

        // If send fails, TelegramWorker will:
        // 1. Increment RetryCount
        // 2. Calculate NextRetryAtUtc using exponential backoff
        // 3. Re-queue alert for retry (up to MaxRetryAttempts)
    }
}
