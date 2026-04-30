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
        -- Supports orderStatus, execDetails, and error callbacks via event_type discriminator
        CREATE TABLE IF NOT EXISTS order_events (
            event_id          INTEGER PRIMARY KEY AUTOINCREMENT,
            order_id          TEXT    NOT NULL,     -- Internal order ID (links to order_tracking)
            ibkr_order_id     INTEGER,              -- IBKR order ID (null until submitted)
            event_type        TEXT    NOT NULL,     -- 'status', 'execution', or 'error'

            -- orderStatus fields (event_type = 'status')
            status            TEXT,                 -- OrderStatus enum as string
            filled            INTEGER,              -- Filled quantity
            remaining         INTEGER,              -- Remaining quantity
            last_fill_price   REAL,                 -- Last fill price (from execDetails)
            avg_fill_price    REAL,                 -- Average fill price (from orderStatus)
            perm_id           INTEGER,              -- IBKR permanent order ID
            parent_id         INTEGER,              -- IBKR parent order ID (for child orders)
            last_trade_date   TEXT,                 -- Last trade date (from orderStatus)
            why_held          TEXT,                 -- Reason order held (from orderStatus)
            mkt_cap_price     REAL,                 -- Market cap price (from orderStatus)

            -- execDetails fields (event_type = 'execution')
            exec_id           TEXT,                 -- IBKR execution ID (unique per fill)
            exec_time         TEXT,                 -- Execution timestamp from IBKR
            side              TEXT,                 -- Execution side (BOT/SLD)
            shares            REAL,                 -- Number of shares executed
            price             REAL,                 -- Execution price
            exchange          TEXT,                 -- Exchange where executed
            symbol            TEXT,                 -- Contract symbol
            sec_type          TEXT,                 -- Contract security type (OPT, STK, etc.)

            -- error fields (event_type = 'error')
            error_code        INTEGER,              -- IBKR error code
            error_message     TEXT,                 -- Error message from IBKR
            error_time        INTEGER,              -- Error timestamp from IBKR (Unix timestamp)

            event_timestamp   TEXT    NOT NULL DEFAULT (datetime('now'))  -- ISO8601 UTC
        );

        -- Index for retrieving all events for a specific order (audit trail)
        CREATE INDEX IF NOT EXISTS idx_order_events_order_id
            ON order_events(order_id, event_id);

        -- Index for looking up events by IBKR ID (for callback correlation)
        CREATE INDEX IF NOT EXISTS idx_order_events_ibkr_order_id
            ON order_events(ibkr_order_id, event_id)
            WHERE ibkr_order_id IS NOT NULL;

        -- Index for time-based queries (e.g., "show all callbacks in last 24h")
        CREATE INDEX IF NOT EXISTS idx_order_events_timestamp
            ON order_events(event_timestamp DESC);

        -- Index for execution ID lookups (deduplication)
        CREATE INDEX IF NOT EXISTS idx_order_events_exec_id
            ON order_events(exec_id)
            WHERE exec_id IS NOT NULL;
        """;

    public string DownSql => """
        DROP INDEX IF EXISTS idx_order_events_exec_id;
        DROP INDEX IF EXISTS idx_order_events_timestamp;
        DROP INDEX IF EXISTS idx_order_events_ibkr_order_id;
        DROP INDEX IF EXISTS idx_order_events_order_id;
        DROP TABLE IF EXISTS order_events;
        """;
}
