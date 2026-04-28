using SharedKernel.Data;

namespace OptionsExecutionService.Migrations;

/// <summary>
/// Migration 005: Add order_events table for IBKR callback persistence.
/// This table provides an immutable audit trail of all order status callbacks,
/// enabling crash recovery and preventing duplicate orders.
/// Phase 1: State Persistence & Idempotency
/// </summary>
public sealed class AddOrderEvents005 : IMigration
{
    public int Version => 5;
    public string Name => "AddOrderEvents";

    public string UpSql => """
        -- Order events table: immutable audit trail of IBKR callbacks
        -- NOTE: event_id uses AUTOINCREMENT to ensure monotonic ordering
        -- (prevents timestamp-based flakes on fast sequential inserts)
        CREATE TABLE IF NOT EXISTS order_events (
            event_id          INTEGER PRIMARY KEY AUTOINCREMENT,
            order_id          TEXT    NOT NULL,
            ibkr_order_id     INTEGER,
            status            TEXT    NOT NULL,
            filled            INTEGER NOT NULL,
            remaining         INTEGER NOT NULL,
            last_fill_price   REAL,
            avg_fill_price    REAL,
            perm_id           INTEGER,
            parent_id         INTEGER,
            last_trade_date   TEXT,
            why_held          TEXT,
            mkt_cap_price     REAL,
            event_timestamp   TEXT    NOT NULL DEFAULT (datetime('now', 'utc'))
        );

        -- Index for retrieving all events for a specific order (audit trail)
        CREATE INDEX IF NOT EXISTS idx_order_events_order_id
            ON order_events(order_id, event_id);

        -- Index for time-based queries (e.g., "show all callbacks in last 24h")
        CREATE INDEX IF NOT EXISTS idx_order_events_timestamp
            ON order_events(event_timestamp DESC);
        """;

    public string DownSql => """
        DROP INDEX IF EXISTS idx_order_events_timestamp;
        DROP INDEX IF EXISTS idx_order_events_order_id;
        DROP TABLE IF EXISTS order_events;
        """;
}
