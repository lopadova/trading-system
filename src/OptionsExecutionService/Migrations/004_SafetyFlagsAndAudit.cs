using SharedKernel.Data;

namespace OptionsExecutionService.Migrations;

/// <summary>
/// Migration 004: Safety flags store + local order audit log mirror.
/// <para>
/// <b>safety_flags</b> — cross-cutting KV store persisted to disk so a trading
/// halt (e.g. drawdown exceeded) survives a service restart. The operator
/// must explicitly unpause; reboots are NOT a reset mechanism.
/// </para>
/// <para>
/// <b>order_audit_log_local</b> — local mirror of every audit row. This is
/// the authoritative record BEFORE the outbox sync to Cloudflare succeeds;
/// if the Worker is unreachable we still have a complete local record to
/// reconcile from later. Mirrors the Worker's <c>order_audit_log</c> schema
/// (see infra/cloudflare/worker/migrations/0009_order_audit_log.sql) so
/// data can be shipped 1:1 without translation.
/// </para>
/// </summary>
public sealed class SafetyFlagsAndAudit004 : IMigration
{
    public int Version => 4;

    public string Name => "SafetyFlagsAndAudit";

    public string UpSql => """
        -- Safety flags: durable KV store for trading-pause, semaphore-override-active, etc.
        CREATE TABLE IF NOT EXISTS safety_flags (
            key         TEXT PRIMARY KEY,
            value       TEXT NOT NULL,
            updated_at  TEXT NOT NULL        -- ISO8601 UTC
        );

        -- Audit log (local mirror). Ship to CF via outbox-style sink.
        CREATE TABLE IF NOT EXISTS order_audit_log_local (
            audit_id         TEXT PRIMARY KEY,   -- GUID
            order_id         TEXT,               -- null for pre-IBKR rejects
            ts               TEXT NOT NULL,      -- ISO8601 UTC
            actor            TEXT NOT NULL,      -- 'system' | 'operator-override' | 'campaign:<id>'
            strategy_id      TEXT,
            contract_symbol  TEXT NOT NULL,
            side             TEXT NOT NULL,      -- 'BUY' | 'SELL'
            quantity         INTEGER NOT NULL,
            price            REAL,
            semaphore_status TEXT NOT NULL,      -- 'green' | 'orange' | 'red' | 'unknown'
            outcome          TEXT NOT NULL,
            override_reason  TEXT,
            details_json     TEXT,
            shipped          INTEGER NOT NULL DEFAULT 0,   -- 0 = pending ship, 1 = shipped to CF
            created_at       TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE INDEX IF NOT EXISTS idx_oal_local_ts
            ON order_audit_log_local (ts DESC);

        CREATE INDEX IF NOT EXISTS idx_oal_local_outcome
            ON order_audit_log_local (outcome, ts DESC);

        CREATE INDEX IF NOT EXISTS idx_oal_local_pending_ship
            ON order_audit_log_local (created_at)
            WHERE shipped = 0;
        """;
}
