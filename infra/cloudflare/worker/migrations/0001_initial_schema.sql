-- Trading System D1 Database Schema
-- Synced from SQLite schema (supervisor.db + options.db)
-- Date: 2026-04-05

-- ============================================================================
-- SUPERVISOR SCHEMA
-- ============================================================================

-- Service health monitoring
CREATE TABLE IF NOT EXISTS service_heartbeats (
    service_name TEXT PRIMARY KEY,
    hostname TEXT NOT NULL,
    last_seen_at TEXT NOT NULL,
    uptime_seconds INTEGER NOT NULL,
    cpu_percent REAL NOT NULL,
    ram_percent REAL NOT NULL,
    disk_free_gb REAL NOT NULL,
    trading_mode TEXT NOT NULL,
    version TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_heartbeats_last_seen
ON service_heartbeats(last_seen_at);

-- Transactional Outbox pattern for event publishing
CREATE TABLE IF NOT EXISTS sync_outbox (
    event_id TEXT PRIMARY KEY,
    event_type TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    dedupe_key TEXT,
    status TEXT NOT NULL CHECK(status IN ('pending', 'sent', 'failed')),
    retry_count INTEGER NOT NULL DEFAULT 0,
    last_error TEXT,
    next_retry_at TEXT,
    created_at TEXT NOT NULL,
    sent_at TEXT
);

CREATE INDEX IF NOT EXISTS idx_outbox_status_retry
ON sync_outbox(status, next_retry_at);

CREATE INDEX IF NOT EXISTS idx_outbox_dedupe
ON sync_outbox(dedupe_key) WHERE dedupe_key IS NOT NULL;

-- Alert history
CREATE TABLE IF NOT EXISTS alert_history (
    alert_id TEXT PRIMARY KEY,
    alert_type TEXT NOT NULL,
    severity TEXT NOT NULL CHECK(severity IN ('info', 'warning', 'critical')),
    message TEXT NOT NULL,
    details_json TEXT,
    source_service TEXT NOT NULL,
    created_at TEXT NOT NULL,
    resolved_at TEXT,
    resolved_by TEXT
);

CREATE INDEX IF NOT EXISTS idx_alerts_unresolved
ON alert_history(resolved_at) WHERE resolved_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_alerts_type_severity
ON alert_history(alert_type, severity);

CREATE INDEX IF NOT EXISTS idx_alerts_created
ON alert_history(created_at DESC);

-- Log file reader state
CREATE TABLE IF NOT EXISTS log_reader_state (
    file_path TEXT PRIMARY KEY,
    last_position INTEGER NOT NULL,
    last_size INTEGER NOT NULL,
    updated_at TEXT NOT NULL
);

-- ============================================================================
-- OPTIONS EXECUTION SCHEMA
-- ============================================================================

-- Active positions
CREATE TABLE IF NOT EXISTS active_positions (
    position_id TEXT PRIMARY KEY,
    campaign_id TEXT NOT NULL,
    symbol TEXT NOT NULL,
    contract_symbol TEXT NOT NULL,
    strategy_name TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    entry_price REAL NOT NULL,
    current_price REAL,
    unrealized_pnl REAL,
    stop_loss REAL,
    take_profit REAL,
    opened_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    metadata_json TEXT
);

CREATE INDEX IF NOT EXISTS idx_positions_campaign
ON active_positions(campaign_id);

CREATE INDEX IF NOT EXISTS idx_positions_symbol
ON active_positions(symbol);

CREATE INDEX IF NOT EXISTS idx_positions_strategy
ON active_positions(strategy_name);

-- Position history (immutable)
CREATE TABLE IF NOT EXISTS position_history (
    history_id TEXT PRIMARY KEY,
    position_id TEXT NOT NULL,
    campaign_id TEXT NOT NULL,
    symbol TEXT NOT NULL,
    contract_symbol TEXT NOT NULL,
    strategy_name TEXT NOT NULL,
    quantity INTEGER NOT NULL,
    entry_price REAL NOT NULL,
    exit_price REAL,
    realized_pnl REAL,
    status TEXT NOT NULL CHECK(status IN ('open', 'closed', 'rolled')),
    opened_at TEXT NOT NULL,
    closed_at TEXT,
    created_at TEXT NOT NULL,
    metadata_json TEXT
);

CREATE INDEX IF NOT EXISTS idx_history_position
ON position_history(position_id);

CREATE INDEX IF NOT EXISTS idx_history_campaign
ON position_history(campaign_id);

CREATE INDEX IF NOT EXISTS idx_history_created
ON position_history(created_at DESC);

-- Execution log
CREATE TABLE IF NOT EXISTS execution_log (
    execution_id TEXT PRIMARY KEY,
    order_id TEXT NOT NULL,
    position_id TEXT,
    campaign_id TEXT NOT NULL,
    symbol TEXT NOT NULL,
    contract_symbol TEXT NOT NULL,
    side TEXT NOT NULL CHECK(side IN ('BUY', 'SELL')),
    quantity INTEGER NOT NULL,
    fill_price REAL NOT NULL,
    commission REAL NOT NULL,
    executed_at TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_executions_order
ON execution_log(order_id);

CREATE INDEX IF NOT EXISTS idx_executions_position
ON execution_log(position_id) WHERE position_id IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_executions_campaign
ON execution_log(campaign_id);

CREATE INDEX IF NOT EXISTS idx_executions_executed
ON execution_log(executed_at DESC);

-- Strategy state persistence
CREATE TABLE IF NOT EXISTS strategy_state (
    campaign_id TEXT PRIMARY KEY,
    strategy_name TEXT NOT NULL,
    state_json TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
