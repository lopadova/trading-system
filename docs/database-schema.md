# Trading System — Database Schema Reference

> Auto-generated from T-01 migrations
> Last updated: 2026-04-05

---

## Overview

The Trading System uses two SQLite databases with WAL mode enabled:

- **supervisor.db** — TradingSupervisorService (health monitoring, outbox, alerts, log tracking)
- **options.db** — OptionsExecutionService (positions, executions, strategy state)

Both databases use:
- WAL mode for concurrent reads/writes
- Foreign keys enabled
- 5-second busy timeout
- 32MB cache size

---

## supervisor.db Schema

### service_heartbeats

Health monitoring for all services.

| Column         | Type    | Constraints    | Description                                |
|----------------|---------|----------------|--------------------------------------------|
| service_name   | TEXT    | PRIMARY KEY    | Service identifier                         |
| hostname       | TEXT    | NOT NULL       | Machine hostname                           |
| last_seen_at   | TEXT    | NOT NULL       | ISO8601 timestamp of last heartbeat        |
| uptime_seconds | INTEGER | NOT NULL       | Service uptime in seconds                  |
| cpu_percent    | REAL    | NOT NULL       | CPU usage percentage (0-100)               |
| ram_percent    | REAL    | NOT NULL       | RAM usage percentage (0-100)               |
| disk_free_gb   | REAL    | NOT NULL       | Free disk space in GB                      |
| trading_mode   | TEXT    | NOT NULL       | "paper" or "live"                          |
| version        | TEXT    | NOT NULL       | Semantic version (e.g., "1.0.0")           |
| created_at     | TEXT    | NOT NULL       | ISO8601 creation timestamp                 |
| updated_at     | TEXT    | NOT NULL       | ISO8601 last update timestamp              |

**Indexes:**
- `idx_heartbeats_last_seen` on (last_seen_at) — for stale heartbeat detection

---

### sync_outbox

Transactional Outbox pattern for publishing events to Cloudflare D1.

| Column         | Type    | Constraints    | Description                                |
|----------------|---------|----------------|--------------------------------------------|
| event_id       | TEXT    | PRIMARY KEY    | GUID for deduplication                     |
| event_type     | TEXT    | NOT NULL       | Event type (e.g., "heartbeat_updated")     |
| payload_json   | TEXT    | NOT NULL       | JSON-serialized event data                 |
| dedupe_key     | TEXT    |                | Optional deduplication key                 |
| status         | TEXT    | NOT NULL       | "pending", "sent", "failed"                |
| retry_count    | INTEGER | NOT NULL       | Number of retry attempts                   |
| last_error     | TEXT    |                | Error message from last failed attempt     |
| next_retry_at  | TEXT    |                | ISO8601 timestamp for next retry           |
| created_at     | TEXT    | NOT NULL       | ISO8601 creation timestamp                 |
| sent_at        | TEXT    |                | ISO8601 timestamp when successfully sent   |

**Indexes:**
- `idx_outbox_status_retry` on (status, next_retry_at) — for fetching pending/failed events
- `idx_outbox_dedupe` on (dedupe_key) WHERE dedupe_key IS NOT NULL — for deduplication

---

### alert_history

Record of all alerts raised by the system.

| Column         | Type    | Constraints    | Description                                |
|----------------|---------|----------------|--------------------------------------------|
| alert_id       | TEXT    | PRIMARY KEY    | GUID                                       |
| alert_type     | TEXT    | NOT NULL       | Alert type (e.g., "HeartbeatMissing")      |
| severity       | TEXT    | NOT NULL       | "info", "warning", "critical"              |
| message        | TEXT    | NOT NULL       | Human-readable alert message               |
| details_json   | TEXT    |                | Additional structured data (JSON)          |
| source_service | TEXT    | NOT NULL       | Which service raised this alert            |
| created_at     | TEXT    | NOT NULL       | ISO8601 creation timestamp                 |
| resolved_at    | TEXT    |                | ISO8601 timestamp when alert was resolved  |
| resolved_by    | TEXT    |                | How it was resolved (e.g., "auto", "manual")|

