using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using System.Text.Json;
using TradingSupervisorService.Collectors;
using TradingSupervisorService.Repositories;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Background service that periodically collects machine metrics and writes heartbeat to database.
/// Runs on a configurable interval (default: 60 seconds).
/// Continues running on errors to ensure monitoring doesn't stop.
/// </summary>
public sealed class HeartbeatWorker : BackgroundService
{
    private readonly ILogger<HeartbeatWorker> _logger;
    private readonly IMachineMetricsCollector _metricsCollector;
    private readonly IHeartbeatRepository _heartbeatRepo;
    private readonly IOutboxRepository _outboxRepo;
    private readonly int _intervalSeconds;
    private readonly TradingMode _tradingMode;
    private readonly string _serviceVersion;
    private readonly string _serviceName;

    public HeartbeatWorker(
        ILogger<HeartbeatWorker> logger,
        IMachineMetricsCollector metricsCollector,
        IHeartbeatRepository heartbeatRepo,
        IOutboxRepository outboxRepo,
        IConfiguration config)
    {
        _logger = logger;
        _metricsCollector = metricsCollector;
        _heartbeatRepo = heartbeatRepo;
        _outboxRepo = outboxRepo;

        // Read configuration with safe defaults
        _intervalSeconds = config.GetValue<int>("Monitoring:IntervalSeconds", 60);

        // Parse TradingMode from config (defaults to Paper for safety)
        string tradingModeStr = config.GetValue<string>("TradingMode", "paper") ?? "paper";
        _tradingMode = tradingModeStr.ToLowerInvariant() switch
        {
            "live" => TradingMode.Live,
            "paper" => TradingMode.Paper,
            _ => TradingMode.Paper  // Unknown values default to safe mode
        };

        // Service metadata
        _serviceVersion = "1.0.0";  // TODO: Read from assembly version in production
        _serviceName = "TradingSupervisorService";

        // Validate configuration
        if (_intervalSeconds <= 0)
        {
            throw new ArgumentException($"Invalid Monitoring:IntervalSeconds={_intervalSeconds}. Must be > 0.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "{Worker} started. Interval={Interval}s, TradingMode={Mode}, Version={Version}",
            nameof(HeartbeatWorker), _intervalSeconds, _tradingMode, _serviceVersion);

        // Main loop: collect metrics and write heartbeat on interval
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);

            // Wait for next cycle (use Task.Delay for cancellation support)
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutdown requested - exit gracefully
                _logger.LogInformation("{Worker} shutdown requested", nameof(HeartbeatWorker));
                break;
            }
        }

        _logger.LogInformation("{Worker} stopped", nameof(HeartbeatWorker));
    }

    /// <summary>
    /// Single heartbeat cycle: collect metrics and write to database.
    /// Errors are logged but do not crash the worker (retry on next cycle).
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("HeartbeatWorker: Starting heartbeat cycle");

            // Collect machine metrics (CPU, RAM, disk, uptime)
            MachineMetrics metrics = await _metricsCollector.CollectAsync(ct);

            // Build heartbeat record
            DateTime now = DateTime.UtcNow;
            ServiceHeartbeat heartbeat = new()
            {
                ServiceName = _serviceName,
                Hostname = metrics.Hostname,
                LastSeenAt = now.ToString("O"),  // ISO8601 format
                UptimeSeconds = metrics.UptimeSeconds,
                CpuPercent = metrics.CpuPercent,
                RamPercent = metrics.RamPercent,
                DiskFreeGb = metrics.DiskFreeGb,
                TradingMode = _tradingMode.ToString().ToLowerInvariant(),
                Version = _serviceVersion,
                CreatedAt = now.ToString("O"),
                UpdatedAt = now.ToString("O")
            };

            // Write to database (upsert - creates or updates existing record)
            await _heartbeatRepo.UpsertAsync(heartbeat, ct);

            // Create outbox entry for remote sync to Cloudflare Worker
            string eventId = Guid.NewGuid().ToString();
            string payloadJson = JsonSerializer.Serialize(heartbeat, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            OutboxEntry outboxEntry = new()
            {
                EventId = eventId,
                EventType = "heartbeat",
                PayloadJson = payloadJson,
                DedupeKey = $"heartbeat:{heartbeat.ServiceName}:{now:yyyy-MM-dd-HH-mm}",  // Dedupe by service + minute
                Status = "pending",
                RetryCount = 0,
                CreatedAt = now.ToString("O")
            };

            await _outboxRepo.InsertAsync(outboxEntry, ct);

            _logger.LogInformation(
                "✓ Heartbeat saved: CPU={Cpu}%, RAM={Ram}%, Disk={Disk}GB → Queued for sync (EventId={EventId})",
                heartbeat.CpuPercent.ToString("F1"), heartbeat.RamPercent.ToString("F1"), heartbeat.DiskFreeGb.ToString("F1"), eventId);
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress - don't log as error
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} cycle failed. Will retry in {Interval}s",
                nameof(HeartbeatWorker), _intervalSeconds);
            // Do NOT rethrow - worker must survive errors and retry on next cycle
        }
    }
}
