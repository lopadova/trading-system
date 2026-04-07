using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TradingSupervisorService.Services;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Background worker that processes Telegram alert queue.
/// Runs continuously, processing queued alerts with configured interval.
/// </summary>
public sealed class TelegramWorker : BackgroundService
{
    private readonly ILogger<TelegramWorker> _logger;
    private readonly TelegramAlerter _alerter;
    private readonly int _processIntervalSeconds;

    public TelegramWorker(
        ILogger<TelegramWorker> logger,
        ITelegramAlerter alerter,
        IConfiguration configuration)
    {
        _logger = logger;

        // Cast to concrete type to access ProcessQueueAsync
        // (interface doesn't expose it as it's internal implementation detail)
        _alerter = (TelegramAlerter)alerter;

        _processIntervalSeconds = configuration.GetValue<int>("Telegram:ProcessIntervalSeconds", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Worker} started. ProcessInterval={Interval}s",
            nameof(TelegramWorker), _processIntervalSeconds);

        // Wait a short time on startup to allow other services to initialize
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_processIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown - process remaining alerts
                _logger.LogInformation("{Worker} shutting down. Processing remaining alerts...", nameof(TelegramWorker));
                await RunCycleAsync(CancellationToken.None);
                break;
            }
        }

        _logger.LogInformation("{Worker} stopped", nameof(TelegramWorker));
    }

    /// <summary>
    /// Runs one processing cycle: dequeue and send all pending alerts.
    /// Errors are logged but do not crash the worker.
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            int pendingBefore = _alerter.GetPendingCount();
            if (pendingBefore == 0)
            {
                // No alerts to process - skip logging to reduce noise
                return;
            }

            _logger.LogDebug("{Worker} processing {Count} pending alerts...",
                nameof(TelegramWorker), pendingBefore);

            int processed = await _alerter.ProcessQueueAsync(ct);

            int pendingAfter = _alerter.GetPendingCount();

            if (processed > 0)
            {
                _logger.LogInformation("{Worker} processed {Processed} alerts. Remaining={Remaining}",
                    nameof(TelegramWorker), processed, pendingAfter);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown - do not log as error
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} cycle failed. Retry in {Interval}s",
                nameof(TelegramWorker), _processIntervalSeconds);
            // Do NOT rethrow - worker must survive errors and retry on next cycle
        }
    }
}
