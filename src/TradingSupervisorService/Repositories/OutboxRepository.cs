using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;

namespace TradingSupervisorService.Repositories;

/// <summary>
/// Data model for outbox event records.
/// Immutable record for type safety and value equality.
/// </summary>
public sealed record OutboxEntry
{
    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public string? DedupeKey { get; init; }
    public string Status { get; init; } = "pending";  // "pending", "sent", "failed"
    public int RetryCount { get; init; }
    public string? LastError { get; init; }
    public string? NextRetryAt { get; init; }  // ISO8601
    public string CreatedAt { get; init; } = string.Empty;  // ISO8601
    public string? SentAt { get; init; }  // ISO8601
}

/// <summary>
/// Repository interface for sync_outbox table.
/// Implements the Transactional Outbox pattern for reliable event publishing.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Inserts a new outbox entry.
    /// Uses INSERT OR IGNORE on dedupe_key to prevent duplicates.
    /// </summary>
    Task InsertAsync(OutboxEntry entry, CancellationToken ct);

    /// <summary>
    /// Gets pending/failed events ready for processing.
    /// Returns events where status is 'pending' or 'failed' AND next_retry_at is due.
    /// </summary>
    Task<IReadOnlyList<OutboxEntry>> GetPendingAsync(int batchSize, CancellationToken ct);

    /// <summary>
    /// Marks an event as successfully sent.
    /// Updates status to 'sent' and sets sent_at timestamp.
    /// </summary>
    Task MarkSentAsync(string eventId, CancellationToken ct);

    /// <summary>
    /// Marks an event as failed and schedules retry with exponential backoff.
    /// </summary>
    Task MarkFailedAsync(string eventId, string errorMessage, DateTime nextRetryAt, CancellationToken ct);
}

/// <summary>
/// SQLite implementation of IOutboxRepository using Dapper.
/// All queries use explicit SQL (no ORM).
/// All IO operations have try/catch with logging.
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<OutboxRepository> _logger;

    public OutboxRepository(IDbConnectionFactory db, ILogger<OutboxRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Inserts a new outbox entry using INSERT OR IGNORE for deduplication.
    /// If dedupe_key matches an existing entry, insert is silently ignored (idempotent).
    /// </summary>
    public async Task InsertAsync(OutboxEntry entry, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (entry == null)
        {
            throw new ArgumentNullException(nameof(entry));
        }
        if (string.IsNullOrWhiteSpace(entry.EventId))
        {
            throw new ArgumentException("EventId cannot be null or empty", nameof(entry));
        }
        if (string.IsNullOrWhiteSpace(entry.EventType))
        {
            throw new ArgumentException("EventType cannot be null or empty", nameof(entry));
        }

        const string sql = """
            INSERT OR IGNORE INTO sync_outbox
                (event_id, event_type, payload_json, dedupe_key, status,
                 retry_count, last_error, next_retry_at, created_at, sent_at)
            VALUES
                (@EventId, @EventType, @PayloadJson, @DedupeKey, @Status,
                 @RetryCount, @LastError, @NextRetryAt, @CreatedAt, @SentAt)
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new
            {
                entry.EventId,
                entry.EventType,
                entry.PayloadJson,
                entry.DedupeKey,
                entry.Status,
                entry.RetryCount,
                entry.LastError,
                entry.NextRetryAt,
                entry.CreatedAt,
                entry.SentAt
            }, cancellationToken: ct);

            int rowsAffected = await conn.ExecuteAsync(cmd);

            // rowsAffected == 0 means dedupe_key matched (normal, not an error)
            if (rowsAffected == 0)
            {
                _logger.LogDebug("Outbox entry {EventId} was a duplicate (dedupe_key={DedupeKey}), ignored",
                    entry.EventId, entry.DedupeKey);
            }
            else
            {
                _logger.LogDebug("Inserted outbox entry {EventId} type={EventType}",
                    entry.EventId, entry.EventType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert outbox entry {EventId}", entry.EventId);
            throw;
        }
    }

    /// <summary>
    /// Gets pending/failed events ready for processing.
    /// Uses LIMIT for safety (pagination).
    /// </summary>
    public async Task<IReadOnlyList<OutboxEntry>> GetPendingAsync(int batchSize, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (batchSize <= 0)
        {
            throw new ArgumentException("BatchSize must be positive", nameof(batchSize));
        }

        const string sql = """
            SELECT
                event_id AS EventId,
                event_type AS EventType,
                payload_json AS PayloadJson,
                dedupe_key AS DedupeKey,
                status AS Status,
                retry_count AS RetryCount,
                last_error AS LastError,
                next_retry_at AS NextRetryAt,
                created_at AS CreatedAt,
                sent_at AS SentAt
            FROM sync_outbox
            WHERE status IN ('pending', 'failed')
              AND (next_retry_at IS NULL OR next_retry_at <= datetime('now'))
            ORDER BY created_at ASC
            LIMIT @BatchSize
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new { BatchSize = batchSize },
                cancellationToken: ct);
            IEnumerable<OutboxEntry> results = await conn.QueryAsync<OutboxEntry>(cmd);

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending outbox entries");
            throw;
        }
    }

    /// <summary>
    /// Marks an event as successfully sent.
    /// Updates status to 'sent' and records sent_at timestamp.
    /// </summary>
    public async Task MarkSentAsync(string eventId, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("EventId cannot be null or empty", nameof(eventId));
        }

        const string sql = """
            UPDATE sync_outbox
            SET status = 'sent',
                sent_at = datetime('now')
            WHERE event_id = @EventId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new { EventId = eventId },
                cancellationToken: ct);
            int rowsAffected = await conn.ExecuteAsync(cmd);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Attempted to mark event {EventId} as sent, but it was not found",
                    eventId);
            }
            else
            {
                _logger.LogDebug("Marked outbox entry {EventId} as sent", eventId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark outbox entry {EventId} as sent", eventId);
            throw;
        }
    }

    /// <summary>
    /// Marks an event as failed and schedules next retry with exponential backoff.
    /// Increments retry_count and updates last_error.
    /// </summary>
    public async Task MarkFailedAsync(string eventId, string errorMessage, DateTime nextRetryAt, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("EventId cannot be null or empty", nameof(eventId));
        }
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            throw new ArgumentException("ErrorMessage cannot be null or empty", nameof(errorMessage));
        }

        const string sql = """
            UPDATE sync_outbox
            SET status = 'failed',
                retry_count = retry_count + 1,
                last_error = @ErrorMessage,
                next_retry_at = @NextRetryAt
            WHERE event_id = @EventId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new
            {
                EventId = eventId,
                ErrorMessage = errorMessage,
                NextRetryAt = nextRetryAt.ToString("O")  // ISO8601 format
            }, cancellationToken: ct);

            int rowsAffected = await conn.ExecuteAsync(cmd);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Attempted to mark event {EventId} as failed, but it was not found",
                    eventId);
            }
            else
            {
                _logger.LogDebug("Marked outbox entry {EventId} as failed, next retry at {NextRetryAt}",
                    eventId, nextRetryAt);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark outbox entry {EventId} as failed", eventId);
            throw;
        }
    }
}
