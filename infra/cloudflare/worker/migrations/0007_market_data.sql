-- Migration 0007: Market data ingestion tables (Phase 7.1)
-- Purpose: capture live market + portfolio series that feed the dashboard
-- aggregate endpoints. All tables use UPSERT-friendly primary keys so the
-- outbox retry path (idempotent) can safely replay the same event twice.
-- Date: 2026-04-20

-- ============================================================================
-- ACCOUNT EQUITY (daily snapshot)
-- ============================================================================
-- One row per calendar day. Used by performance.ts, drawdowns.ts,
-- monthly-returns.ts. margin_used_pct is pre-computed for risk widgets.
CREATE TABLE IF NOT EXISTS account_equity_daily (
    date TEXT PRIMARY KEY,                       -- ISO date (YYYY-MM-DD)
    account_value REAL NOT NULL,                 -- total NAV
    cash REAL NOT NULL,
    buying_power REAL NOT NULL,
    margin_used REAL NOT NULL,                   -- absolute margin consumption
    margin_used_pct REAL NOT NULL,               -- margin_used / account_value * 100
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_equity_date
ON account_equity_daily (date DESC);

-- ============================================================================
-- MARKET QUOTES (daily OHLCV per symbol)
-- ============================================================================
-- Generic per-symbol daily bar table. Drives SPX / VIX / VIX3M / benchmark
-- history for the semaphore + performance cards.
CREATE TABLE IF NOT EXISTS market_quotes_daily (
    symbol TEXT NOT NULL,
    date TEXT NOT NULL,                          -- ISO date (YYYY-MM-DD)
    open REAL,
    high REAL,
    low REAL,
    close REAL NOT NULL,
    volume INTEGER,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (symbol, date)
);

CREATE INDEX IF NOT EXISTS idx_market_quotes_symbol_date
ON market_quotes_daily (symbol, date DESC);

-- ============================================================================
-- VIX TERM STRUCTURE (denormalized curve per day)
-- ============================================================================
-- One row per day capturing the VIX curve. Denormalized for fast semaphore
-- computation (IVTS = vix / vix3m). Each leg is also mirrored into
-- market_quotes_daily by the ingest handler so chart endpoints have a single
-- source of truth.
CREATE TABLE IF NOT EXISTS vix_term_structure (
    date TEXT PRIMARY KEY,
    vix REAL,
    vix1d REAL,
    vix3m REAL,
    vix6m REAL,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_vix_date
ON vix_term_structure (date DESC);

-- ============================================================================
-- BENCHMARK SERIES (pre-normalized for chart overlay)
-- ============================================================================
-- Benchmark closes already re-based to 100 from a chosen start date, stored
-- in close_normalized so the Worker doesn't recompute on every request.
-- Used by performance /series to overlay S&P 500 + SWDA curves.
CREATE TABLE IF NOT EXISTS benchmark_series (
    symbol TEXT NOT NULL,
    date TEXT NOT NULL,
    close REAL NOT NULL,
    close_normalized REAL,                       -- base-100 from normalize_base_date
    PRIMARY KEY (symbol, date)
);

CREATE INDEX IF NOT EXISTS idx_benchmark_symbol_date
ON benchmark_series (symbol, date DESC);

-- ============================================================================
-- POSITION GREEKS (time-series per position)
-- ============================================================================
-- Rolling snapshots of option Greeks + IV per open position. Drives the
-- portfolio delta/theta/vega aggregation in risk.ts /metrics.
CREATE TABLE IF NOT EXISTS position_greeks (
    position_id TEXT NOT NULL,
    snapshot_ts TEXT NOT NULL,                   -- ISO 8601 timestamp
    delta REAL,
    gamma REAL,
    theta REAL,
    vega REAL,
    iv REAL,
    underlying_price REAL,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (position_id, snapshot_ts)
);

CREATE INDEX IF NOT EXISTS idx_greeks_position
ON position_greeks (position_id, snapshot_ts DESC);
