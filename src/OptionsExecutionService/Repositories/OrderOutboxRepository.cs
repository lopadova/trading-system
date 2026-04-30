using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;

namespace OptionsExecutionService.Repositories;

/// <summary>
/// Repository for managing outbox entries for atomic broker+DB operations.
/// Implements outbox pattern for crash recovery and idempotency.
/// Phase 1: State Persistence & Idempotency
/// </summary>
public sealed class OrderOutboxRepository : IOrderOutboxRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<OrderOutboxRepository> _logger;

    public OrderOutboxRepository(IDbConnectionFactory db, ILogger<OrderOutboxRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrderOutboxEntry> InsertAsync(OrderOutboxEntry entry, CancellationToken ct = default)
    {
        // Validate inputs (negative-first pattern)
        if (string.IsNullOrWhiteSpace(entry.OrderId))
        {
            throw new ArgumentException("OrderId cannot be empty", nameof(entry));
        }
        if (string.IsNullOrWhiteSpace(entry.Operation))
        {
            throw new ArgumentException("Operation cannot be empty", nameof(entry));
        }
        if (string.IsNullOrWhiteSpace(entry.Payload))
        {
            throw new ArgumentException("Payload cannot be empty", nameof(entry));
        }

        // SQL: Insert outbox entry with auto-generated outbox_id and created_at timestamp
        // NOTE: RETURNING clause retrieves the auto-generated ID and timestamp in a single round-trip
        const string sql = """
            INSERT INTO order_outbox (order_id, operation, payload, status)
            VALUES (@OrderId, @Operation, @Payload, @Status)
            RETURNING outbox_id AS OutboxId, created_at AS CreatedAt
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new
            {
                OrderId = entry.OrderId,
                Operation = entry.Operation,
                Payload = entry.Payload,
                Status = entry.Status
            }, cancellationToken: ct);

            // Execute and retrieve the auto-generated ID and timestamp
            var result = await conn.QuerySingleAsync<(long OutboxId, string CreatedAt)>(cmd);

            _logger.LogDebug(
                "Inserted outbox entry: OutboxId={OutboxId} OrderId={OrderId} Operation={Operation}",
                result.OutboxId, entry.OrderId, entry.Operation);

            // Return entry with populated OutboxId and CreatedAt
            return entry with
            {
                OutboxId = result.OutboxId,
                CreatedAt = result.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to insert outbox entry: OrderId={OrderId} Operation={Operation}",
                entry.OrderId, entry.Operation);
            throw;
        }
    }

    public async Task<IReadOnlyList<OrderOutboxEntry>> GetPendingAsync(int limit, CancellationToken ct = default)
    {
        // Validate input
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than 0");
        }

        // SQL: Get pending entries ordered by creation time (FIFO processing)
        // NOTE: Index idx_order_outbox_status on (status, created_at) makes this efficient
        const string sql = """
            SELECT
                outbox_id AS OutboxId,
                order_id AS OrderId,
                operation AS Operation,
                payload AS Payload,
                status AS Status,
                created_at AS CreatedAt,
                sent_at AS SentAt
            FROM order_outbox
            WHERE status = 'pending'
            ORDER BY created_at
            LIMIT @Limit
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { Limit = limit }, cancellationToken: ct);

            IEnumerable<OrderOutboxEntry> results = await conn.QueryAsync<OrderOutboxEntry>(cmd);
            List<OrderOutboxEntry> list = results.ToList();

            _logger.LogDebug("Retrieved {Count} pending outbox entries", list.Count);

            return list.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get pending outbox entries");
            throw;
        }
    }

    public async Task MarkSentAsync(long outboxId, CancellationToken ct = default)
    {
        // Validate input
        if (outboxId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outboxId), "OutboxId must be greater than 0");
        }

        // SQL: Update status to 'sent' and set sent_at timestamp
        const string sql = """
            UPDATE order_outbox
            SET status = 'sent', sent_at = datetime('now')
            WHERE outbox_id = @OutboxId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { OutboxId = outboxId }, cancellationToken: ct);

            int rowsAffected = await conn.ExecuteAsync(cmd);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("No outbox entry found with OutboxId={OutboxId} to mark as sent", outboxId);
            }
            else
            {
                _logger.LogDebug("Marked outbox entry as sent: OutboxId={OutboxId}", outboxId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark outbox entry as sent: OutboxId={OutboxId}", outboxId);
            throw;
        }
    }

    public async Task MarkFailedAsync(long outboxId, CancellationToken ct = default)
    {
        // Validate input
        if (outboxId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outboxId), "OutboxId must be greater than 0");
        }

        // SQL: Update status to 'failed' for manual review/retry
        // NOTE: Do not set sent_at for failed entries (used for cleanup of successful sends only)
        const string sql = """
            UPDATE order_outbox
            SET status = 'failed'
            WHERE outbox_id = @OutboxId
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            CommandDefinition cmd = new(sql, new { OutboxId = outboxId }, cancellationToken: ct);

            int rowsAffected = await conn.ExecuteAsync(cmd);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("No outbox entry found with OutboxId={OutboxId} to mark as failed", outboxId);
            }
            else
            {
                _logger.LogWarning("Marked outbox entry as failed: OutboxId={OutboxId} (requires manual review)", outboxId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark outbox entry as failed: OutboxId={OutboxId}", outboxId);
            throw;
        }
    }
}
