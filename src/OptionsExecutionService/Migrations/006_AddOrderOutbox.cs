using SharedKernel.Data;

namespace OptionsExecutionService.Migrations;

/// <summary>
/// Migration 006: Add order_outbox table for outbox pattern.
/// This table stores order intentions before broker execution,
/// enabling crash recovery and atomic broker+DB operations.
/// Phase 1: State Persistence & Idempotency
/// </summary>
public sealed class AddOrderOutbox006 : IMigration
{
    public int Version => 6;
    public string Name => "AddOrderOutbox";

    public string UpSql => """
        -- Order outbox table: stores order intentions before broker execution
        -- Implements outbox pattern for atomic broker+DB operations
        -- Enables crash recovery: unsent entries are retried on service restart
        CREATE TABLE IF NOT EXISTS order_outbox (
            outbox_id         INTEGER PRIMARY KEY AUTOINCREMENT,
            order_id          TEXT    NOT NULL,     -- Internal order ID (links to order_tracking)
            operation         TEXT    NOT NULL,     -- Operation type: PlaceOrder, CancelOrder, ModifyOrder
            payload           TEXT    NOT NULL,     -- JSON payload of operation (order params, cancel params, etc.)
            status            TEXT    NOT NULL DEFAULT 'pending',  -- 'pending' or 'sent'
            created_at        TEXT    NOT NULL DEFAULT (datetime('now')),  -- ISO8601 UTC timestamp
            sent_at           TEXT                   -- Timestamp when marked sent (null until sent)
        );

        -- Index for GetPendingAsync: retrieves unsent entries ordered by creation time
        -- (allows FIFO processing of pending operations)
        CREATE INDEX IF NOT EXISTS idx_order_outbox_status
            ON order_outbox(status, created_at)
            WHERE status = 'pending';

        -- Index for order-specific queries (e.g., "find all pending ops for order-123")
        CREATE INDEX IF NOT EXISTS idx_order_outbox_order_id
            ON order_outbox(order_id, created_at DESC);

        -- Index for cleanup queries (e.g., DeleteOldAsync purges sent entries older than 30 days)
        CREATE INDEX IF NOT EXISTS idx_order_outbox_cleanup
            ON order_outbox(status, sent_at)
            WHERE status = 'sent' AND sent_at IS NOT NULL;
        """;

    public string DownSql => """
        DROP INDEX IF EXISTS idx_order_outbox_cleanup;
        DROP INDEX IF EXISTS idx_order_outbox_order_id;
        DROP INDEX IF EXISTS idx_order_outbox_status;
        DROP TABLE IF EXISTS order_outbox;
        """;
}
