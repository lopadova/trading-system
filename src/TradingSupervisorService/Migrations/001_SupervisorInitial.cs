using SharedKernel.Data;

namespace TradingSupervisorService.Migrations;

/// <summary>
/// Initial schema for supervisor.db.
/// Creates all tables for the TradingSupervisorService:
/// - service_heartbeats: Health monitoring from all services
/// - sync_outbox: Outbox pattern for cross-DB event publishing
/// - alert_history: Alert events raised by the system
/// - log_reader_state: File position tracking for log tailing
/// </summary>
public sealed class SupervisorInitial001 : IMigration
{
    public int Version => 1;
    public string Name => "SupervisorInitial";

    public string UpSql => """
        -- ========================================
        -- Table: service_heartbeats
        -- Purpose: Health monitoring for all services (TradingSupervisor, OptionsExecution, Dashboard)
        -- ========================================
        CREATE TABLE service_heartbeats (
            service_name    TEXT PRIMARY KEY,        -- "TradingSupervisor", "OptionsExecution", etc.
            hostname        TEXT NOT NULL,           -- Machine hostname
            last_seen_at    TEXT NOT NULL,           -- ISO8601 timestamp of last heartbeat
            uptime_seconds  INTEGER NOT NULL,        -- Service uptime in seconds
            cpu_percent     REAL NOT NULL,           -- CPU usage percentage (0-100)
            ram_percent     REAL NOT NULL,           -- RAM usage percentage (0-100)
            disk_free_gb    REAL NOT NULL,           -- Free disk space in GB
            trading_mode    TEXT NOT NULL,           -- "paper" or "live"
            version         TEXT NOT NULL,           -- Semantic version (e.g., "1.0.0")
            created_at      TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at      TEXT NOT NULL DEFAULT (datetime('now'))
        );

        -- Index for querying stale heartbeats (health check)
        CREATE INDEX idx_heartbeats_last_seen ON service_heartbeats(last_seen_at);

        -- ========================================
        -- Table: sync_outbox
        -- Purpose: Outbox pattern for publishing events from supervisor.db to Cloudflare D1
        -- ========================================
        CREATE TABLE sync_outbox (
            event_id       TEXT PRIMARY KEY,         -- GUID for deduplication
            event_type     TEXT NOT NULL,            -- "heartbeat_updated", "alert_raised", etc.
            payload_json   TEXT NOT NULL,            -- JSON-serialized event data
            dedupe_key     TEXT,                     -- Optional deduplication key (e.g., "heartbeat:service_name")
            status         TEXT NOT NULL DEFAULT 'pending',  -- "pending", "sent", "failed"
            retry_count    INTEGER NOT NULL DEFAULT 0,
            last_error     TEXT,                     -- Error message from last failed attempt
            next_retry_at  TEXT,                     -- ISO8601 timestamp for next retry (exponential backoff)
            created_at     TEXT NOT NULL DEFAULT (datetime('now')),
            sent_at        TEXT                      -- ISO8601 timestamp when successfully sent
        );

        -- Index for fetching pending/failed events ready for retry
        CREATE INDEX idx_outbox_status_retry ON sync_outbox(status, next_retry_at);

        -- Index for deduplication lookups
        CREATE INDEX idx_outbox_dedupe ON sync_outbox(dedupe_key) WHERE dedupe_key IS NOT NULL;

        -- ========================================
        -- Table: alert_history
        -- Purpose: Record of all alerts raised by the system
        -- ========================================
        CREATE TABLE alert_history (
            alert_id       TEXT PRIMARY KEY,         -- GUID
            alert_type     TEXT NOT NULL,            -- "HeartbeatMissing", "PositionRisk", "OrderFailed", etc.
            severity       TEXT NOT NULL,            -- "info", "warning", "critical"
            message        TEXT NOT NULL,            -- Human-readable alert message
            details_json   TEXT,                     -- Additional structured data (JSON)
            source_service TEXT NOT NULL,            -- Which service raised this alert
            created_at     TEXT NOT NULL DEFAULT (datetime('now')),
            resolved_at    TEXT,                     -- ISO8601 timestamp when alert was resolved
            resolved_by    TEXT                      -- How it was resolved (e.g., "auto", "manual")
        );

        -- Index for querying unresolved alerts
        CREATE INDEX idx_alerts_unresolved ON alert_history(resolved_at) WHERE resolved_at IS NULL;

        -- Index for querying alerts by type and severity
        CREATE INDEX idx_alerts_type_severity ON alert_history(alert_type, severity);

        -- Index for time-based queries (recent alerts)
        CREATE INDEX idx_alerts_created ON alert_history(created_at DESC);

        -- ========================================
        -- Table: log_reader_state
        -- Purpose: Track file position for log file tailing (LogReaderWorker)
        -- ========================================
        CREATE TABLE log_reader_state (
            file_path      TEXT PRIMARY KEY,         -- Absolute path to log file being tailed
            last_position  INTEGER NOT NULL DEFAULT 0,  -- Byte offset in file
            last_size      INTEGER NOT NULL DEFAULT 0,  -- File size at last read (detect rotation)
            updated_at     TEXT NOT NULL DEFAULT (datetime('now'))
        );

        -- No additional indexes needed (file_path is PRIMARY KEY)
        """;
}
