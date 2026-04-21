-- Migration 0009 — Order Audit Log (Phase 7.4)
--
-- Receiving table for order_audit events shipped from the OptionsExecutionService
-- (see src/OptionsExecutionService/Services/OutboxOrderAuditSink.cs and the
-- local mirror in src/OptionsExecutionService/Migrations/004_SafetyFlagsAndAudit.cs).
--
-- One row is written for EVERY order-placement attempt — success, broker
-- reject, gate reject, circuit-breaker reject — so the dashboard's audit view
-- is the single source of truth for "was an order placed / blocked / filled".
--
-- Idempotency: audit_id is a GUID generated upstream. Re-ingesting the same
-- row is a no-op on the UPSERT path.

CREATE TABLE IF NOT EXISTS order_audit_log (
  audit_id         TEXT PRIMARY KEY,
  order_id         TEXT,                               -- Nullable: pre-IBKR rejects have no order id yet
  ts               TEXT NOT NULL,                      -- ISO-8601 UTC timestamp of the decision
  actor            TEXT NOT NULL,                      -- "system" | "operator-override" | "campaign:<id>"
  strategy_id      TEXT,
  contract_symbol  TEXT NOT NULL,
  side             TEXT NOT NULL,                      -- "BUY" | "SELL"
  quantity         INTEGER NOT NULL,
  price            REAL,                               -- Nullable: unknown for pending/blocked rows
  semaphore_status TEXT NOT NULL,                      -- "green" | "orange" | "red" | "unknown"
  outcome          TEXT NOT NULL,                      -- "placed" | "filled" | "rejected_*" | "error"
  override_reason  TEXT,
  details_json     TEXT,
  created_at       TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Index for the default dashboard view (most-recent first).
CREATE INDEX idx_order_audit_ts ON order_audit_log (ts DESC);

-- Partial index for order lookup — most audit rows have a non-null order_id,
-- but blocked pre-IBKR rows do not; skip those to keep the index lean.
CREATE INDEX idx_order_audit_order_id ON order_audit_log (order_id) WHERE order_id IS NOT NULL;

-- Index for outcome-filtered queries ("show me all rejected_semaphore rows").
CREATE INDEX idx_order_audit_outcome ON order_audit_log (outcome, ts DESC);
