/**
 * Activity Feed API Route
 * Returns the most recent trading events for the Overview activity panel.
 *
 * Phase 7.2 — Replaces the hardcoded EVENTS array with a real D1 query that
 * UNIONs two sources:
 *
 *   1. alert_history: maps severity (critical/warning/info) to tone and icon
 *   2. execution_log:  maps order fills to "Order filled" events
 *
 * (Campaign state changes are NOT included yet — there is no campaigns audit
 * table in the 0006 schema; tracked separately as TODO(Phase 7.x).)
 *
 * Rows are ordered DESC by event timestamp and capped by `?limit=` (default
 * 8, clamped to 1..50).
 *
 * Fallback to pre-Phase-7.2 mock when both source tables are empty.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type {
  ActivityResponse,
  ActivityEvent,
  ActivityIcon,
  ActivityTone,
} from '../types/api'
import { authMiddleware } from '../middleware/auth'

// ---------------------------------------------------------------------------
// D1 row shape — produced by the UNION query below.
// ---------------------------------------------------------------------------
interface ActivityRow {
  source: 'alert' | 'execution'
  id: string
  ts: string
  a: string  // alert severity or execution side
  b: string  // alert type or order symbol
  c: string  // alert message or contract_symbol + qty
  d: number | null  // fill_price for execution; null for alerts
}

// ---------------------------------------------------------------------------
// Fallback mock events — ordered most-recent first
// ---------------------------------------------------------------------------
function fallbackEvents(): ActivityEvent[] {
  const now = Date.now()
  return [
    { id: 'a1', icon: 'check-circle-2', tone: 'green',  title: 'Order filled',           subtitle: 'SPY 450C × 3 @ $12.40',          timestamp: new Date(now -   2 * 60_000).toISOString() },
    { id: 'a2', icon: 'alert-triangle', tone: 'yellow', title: 'Delta breach warning',   subtitle: 'SPY Iron Condor · short call 0.37', timestamp: new Date(now -   6 * 60_000).toISOString() },
    { id: 'a3', icon: 'play',           tone: 'blue',   title: 'Campaign resumed',       subtitle: 'IWM Volatility Harvest',         timestamp: new Date(now -  24 * 60_000).toISOString() },
    { id: 'a4', icon: 'x-circle',       tone: 'red',    title: 'Order rejected',         subtitle: 'QQQ 395C — insufficient margin', timestamp: new Date(now -  38 * 60_000).toISOString() },
    { id: 'a5', icon: 'repeat',         tone: 'purple', title: 'Position rolled',        subtitle: 'SPY 450P/445P → next week',      timestamp: new Date(now -  72 * 60_000).toISOString() },
    { id: 'a6', icon: 'trending-up',    tone: 'green',  title: 'Take-profit hit',        subtitle: 'IWM 195C closed @ +$94.50',      timestamp: new Date(now - 124 * 60_000).toISOString() },
    { id: 'a7', icon: 'refresh-cw',     tone: 'blue',   title: 'IBKR reconnected',       subtitle: 'after 4.2s drop',                timestamp: new Date(now - 201 * 60_000).toISOString() },
    { id: 'a8', icon: 'file-text',      tone: 'muted',  title: 'Daily report exported',  subtitle: 'pnl_2026-04-19.csv',             timestamp: new Date(now -  24 * 60 * 60_000).toISOString() },
  ]
}

// ---------------------------------------------------------------------------
// Severity → (tone, icon) map for alert rows
// ---------------------------------------------------------------------------
function alertTone(severity: string): ActivityTone {
  if (severity === 'critical') return 'red'
  if (severity === 'warning') return 'yellow'
  return 'blue'
}

function alertIcon(severity: string): ActivityIcon {
  if (severity === 'critical') return 'x-circle'
  if (severity === 'warning') return 'alert-triangle'
  return 'refresh-cw'
}

function executionIcon(): ActivityIcon {
  return 'check-circle-2'
}

// ---------------------------------------------------------------------------
// Route handler
// ---------------------------------------------------------------------------
export const activity = new Hono<{ Bindings: Env }>()

// All activity routes require authentication
activity.use('*', authMiddleware)

activity.get('/recent', async (c) => {
  const rawLimit = Number(c.req.query('limit') ?? 8)
  // Clamp limit to [1, 50] — guard against NaN and absurd values
  const safeLimit = Number.isFinite(rawLimit) ? Math.max(1, Math.min(rawLimit, 50)) : 8

  try {
    // UNION two event sources into a single feed. Column names are unified
    // so we can ORDER BY ts DESC in one pass on the D1 side.
    //
    // alert_history:
    //   source='alert', id=alert_id, ts=created_at,
    //   a=severity, b=alert_type, c=message, d=NULL
    //
    // execution_log:
    //   source='execution', id=execution_id, ts=executed_at,
    //   a=side, b=symbol, c=contract_symbol || quantity, d=fill_price
    const sql =
      "SELECT * FROM (" +
      "  SELECT 'alert' AS source, alert_id AS id, created_at AS ts, " +
      "         severity AS a, alert_type AS b, message AS c, NULL AS d " +
      "  FROM alert_history " +
      "  UNION ALL " +
      "  SELECT 'execution' AS source, execution_id AS id, executed_at AS ts, " +
      "         side AS a, symbol AS b, contract_symbol AS c, fill_price AS d " +
      "  FROM execution_log " +
      ") ORDER BY ts DESC LIMIT ?"

    const res = await c.env.DB.prepare(sql).bind(safeLimit).all<ActivityRow>()
    const rows = res.results ?? []

    if (rows.length === 0) {
      c.header('X-Data-Source', 'fallback-mock')
      return c.json({ events: fallbackEvents().slice(0, safeLimit) })
    }

    const events: ActivityEvent[] = rows.map((r) => {
      if (r.source === 'execution') {
        const priceStr = r.d === null || r.d === undefined ? '' : ` @ $${r.d.toFixed(2)}`
        return {
          id: r.id,
          icon: executionIcon(),
          tone: 'green' as ActivityTone,
          title: `Order ${r.a === 'BUY' ? 'bought' : 'sold'}`,
          subtitle: `${r.c}${priceStr}`,
          timestamp: r.ts,
        }
      }
      // alert
      return {
        id: r.id,
        icon: alertIcon(r.a),
        tone: alertTone(r.a),
        title: r.b,
        subtitle: r.c,
        timestamp: r.ts,
      }
    })

    const payload: ActivityResponse = { events }
    return c.json(payload)
  } catch (error) {
    console.error('activity/recent query failed:', error)
    c.header('X-Data-Source', 'fallback-mock')
    return c.json({ events: fallbackEvents().slice(0, safeLimit) })
  }
})
