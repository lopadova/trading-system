using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TradingSupervisorService.Repositories;

namespace TradingSupervisorService.Workers;

/// <summary>
/// BackgroundService that syncs sync_outbox events to Cloudflare Worker API.
/// Implements retry logic with exponential backoff for failed syncs.
/// Runs on a configurable interval and processes pending/failed events in batches.
/// </summary>
public sealed class OutboxSyncWorker : BackgroundService
{
    private readonly ILogger<OutboxSyncWorker> _logger;
    private readonly IOutboxRepository _outbox;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;

    // Configuration values (loaded from appsettings.json)
    private readonly int _intervalSeconds;
    private readonly int _batchSize;
    private readonly string _workerUrl;
    private readonly string _apiKey;
    private readonly int _initialRetryDelaySeconds;
    private readonly int _maxRetryDelaySeconds;
    private readonly int _maxRetries;
    private readonly int _startupDelaySeconds;

    public OutboxSyncWorker(
        ILogger<OutboxSyncWorker> logger,
        IOutboxRepository outbox,
        IConfiguration config,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _outbox = outbox;
        _config = config;
        _httpClient = httpClientFactory.CreateClient();

        // Load configuration with defaults
        _intervalSeconds = config.GetValue("OutboxSync:IntervalSeconds", 30);
        _batchSize = config.GetValue("OutboxSync:BatchSize", 50);
        _workerUrl = config.GetValue("Cloudflare:WorkerUrl", "")!;
        _apiKey = config.GetValue("Cloudflare:ApiKey", "")!;
        _initialRetryDelaySeconds = config.GetValue("OutboxSync:InitialRetryDelaySeconds", 5);
        _maxRetryDelaySeconds = config.GetValue("OutboxSync:MaxRetryDelaySeconds", 300);
        _maxRetries = config.GetValue("OutboxSync:MaxRetries", 10);
        _startupDelaySeconds = config.GetValue("OutboxSync:StartupDelaySeconds", 5);

        // Validate critical configuration
        if (string.IsNullOrWhiteSpace(_workerUrl))
        {
            _logger.LogWarning("Cloudflare:WorkerUrl is not configured. OutboxSyncWorker will run but skip sync.");
        }

        // Configure HttpClient
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _apiKey);
        }
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Worker} started. Interval={Interval}s, BatchSize={BatchSize}, WorkerUrl={WorkerUrl}",
            nameof(OutboxSyncWorker), _intervalSeconds, _batchSize, _workerUrl);

        // Wait on startup to let other services initialize (configurable for testing)
        if (_startupDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_startupDelaySeconds), stoppingToken).ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);

            // Wait for the configured interval before next sync
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken)
                      .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Runs a single sync cycle: fetch pending events, send to Cloudflare, update status.
    /// Errors are logged but do NOT crash the worker (survives errors and retries next cycle).
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            // Skip sync if Cloudflare Worker URL is not configured
            if (string.IsNullOrWhiteSpace(_workerUrl))
            {
                return;
            }

            // Fetch pending/failed events ready for processing
            IReadOnlyList<OutboxEntry> pending = await _outbox.GetPendingAsync(_batchSize, ct);

            if (pending.Count == 0)
            {
                _logger.LogTrace("No pending outbox events to sync");
                return;
            }

            _logger.LogDebug("Found {Count} pending outbox events to sync", pending.Count);

            // Process each event individually
            // (Could be optimized to batch send, but individual processing provides better error granularity)
            int successCount = 0;
            int failCount = 0;

            foreach (OutboxEntry entry in pending)
            {
                bool success = await SendEventAsync(entry, ct);

                if (success)
                {
                    await _outbox.MarkSentAsync(entry.EventId, ct);
                    successCount++;
                }
                else
                {
                    // Calculate next retry time with exponential backoff
                    DateTime nextRetry = CalculateNextRetry(entry.RetryCount);
                    string errorMsg = $"HTTP request failed (see logs). Retry {entry.RetryCount + 1}/{_maxRetries}";

                    await _outbox.MarkFailedAsync(entry.EventId, errorMsg, nextRetry, ct);
                    failCount++;

                    // Log warning if max retries exceeded
                    if (entry.RetryCount + 1 >= _maxRetries)
                    {
                        _logger.LogWarning("Event {EventId} has reached max retries ({MaxRetries}). Manual intervention may be required.",
                            entry.EventId, _maxRetries);
                    }
                }
            }

            _logger.LogInformation("Outbox sync cycle completed: {Success} sent, {Failed} failed",
                successCount, failCount);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — do not log
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} cycle failed. Retry in {Interval}s",
                nameof(OutboxSyncWorker), _intervalSeconds);
            // Do NOT rethrow - worker must survive errors and retry on next cycle
        }
    }

    /// <summary>
    /// Sends a single outbox event to the Cloudflare Worker API.
    /// Returns true if successful (HTTP 2xx), false otherwise.
    /// All HTTP errors are logged but not thrown (handled by retry logic).
    /// </summary>
    private async Task<bool> SendEventAsync(OutboxEntry entry, CancellationToken ct)
    {
        try
        {
            // Build the API endpoint URL (e.g., https://worker.dev/api/v1/ingest)
            string endpoint = $"{_workerUrl.TrimEnd('/')}/api/v1/ingest";

            // Prepare request body (the outbox entry payload is already JSON)
            // Wrap it in an envelope with metadata
            var payload = new
            {
                event_id = entry.EventId,
                event_type = entry.EventType,
                payload = JsonDocument.Parse(entry.PayloadJson).RootElement,  // Parse JSON string to object
                dedupe_key = entry.DedupeKey,
                created_at = entry.CreatedAt
            };

            string jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false
            });

            StringContent content = new(jsonPayload, Encoding.UTF8, "application/json");

            // Send HTTP POST request
            HttpResponseMessage response = await _httpClient.PostAsync(endpoint, content, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully sent event {EventId} to Cloudflare Worker", entry.EventId);
                return true;
            }

            // Log error response for debugging
            string responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Failed to send event {EventId}. Status={StatusCode}, Response={Response}",
                entry.EventId, response.StatusCode, responseBody);

            return false;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed for event {EventId}", entry.EventId);
            return false;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "HTTP request timeout for event {EventId}", entry.EventId);
            return false;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse payload JSON for event {EventId}. Payload={Payload}",
                entry.EventId, entry.PayloadJson);
            // JSON parse error is permanent — should not retry
            // Mark as sent to avoid infinite retries on corrupt data
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error sending event {EventId}", entry.EventId);
            return false;
        }
    }

    /// <summary>
    /// Calculates next retry timestamp using exponential backoff.
    /// Formula: delay = min(initial * 2^retry_count, max_delay)
    /// Example: 5s, 10s, 20s, 40s, 80s, 160s, 300s (capped at 300s)
    /// </summary>
    private DateTime CalculateNextRetry(int retryCount)
    {
        // Calculate delay in seconds with exponential backoff
        int delaySeconds = _initialRetryDelaySeconds * (int)Math.Pow(2, retryCount);

        // Cap delay at maximum
        delaySeconds = Math.Min(delaySeconds, _maxRetryDelaySeconds);

        // Add delay to current UTC time
        return DateTime.UtcNow.AddSeconds(delaySeconds);
    }

    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}
