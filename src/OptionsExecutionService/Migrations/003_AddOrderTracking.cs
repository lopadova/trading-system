using SharedKernel.Data;

namespace OptionsExecutionService.Migrations;

/// <summary>
/// Migration 003: Add order_tracking table for order placement and lifecycle tracking.
/// This table provides an audit trail of all orders before, during, and after IBKR submission.
/// </summary>
public sealed class AddOrderTracking003 : IMigration
{
    public int Version => 3;
    public string Name => "AddOrderTracking";

    public string UpSql => """
        -- Order tracking table: audit trail for all orders
        CREATE TABLE IF NOT EXISTS order_tracking (
            order_id          TEXT    PRIMARY KEY,  -- Internal GUID
            ibkr_order_id     INTEGER,              -- IBKR order ID (null until submitted)
            campaign_id       TEXT    NOT NULL,
            position_id       TEXT,                 -- FK to active_positions (nullable)
            symbol            TEXT    NOT NULL,
            contract_symbol   TEXT    NOT NULL,
            side              TEXT    NOT NULL,     -- 'Buy' or 'Sell'
            order_type        TEXT    NOT NULL,     -- 'Market', 'Limit', 'Stop', 'StopLimit'
            quantity          INTEGER NOT NULL,
            limit_price       REAL,
            stop_price        REAL,
            time_in_force     TEXT    NOT NULL,     -- 'DAY' or 'GTC'
            status            TEXT    NOT NULL,     -- OrderStatus enum as string
            filled_quantity   INTEGER NOT NULL DEFAULT 0,
            avg_fill_price    REAL,
            strategy_name     TEXT    NOT NULL,
            metadata_json     TEXT,
            created_at        TEXT    NOT NULL,     -- ISO8601
            submitted_at      TEXT,                 -- ISO8601 (when sent to IBKR)
            completed_at      TEXT,                 -- ISO8601 (when filled/cancelled/rejected)
            updated_at        TEXT    NOT NULL      -- ISO8601
        );

        -- Index for looking up orders by IBKR ID (for status callbacks)
        CREATE INDEX IF NOT EXISTS idx_order_tracking_ibkr_id
            ON order_tracking(ibkr_order_id)
            WHERE ibkr_order_id IS NOT NULL;

        -- Index for campaign queries
        CREATE INDEX IF NOT EXISTS idx_order_tracking_campaign
            ON order_tracking(campaign_id);

        -- Index for position queries
        CREATE INDEX IF NOT EXISTS idx_order_tracking_position
            ON order_tracking(position_id)
            WHERE position_id IS NOT NULL;

        -- Index for failed orders (circuit breaker)
        CREATE INDEX IF NOT EXISTS idx_order_tracking_failed
            ON order_tracking(created_at DESC)
            WHERE status IN ('Failed', 'Rejected');

        -- Index for active orders monitoring
        CREATE INDEX IF NOT EXISTS idx_order_tracking_active
            ON order_tracking(status)
            WHERE status IN ('PendingSubmit', 'Submitted', 'Active', 'PartiallyFilled');

        -- Index for time-based queries
        CREATE INDEX IF NOT EXISTS idx_order_tracking_created
            ON order_tracking(created_at DESC);
        """;

    public string DownSql => """
        DROP INDEX IF EXISTS idx_order_tracking_created;
        DROP INDEX IF EXISTS idx_order_tracking_active;
        DROP INDEX IF EXISTS idx_order_tracking_failed;
        DROP INDEX IF EXISTS idx_order_tracking_position;
        DROP INDEX IF EXISTS idx_order_tracking_campaign;
        DROP INDEX IF EXISTS idx_order_tracking_ibkr_id;
        DROP TABLE IF EXISTS order_tracking;
        """;
}
