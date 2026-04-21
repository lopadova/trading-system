-- Migration 0008: Observability (Phase 7.3)
-- Purpose: centralize structured logs from .NET services + Worker + Dashboard into
-- D1, capture browser Core Web Vitals, and close the schema gap on service_heartbeats
-- (disk_total_gb + network_kbps). All tables use UPSERT-friendly keys so the
-- at-least-once outbox and browser retry paths are idempotent.
-- Date: 2026-04-21

-- ============================================================================
-- SERVICE LOGS (centralized structured logging)
-- ============================================================================
-- Every .NET Serilog sink + Worker console + Dashboard Sentry breadcrumb-style
-- log can land here. PK = (service, ts, sequence) so a batched POST (where
-- sequence is the index inside the batch envelope) is exact-duplicate safe:
-- replaying the same batch after a network blip does not double-insert.
--
-- properties is free-form JSON for structured fields (correlation_id, user_id,
-- request_id, etc.). exception_* columns are split so we can query/filter by
-- exception type without touching the JSON blob.
CREATE TABLE IF NOT EXISTS service_logs (
    service TEXT NOT NULL,
    ts TEXT NOT NULL,                            -- ISO 8601 timestamp (UTC)
    sequence INTEGER NOT NULL DEFAULT 0,         -- per-batch ordering / dedupe key
    level TEXT NOT NULL CHECK(level IN ('trace','debug','info','warn','error','critical')),
    message TEXT NOT NULL,
    properties TEXT,                             -- JSON-encoded structured fields
    source_context TEXT,                         -- Serilog SourceContext or equiv.
    exception_type TEXT,
    exception_message TEXT,
    exception_stack TEXT,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (service, ts, sequence)
);

CREATE INDEX IF NOT EXISTS idx_service_logs_service_ts
ON service_logs (service, ts DESC);

CREATE INDEX IF NOT EXISTS idx_service_logs_level_ts
ON service_logs (level, ts DESC);

-- ============================================================================
-- WEB VITALS (dashboard performance telemetry)
-- ============================================================================
-- Each browser session reports CLS / INP / LCP / FCP / TTFB. One metric per
-- (session, name, timestamp) triple. The rating column is the standard
-- 'good' | 'needs-improvement' | 'poor' bucket emitted by web-vitals itself —
-- storing it avoids recomputing the thresholds later.
CREATE TABLE IF NOT EXISTS web_vitals (
    session_id TEXT NOT NULL,
    name TEXT NOT NULL CHECK(name IN ('CLS','INP','LCP','FCP','TTFB')),
    value REAL NOT NULL,
    rating TEXT,                                 -- 'good' | 'needs-improvement' | 'poor'
    navigation_type TEXT,                        -- 'navigate' | 'reload' | 'back-forward' | ...
    metric_id TEXT,                              -- web-vitals unique id (for dedupe)
    timestamp TEXT NOT NULL,                     -- ISO 8601, client-provided
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (session_id, name, timestamp)
);

CREATE INDEX IF NOT EXISTS idx_web_vitals_name_ts
ON web_vitals (name, timestamp DESC);

-- ============================================================================
-- service_heartbeats: close Phase 7.2 schema gap
-- ============================================================================
-- The aggregate /api/system/metrics route expects disk_total_gb and
-- network_kbps in the heartbeats payload. SQLite requires one ALTER per
-- column. Columns are nullable so pre-migration rows stay valid.
ALTER TABLE service_heartbeats ADD COLUMN disk_total_gb REAL;
ALTER TABLE service_heartbeats ADD COLUMN network_kbps REAL;
