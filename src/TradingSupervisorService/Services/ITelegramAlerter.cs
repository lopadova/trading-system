using SharedKernel.Domain;

namespace TradingSupervisorService.Services;

/// <summary>
/// Interface for Telegram alerting service.
/// Sends alerts to configured Telegram chat via Bot API.
/// </summary>
public interface ITelegramAlerter
{
    /// <summary>
    /// Queues an alert to be sent via Telegram.
    /// Alert is added to internal queue and processed asynchronously.
    /// </summary>
    /// <param name="alert">Alert to send</param>
    Task QueueAlertAsync(TelegramAlert alert);

    /// <summary>
    /// Sends an alert immediately (bypassing queue).
    /// Used for critical alerts that cannot wait for queue processing.
    /// </summary>
    /// <param name="alert">Alert to send</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if sent successfully, false otherwise</returns>
    Task<bool> SendImmediateAsync(TelegramAlert alert, CancellationToken ct);

    /// <summary>
    /// Gets count of pending alerts in queue.
    /// </summary>
    int GetPendingCount();

    /// <summary>
    /// Processes queued alerts asynchronously.
    /// Called by TelegramWorker background service.
    /// Returns number of alerts successfully processed.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Number of alerts processed</returns>
    Task<int> ProcessQueueAsync(CancellationToken ct);
}
