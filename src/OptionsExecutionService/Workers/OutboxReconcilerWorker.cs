using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OptionsExecutionService.Repositories;

namespace OptionsExecutionService.Workers;

/// <summary>
/// Background worker that processes pending outbox entries for crash recovery.
/// Implements the reconciler pattern: retries pending broker operations after service restart.
/// Phase 1: State Persistence & Idempotency - Task #19
/// </summary>
public sealed class OutboxReconcilerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OutboxReconcilerWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _interval;

    public OutboxReconcilerWorker(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<OutboxReconcilerWorker> logger,
        IConfiguration configuration)
    {
        _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Read reconciler interval from config (default: 60 seconds)
        var intervalSeconds = int.Parse(_configuration["OutboxReconciler:IntervalSeconds"] ?? "60");
        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxReconcilerWorker starting (interval: {Interval})", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEntriesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // CRITICAL: Worker must never crash - log error and continue
                _logger.LogError(ex, "Error processing outbox entries");
            }

            // Wait for next iteration
            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("OutboxReconcilerWorker stopping");
    }

    private async Task ProcessPendingEntriesAsync(CancellationToken ct)
    {
        // Create a scope for scoped repository (BackgroundService is singleton, repository is scoped)
        using IServiceScope scope = _serviceScopeFactory.CreateScope();
        IOrderOutboxRepository repository = scope.ServiceProvider.GetRequiredService<IOrderOutboxRepository>();

        // Get pending entries (limit 100 per iteration to avoid memory issues)
        var pendingEntries = await repository.GetPendingAsync(limit: 100, ct);

        if (pendingEntries.Count == 0)
        {
            _logger.LogDebug("No pending outbox entries");
            return;
        }

        _logger.LogInformation("Processing {Count} pending outbox entries", pendingEntries.Count);

        foreach (var entry in pendingEntries)
        {
            try
            {
                // Process the entry (for now, just mark as sent - actual broker integration in later tasks)
                await ProcessEntryAsync(entry, ct);

                // Mark as sent
                await repository.MarkSentAsync(entry.OutboxId, ct);

                _logger.LogInformation(
                    "Processed outbox entry: OutboxId={OutboxId} OrderId={OrderId} Operation={Operation}",
                    entry.OutboxId, entry.OrderId, entry.Operation);
            }
            catch (Exception ex)
            {
                // Mark entry as failed
                _logger.LogError(ex,
                    "Failed to process outbox entry: OutboxId={OutboxId} OrderId={OrderId}",
                    entry.OutboxId, entry.OrderId);

                try
                {
                    await repository.MarkFailedAsync(entry.OutboxId, ct);
                }
                catch (Exception markEx)
                {
                    _logger.LogError(markEx,
                        "Failed to mark entry as failed: OutboxId={OutboxId}",
                        entry.OutboxId);
                }
            }
        }
    }

    private async Task ProcessEntryAsync(OrderOutboxEntry entry, CancellationToken ct)
    {
        // Validate entry
        if (string.IsNullOrWhiteSpace(entry.Payload))
        {
            throw new InvalidOperationException($"Entry {entry.OutboxId} has empty payload");
        }

        // TODO (Task #20): Parse payload and execute actual broker operation
        // For now, just validate JSON format
        try
        {
            // Simple JSON validation - will throw if invalid
            System.Text.Json.JsonDocument.Parse(entry.Payload);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidOperationException($"Entry {entry.OutboxId} has invalid JSON payload", ex);
        }

        // Simulate async processing
        await Task.CompletedTask;

        _logger.LogDebug(
            "Validated outbox entry: OutboxId={OutboxId} Operation={Operation}",
            entry.OutboxId, entry.Operation);
    }
}
