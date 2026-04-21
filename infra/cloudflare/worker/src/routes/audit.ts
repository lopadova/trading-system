/**
 * Audit API Route (Phase 7.4)
 * Read access to the order_audit_log — the single source of truth for
 * "was an order placed/blocked". Used by the dashboard's audit view and by
 * ad-hoc operator queries via curl.
 *
 * Security: all routes require a valid X-Api-Key (same posture as /api/risk
 * and /api/performance). Audit data does not contain PII but it does contain
 * trading activity + strategy names, so it is NOT world-readable.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import { authMiddleware } from '../middleware/auth'

export const audit = new Hono<{ Bindings: Env }>()

audit.use('*', authMiddleware)

// ---------------------------------------------------------------------------
// Query parsing helpers
// ---------------------------------------------------------------------------

/** Default page size. Tuned for dashboard table views. */
const DEFAULT_LIMIT = 50
/** Upper bound — any query asking for more gets clamped here. Prevents OOM on
 *  a malformed or hostile client that passes limit=999999. */
const MAX_LIMIT = 500

const ALLOWED_OUTCOMES = new Set<string>([
  'placed',
  'filled',
  'rejected_semaphore',
  'rejected_pnl_pause',
  'rejected_max_size',
  'rejected_max_value',
  'rejected_max_risk',
  'rejected_min_balance',
  'rejected_breaker',
  'rejected_broker',
  'error',
])

/**
 * Parse the `limit` query parameter. Defaults to DEFAULT_LIMIT, clamps to
 * [1, MAX_LIMIT]. Returns null only for non-numeric input so the caller can
 * emit a 400.
 */
function parseLimit(raw: string | undefined): number | null {
  if (raw === undefined || raw === '') return DEFAULT_LIMIT
  const n = Number(raw)
  if (!Number.isFinite(n) || !Number.isInteger(n)) return null
  if (n < 1) return 1
  if (n > MAX_LIMIT) return MAX_LIMIT
  return n
}

/**
 * Parse the optional `from` (ISO date/datetime) filter. Returns:
 *   - undefined: no filter (param missing)
 *   - string: normalized ISO string to use in SQL bind
 *   - null: param present but invalid → caller emits 400
 */
function parseFrom(raw: string | undefined): string | undefined | null {
  if (raw === undefined || raw === '') return undefined
  // Accept both YYYY-MM-DD and full ISO-8601. If it's a bare date, treat as
  // UTC midnight so comparisons against stored ISO timestamps are consistent.
  const isDateOnly = /^\d{4}-\d{2}-\d{2}$/.test(raw)
  const candidate = isDateOnly ? `${raw}T00:00:00Z` : raw
  const date = new Date(candidate)
  if (Number.isNaN(date.getTime())) return null
  return date.toISOString()
}

/**
 * Parse the optional `outcome` filter. Returns:
 *   - undefined: no filter
 *   - string: accepted value
 *   - null: invalid → caller emits 400
 */
function parseOutcome(raw: string | undefined): string | undefined | null {
  if (raw === undefined || raw === '') return undefined
  if (!ALLOWED_OUTCOMES.has(raw)) return null
  return raw
}

// ---------------------------------------------------------------------------
// Row shape
// ---------------------------------------------------------------------------

interface AuditRow {
  audit_id: string
  order_id: string | null
  ts: string
  actor: string
  strategy_id: string | null
  contract_symbol: string
  side: string
  quantity: number
  price: number | null
  semaphore_status: string
  outcome: string
  override_reason: string | null
  details_json: string | null
  created_at: string
}

// ---------------------------------------------------------------------------
// Routes
// ---------------------------------------------------------------------------

/**
 * GET /api/audit/orders?limit=50&from=<ISO>&outcome=<str>
 *
 * Returns the most-recent order-audit rows, filtered by optional from/outcome
 * parameters. Response shape:
 *   { items: AuditRow[], count: number, limit: number, from?: string, outcome?: string }
 */
audit.get('/orders', async (c) => {
  const limit = parseLimit(c.req.query('limit'))
  if (limit === null) {
    return c.json(
      { error: 'invalid_limit', message: 'limit must be a positive integer' },
      400,
    )
  }

  const fromIso = parseFrom(c.req.query('from'))
  if (fromIso === null) {
    return c.json(
      { error: 'invalid_from', message: 'from must be ISO-8601 (date or datetime)' },
      400,
    )
  }

  const outcome = parseOutcome(c.req.query('outcome'))
  if (outcome === null) {
    return c.json(
      { error: 'invalid_outcome', message: `outcome must be one of: ${[...ALLOWED_OUTCOMES].join(', ')}` },
      400,
    )
  }

  try {
    // Build the WHERE clause dynamically. Each filter is optional; the SQL
    // string only references bound parameters so there's no injection risk.
    const whereParts: string[] = []
    const binds: unknown[] = []
    if (fromIso !== undefined) {
      whereParts.push('ts >= ?')
      binds.push(fromIso)
    }
    if (outcome !== undefined) {
      whereParts.push('outcome = ?')
      binds.push(outcome)
    }
    const whereSql = whereParts.length > 0 ? 'WHERE ' + whereParts.join(' AND ') : ''

    // ORDER BY ts DESC then audit_id DESC — audit_id is GUID so the secondary
    // sort is purely tiebreak stability, not a chronological signal.
    const sql =
      'SELECT audit_id, order_id, ts, actor, strategy_id, contract_symbol, ' +
      'side, quantity, price, semaphore_status, outcome, override_reason, ' +
      'details_json, created_at ' +
      `FROM order_audit_log ${whereSql} ` +
      'ORDER BY ts DESC, audit_id DESC ' +
      'LIMIT ?'
    binds.push(limit)

    const res = await c.env.DB.prepare(sql).bind(...binds).all<AuditRow>()
    const items = res.results ?? []

    return c.json({
      items,
      count: items.length,
      limit,
      ...(fromIso !== undefined ? { from: fromIso } : {}),
      ...(outcome !== undefined ? { outcome } : {}),
    })
  } catch (error) {
    console.error('audit/orders query failed:', error)
    return c.json(
      { error: 'audit_query_error', message: error instanceof Error ? error.message : 'unknown' },
      500,
    )
  }
})
