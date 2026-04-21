using SharedKernel.Data;

namespace TradingSupervisorService.Migrations;

/// <summary>
/// Migration 003 (Phase 7.1): Local SQLite cache tables for market-data ingestion workers.
///
/// These tables exist ONLY in supervisor.db as local caches / dedupe state for collectors.
/// The authoritative store for downstream aggregation is the Cloudflare D1 database
/// (populated via sync_outbox → Worker ingest route). See infra/cloudflare/worker/migrations/0007_market_data.sql.
///
/// Tables:
/// - benchmark_fetch_log: Tracks last successful Stooq/Yahoo benchmark pull per symbol.
///   Lets BenchmarkCollector skip duplicate daily fetches and detect stale gaps.
///
/// - position_greeks_cache: Rolling per-position Greeks snapshots captured from IBKR
///   tickOptionComputation callbacks (generic ticks 106 + 100). Primary purpose is
///   short-horizon queries by GreeksMonitorWorker (fresher than the options.db mirror)
///   and local auditability if the Outbox sync is temporarily down.
/// </summary>
public sealed class Migration003_MarketDataCache : IMigration
{
    public int Version => 3;
    public string Name => "MarketDataCache";

    public string UpSql => """
        -- ========================================
        -- Table: benchmark_fetch_log
        -- Purpose: Track last-seen date per external benchmark source (Stooq / Yahoo).
        --          Drives BenchmarkCollector's "new-rows-only" insertion into sync_outbox.
        -- ========================================
        CREATE TABLE IF NOT EXISTS benchmark_fetch_log (
            symbol              TEXT    PRIMARY KEY,   -- "SPX", "SWDA", etc.
            last_fetched_date   TEXT,                  -- ISO date (YYYY-MM-DD) of most-recent row queued
            last_success_ts     TEXT,                  -- ISO8601 UTC of most-recent successful fetch
            last_error          TEXT,                  -- Last failure reason (null if healthy)
            last_error_ts       TEXT,                  -- ISO8601 UTC of last failure (null if healthy)
            source              TEXT                   -- "stooq" | "yahoo" | null (which source last succeeded)
        );

        -- ========================================
        -- Table: position_greeks_cache
        -- Purpose: Local rolling cache of live Greeks ticks from IBKR.
        --          PK=(position_id, snapshot_ts) allows time-series per position.
        --          Consumed by GreeksMonitorWorker for fresher threshold checks.
        -- ========================================
        CREATE TABLE IF NOT EXISTS position_greeks_cache (
            position_id        TEXT    NOT NULL,
            snapshot_ts        TEXT    NOT NULL,     -- ISO8601 UTC
            delta              REAL,
            gamma              REAL,
            theta              REAL,
            vega               REAL,
            iv                 REAL,                 -- implied volatility
            underlying_price   REAL,
            PRIMARY KEY (position_id, snapshot_ts)
        );

        -- Query: latest snapshot per position (DESC index)
        CREATE INDEX IF NOT EXISTS idx_pg_cache_position_ts
        ON position_greeks_cache (position_id, snapshot_ts DESC);

        -- Query: time-windowed cleanup (drop snapshots older than N days)
        CREATE INDEX IF NOT EXISTS idx_pg_cache_ts
        ON position_greeks_cache (snapshot_ts);
        """;
}