**Indexes:**
- `idx_alerts_unresolved` on (resolved_at) WHERE resolved_at IS NULL — for querying active alerts
- `idx_alerts_type_severity` on (alert_type, severity) — for filtering alerts
- `idx_alerts_created` on (created_at DESC) — for time-based queries

---

### log_reader_state

Track file position for log file tailing (LogReaderWorker).

| Column         | Type    | Constraints    | Description                                |
|----------------|---------|----------------|--------------------------------------------|
| file_path      | TEXT    | PRIMARY KEY    | Absolute path to log file being tailed     |
| last_position  | INTEGER | NOT NULL       | Byte offset in file                        |
| last_size      | INTEGER | NOT NULL       | File size at last read (detect rotation)   |
| updated_at     | TEXT    | NOT NULL       | ISO8601 last update timestamp              |

**Indexes:** None (file_path is PRIMARY KEY)

---

## options.db Schema

### active_positions

Current open positions for all active campaigns.

| Column           | Type    | Constraints    | Description                                |
|------------------|---------|----------------|--------------------------------------------|
| position_id      | TEXT    | PRIMARY KEY    | GUID                                       |
| campaign_id      | TEXT    | NOT NULL       | Which campaign owns this position          |
| symbol           | TEXT    | NOT NULL       | Underlying symbol (e.g., "SPY")            |
| contract_symbol  | TEXT    | NOT NULL       | Full option contract symbol (OCC format)   |
| strategy_name    | TEXT    | NOT NULL       | Strategy that created this position        |
| quantity         | INTEGER | NOT NULL       | Number of contracts (+ = long, - = short)  |
| entry_price      | REAL    | NOT NULL       | Average entry price per contract           |
| current_price    | REAL    |                | Last known market price                    |
| unrealized_pnl   | REAL    |                | Current unrealized profit/loss             |
| stop_loss        | REAL    |                | Stop loss price (optional)                 |
| take_profit      | REAL    |                | Take profit price (optional)               |
| opened_at        | TEXT    | NOT NULL       | ISO8601 timestamp when position was opened |
| updated_at       | TEXT    | NOT NULL       | ISO8601 last update timestamp              |
| metadata_json    | TEXT    |                | Strategy-specific metadata (JSON)          |

**Indexes:**
- `idx_positions_campaign` on (campaign_id) — for querying positions by campaign
- `idx_positions_symbol` on (symbol) — for querying positions by symbol
- `idx_positions_strategy` on (strategy_name) — for querying positions by strategy

---

### position_history

Immutable historical record of all positions (active and closed).

| Column           | Type    | Constraints    | Description                                |
|------------------|---------|----------------|--------------------------------------------|
| history_id       | TEXT    | PRIMARY KEY    | GUID                                       |
| position_id      | TEXT    | NOT NULL       | Reference to active_positions.position_id  |
| campaign_id      | TEXT    | NOT NULL       | Campaign identifier                        |
| symbol           | TEXT    | NOT NULL       | Underlying symbol                          |
| contract_symbol  | TEXT    | NOT NULL       | Full option contract symbol                |
| strategy_name    | TEXT    | NOT NULL       | Strategy name                              |
| quantity         | INTEGER | NOT NULL       | Number of contracts                        |
| entry_price      | REAL    | NOT NULL       | Entry price per contract                   |
| exit_price       | REAL    |                | Average exit price (if closed)             |
| realized_pnl     | REAL    |                | Realized profit/loss (if closed)           |
| status           | TEXT    | NOT NULL       | "open", "closed", "rolled"                 |
| opened_at        | TEXT    | NOT NULL       | ISO8601 open timestamp                     |
| closed_at        | TEXT    |                | ISO8601 close timestamp                    |
| created_at       | TEXT    | NOT NULL       | ISO8601 creation timestamp                 |
| metadata_json    | TEXT    |                | Strategy-specific metadata (JSON)          |

