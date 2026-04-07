using SharedKernel.Data;

namespace OptionsExecutionService.Migrations;

/// <summary>
/// Initial schema for options.db.
/// Creates all tables for the OptionsExecutionService:
/// - active_positions: Current open positions tracked by the service
/// - position_history: Historical record of all positions (for audit and analysis)
/// - execution_log: Detailed log of all order executions and fills
/// - strategy_state: Per-campaign strategy state (JSON)
/// </summary>
public sealed class OptionsInitial001 : IMigration
{
    public int Version => 1;
    public string Name => "OptionsInitial";

    public string UpSql => """
        -- ========================================
        -- Table: campaigns
        -- Purpose: Core campaign metadata and state tracking
        -- ========================================
        CREATE TABLE campaigns (
            campaign_id       TEXT PRIMARY KEY,       -- GUID
            strategy_name     TEXT NOT NULL,          -- Strategy that created this campaign
            status            TEXT NOT NULL,          -- CampaignState enum as string
            created_at        TEXT NOT NULL,          -- ISO8601 timestamp
            activated_at      TEXT,                   -- When positions opened (if Active)
            closed_at         TEXT,                   -- When campaign closed (if Closed)
            close_reason      TEXT,                   -- Reason for closing (if Closed)
            realized_pnl      REAL,                   -- Final P&L (if Closed)
            strategy_definition_json TEXT NOT NULL,   -- Full strategy configuration
            state_json        TEXT,                   -- Arbitrary strategy state (JSON)
            updated_at        TEXT NOT NULL DEFAULT (datetime('now'))
        );

        -- Index for querying campaigns by strategy
        CREATE INDEX idx_campaigns_strategy ON campaigns(strategy_name);

        -- Index for querying campaigns by status
        CREATE INDEX idx_campaigns_status ON campaigns(status);

        -- Index for time-based queries (recent campaigns)
        CREATE INDEX idx_campaigns_created ON campaigns(created_at DESC);

        -- ========================================
        -- Table: positions
        -- Purpose: Current open positions for all active campaigns
        -- ========================================
        CREATE TABLE positions (
            position_id       TEXT PRIMARY KEY,       -- GUID
            campaign_id       TEXT NOT NULL,          -- Which campaign owns this position
            symbol            TEXT NOT NULL,          -- Underlying symbol (e.g., "SPY")
            contract_symbol   TEXT NOT NULL,          -- Full option contract symbol (OCC format)
            contract_id       INTEGER NOT NULL,       -- IBKR contract ID
            strategy_name     TEXT NOT NULL,          -- Strategy that created this position
            quantity          INTEGER NOT NULL,       -- Number of contracts (positive = long, negative = short)
            entry_price       REAL NOT NULL,          -- Average entry price per contract
            current_price     REAL,                   -- Last known market price (updated by MarketDataWorker)
            unrealized_pnl    REAL,                   -- Current unrealized profit/loss
            stop_loss         REAL,                   -- Stop loss price (optional)
            take_profit       REAL,                   -- Take profit price (optional)
            opened_at         TEXT NOT NULL,          -- ISO8601 timestamp when position was opened
            updated_at        TEXT NOT NULL DEFAULT (datetime('now')),
            metadata_json     TEXT,                   -- Strategy-specific metadata (JSON)
            FOREIGN KEY (campaign_id) REFERENCES campaigns(campaign_id)
        );

        -- Index for querying positions by campaign
        CREATE INDEX idx_positions_campaign ON positions(campaign_id);

        -- Index for querying positions by symbol
        CREATE INDEX idx_positions_symbol ON positions(symbol);

        -- Index for querying positions by strategy
        CREATE INDEX idx_positions_strategy ON positions(strategy_name);

        -- ========================================
        -- Table: position_history
        -- Purpose: Immutable historical record of all positions (active and closed)
        -- ========================================
        CREATE TABLE position_history (
            history_id        TEXT PRIMARY KEY,       -- GUID
            position_id       TEXT NOT NULL,          -- Reference to positions.position_id
            campaign_id       TEXT NOT NULL,
            symbol            TEXT NOT NULL,
            contract_symbol   TEXT NOT NULL,
            strategy_name     TEXT NOT NULL,
            quantity          INTEGER NOT NULL,
            entry_price       REAL NOT NULL,
            exit_price        REAL,                   -- Average exit price (if closed)
            realized_pnl      REAL,                   -- Realized profit/loss (if closed)
            status            TEXT NOT NULL,          -- "open", "closed", "rolled"
            opened_at         TEXT NOT NULL,
            closed_at         TEXT,                   -- ISO8601 timestamp when position was closed
            created_at        TEXT NOT NULL DEFAULT (datetime('now')),
            metadata_json     TEXT
        );

        -- Index for querying history by position_id (audit trail)
        CREATE INDEX idx_history_position ON position_history(position_id);

        -- Index for querying history by campaign
        CREATE INDEX idx_history_campaign ON position_history(campaign_id);

        -- Index for time-based queries (recent history)
        CREATE INDEX idx_history_created ON position_history(created_at DESC);

        -- ========================================
        -- Table: execution_log
        -- Purpose: Detailed log of all order executions and fills from IBKR
        -- ========================================
        CREATE TABLE execution_log (
            execution_id      TEXT PRIMARY KEY,       -- IBKR execution ID
            order_id          TEXT NOT NULL,          -- IBKR order ID
            position_id       TEXT,                   -- Associated position_id (if applicable)
            campaign_id       TEXT NOT NULL,
            symbol            TEXT NOT NULL,
            contract_symbol   TEXT NOT NULL,
            side              TEXT NOT NULL,          -- "BUY" or "SELL"
            quantity          INTEGER NOT NULL,       -- Number of contracts filled
            fill_price        REAL NOT NULL,          -- Actual fill price
            commission        REAL NOT NULL,          -- Commission charged by IBKR
            executed_at       TEXT NOT NULL,          -- ISO8601 timestamp of execution
            created_at        TEXT NOT NULL DEFAULT (datetime('now'))
        );

        -- Index for querying executions by order_id
        CREATE INDEX idx_executions_order ON execution_log(order_id);

        -- Index for querying executions by position_id
        CREATE INDEX idx_executions_position ON execution_log(position_id) WHERE position_id IS NOT NULL;

        -- Index for querying executions by campaign
        CREATE INDEX idx_executions_campaign ON execution_log(campaign_id);

        -- Index for time-based queries (recent executions)
        CREATE INDEX idx_executions_executed ON execution_log(executed_at DESC);

        -- ========================================
        -- Table: strategy_state
        -- Purpose: Per-campaign strategy state persistence (arbitrary JSON)
        -- NOTE: This is kept for backward compatibility with CampaignRepository
        -- ========================================
        CREATE TABLE strategy_state (
            campaign_id       TEXT PRIMARY KEY,       -- One state record per campaign
            strategy_name     TEXT NOT NULL,          -- Which strategy owns this state
            state_json        TEXT NOT NULL,          -- Arbitrary JSON state (strategy-defined)
            updated_at        TEXT NOT NULL DEFAULT (datetime('now'))
        );

        -- No additional indexes needed (campaign_id is PRIMARY KEY)
        """;
}