**Indexes:**
- `idx_history_position` on (position_id) — for audit trail queries
- `idx_history_campaign` on (campaign_id) — for querying history by campaign
- `idx_history_created` on (created_at DESC) — for time-based queries

---

### execution_log

Detailed log of all order executions and fills from IBKR.

| Column           | Type    | Constraints    | Description                                |
|------------------|---------|----------------|--------------------------------------------|
| execution_id     | TEXT    | PRIMARY KEY    | IBKR execution ID                          |
| order_id         | TEXT    | NOT NULL       | IBKR order ID                              |
| position_id      | TEXT    |                | Associated position_id (if applicable)     |
| campaign_id      | TEXT    | NOT NULL       | Campaign identifier                        |
| symbol           | TEXT    | NOT NULL       | Underlying symbol                          |
| contract_symbol  | TEXT    | NOT NULL       | Full option contract symbol                |
| side             | TEXT    | NOT NULL       | "BUY" or "SELL"                            |
| quantity         | INTEGER | NOT NULL       | Number of contracts filled                 |
| fill_price       | REAL    | NOT NULL       | Actual fill price                          |
| commission       | REAL    | NOT NULL       | Commission charged by IBKR                 |
| executed_at      | TEXT    | NOT NULL       | ISO8601 execution timestamp                |
| created_at       | TEXT    | NOT NULL       | ISO8601 creation timestamp                 |

**Indexes:**
- `idx_executions_order` on (order_id) — for querying executions by order
- `idx_executions_position` on (position_id) WHERE position_id IS NOT NULL — for position audit
- `idx_executions_campaign` on (campaign_id) — for campaign-level reporting
- `idx_executions_executed` on (executed_at DESC) — for time-based queries

---

### strategy_state

Per-campaign strategy state persistence (arbitrary JSON).

| Column         | Type    | Constraints    | Description                                |
|----------------|---------|----------------|--------------------------------------------|
| campaign_id    | TEXT    | PRIMARY KEY    | One state record per campaign              |
| strategy_name  | TEXT    | NOT NULL       | Which strategy owns this state             |
| state_json     | TEXT    | NOT NULL       | Arbitrary JSON state (strategy-defined)    |
| updated_at     | TEXT    | NOT NULL       | ISO8601 last update timestamp              |

**Indexes:** None (campaign_id is PRIMARY KEY)

---

## Migration System

Both databases use the same migration tracking table:

### schema_migrations

| Column     | Type    | Constraints    | Description                        |
|------------|---------|----------------|------------------------------------|
| version    | INTEGER | PRIMARY KEY    | Migration version number           |
| name       | TEXT    | NOT NULL       | Migration name                     |
| applied_at | TEXT    | NOT NULL       | ISO8601 timestamp when applied     |

**Migrations are:**
- Transactional (rollback on failure)
- Idempotent (safe to run multiple times)
- Sorted by version before execution

---

## PRAGMA Settings

All connections (production and test) use:

```sql
PRAGMA journal_mode=WAL;       -- Write-Ahead Logging (production only, not in-memory tests)
PRAGMA synchronous=NORMAL;     -- Balanced durability vs performance
PRAGMA busy_timeout=5000;      -- 5-second wait for write locks
PRAGMA foreign_keys=ON;        -- Enforce referential integrity
PRAGMA cache_size=-32000;      -- 32MB cache
```

---

## Verification

To verify schema after running migrations:

```bash
./scripts/verify-schema.sh
```

This script checks:
- PRAGMA settings are correct
- All tables exist
- All indexes exist

---

*Schema version: 1 — Generated from migrations 001*
*Last updated: 2026-04-05 by T-01*
