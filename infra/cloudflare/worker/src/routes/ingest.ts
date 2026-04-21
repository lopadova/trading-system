/**
 * Ingest API Route
 * POST endpoint for receiving events from TradingSupervisorService outbox.
 *
 * Phase 7.1 — extends the original 3 event types (heartbeat / alert / position)
 * with 5 market-data event types that feed the dashboard aggregate endpoints.
 *
 * Idempotency: every new handler uses INSERT OR REPLACE against a table with
 * a natural primary key (date, (symbol,date), (position_id, snapshot_ts)) so
 * replaying the same Outbox entry after a retry never creates duplicates.
 */

import { Hono, type Context } from 'hono'
import { z } from 'zod'
import type { Env } from '../types/env'
import { authMiddleware } from '../middleware/auth'
import { recordMetric } from '../lib/metrics'

type IngestContext = Context<{ Bindings: Env }>

const ingest = new Hono<{ Bindings: Env }>()

// All routes require authentication
ingest.use('*', authMiddleware)

// ============================================================================
// Zod schemas — strict validation for each event type
// ============================================================================

// ISO date format YYYY-MM-DD
const isoDate = z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Expected ISO date YYYY-MM-DD')

// Optional nullable number helper (payloads may omit or explicitly null)
const optionalNumber = z.number().nullable().optional()

export const AccountEquityPayloadSchema = z.object({
  date: isoDate,
  account_value: z.number(),
  cash: z.number(),
  buying_power: z.number(),
  margin_used: z.number(),
  margin_used_pct: z.number()
})

export const MarketQuotePayloadSchema = z.object({
  symbol: z.string().min(1).max(32),
  date: isoDate,
  open: optionalNumber,
  high: optionalNumber,
  low: optionalNumber,
  close: z.number(),
  volume: z.number().int().nullable().optional()
})

export const VixSnapshotPayloadSchema = z.object({
  date: isoDate,
  vix: optionalNumber,
  vix1d: optionalNumber,
  vix3m: optionalNumber,
  vix6m: optionalNumber
})

export const BenchmarkClosePayloadSchema = z.object({
  symbol: z.string().min(1).max(32),
  date: isoDate,
  close: z.number(),
  close_normalized: optionalNumber
})

export const PositionGreeksPayloadSchema = z.object({
  position_id: z.string().min(1),
  snapshot_ts: z.string().min(1),  // ISO 8601 timestamp (not just date)
  delta: optionalNumber,
  gamma: optionalNumber,
  theta: optionalNumber,
  vega: optionalNumber,
  iv: optionalNumber,
  underlying_price: optionalNumber
})

// Phase 7.3 — browser Web Vitals emitted by the dashboard web-vitals reporter.
// Kept lenient: clients can send the raw payload produced by the web-vitals
// library (name, value, id, navigationType, rating) without preprocessing.
export const WebVitalsPayloadSchema = z.object({
  session_id: z.string().min(1),
  name: z.enum(['CLS', 'INP', 'LCP', 'FCP', 'TTFB']),
  value: z.number(),
  rating: z.enum(['good', 'needs-improvement', 'poor']).optional(),
  navigationType: z.string().optional(),
  id: z.string().optional(),
  timestamp: z.string().optional()  // ISO 8601; auto-filled if absent
})

// Phase 7.4 — Order audit log entries. One row per order-placement attempt
// (placed / filled / rejected_* / error). Matches the OrderAuditEntry contract
// in src/SharedKernel/Safety/OrderAuditEntry.cs. audit_id is the PK for
// idempotent replays.
export const ALLOWED_AUDIT_OUTCOMES = [
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
  'error'
] as const

export const OrderAuditPayloadSchema = z.object({
  audit_id: z.string().min(1).max(128),
  order_id: z.string().min(1).max(128).nullable().optional(),
  ts: z.string().min(1),                                // ISO-8601 timestamp
  actor: z.string().min(1).max(128),
  strategy_id: z.string().max(128).nullable().optional(),
  contract_symbol: z.string().min(1).max(64),
  side: z.enum(['BUY', 'SELL']),
  quantity: z.number().int().positive(),
  price: z.number().nullable().optional(),
  semaphore_status: z.enum(['green', 'orange', 'red', 'unknown']),
  outcome: z.enum(ALLOWED_AUDIT_OUTCOMES),
  override_reason: z.string().max(512).nullable().optional(),
  details_json: z.string().max(8192).nullable().optional()
})

/**
 * POST /api/v1/ingest
 * Receives outbox events from TradingSupervisorService and inserts into D1.
 *
 * Expected envelope:
 * {
 *   event_id: string,
 *   event_type: 'heartbeat' | 'alert' | 'position'
 *             | 'account_equity' | 'market_quote' | 'vix_snapshot'
 *             | 'benchmark_close' | 'position_greeks',
 *   payload: { ... event-specific data }
 * }
 */
ingest.post('/', async (c) => {
  try {
    const body = await c.req.json()

    // Validate required envelope fields
    if (!body.event_id || !body.event_type || !body.payload) {
      return c.json(
        {
          error: 'invalid_request',
          message: 'Missing required fields: event_id, event_type, payload'
        },
        400
      )
    }

    const { event_id, event_type, payload } = body

    // Route to appropriate handler based on event_type
    switch (event_type) {
      // --- existing event types (preserved unchanged) ---
      case 'heartbeat':
        await handleHeartbeat(c.env.DB, event_id, payload)
        break

      case 'alert':
        await handleAlert(c.env.DB, event_id, payload)
        break

      case 'position':
        await handlePosition(c.env.DB, event_id, payload)
        break

      // --- Phase 7.1 market-data event types ---
      case 'account_equity': {
        const parsed = AccountEquityPayloadSchema.safeParse(payload)
        if (!parsed.success) return validationError(c, event_type, parsed.error)
        await handleAccountEquity(c.env.DB, event_id, parsed.data)
        break
      }

      case 'market_quote': {
        const parsed = MarketQuotePayloadSchema.safeParse(payload)
        if (!parsed.success) return validationError(c, event_type, parsed.error)
        await handleMarketQuote(c.env.DB, event_id, parsed.data)
        break
      }

      case 'vix_snapshot': {
        const parsed = VixSnapshotPayloadSchema.safeParse(payload)
        if (!parsed.success) return validationError(c, event_type, parsed.error)
        await handleVixSnapshot(c.env.DB, event_id, parsed.data)
        break
      }

      case 'benchmark_close': {
        const parsed = BenchmarkClosePayloadSchema.safeParse(payload)
        if (!parsed.success) return validationError(c, event_type, parsed.error)
        await handleBenchmarkClose(c.env.DB, event_id, parsed.data)
        break
      }

      case 'position_greeks': {
        const parsed = PositionGreeksPayloadSchema.safeParse(payload)
        if (!parsed.success) return validationError(c, event_type, parsed.error)
        await handlePositionGreeks(c.env.DB, event_id, parsed.data)
        break
      }

      case 'web_vitals': {
        const parsed = WebVitalsPayloadSchema.safeParse(payload)
        if (!parsed.success) return validationError(c, event_type, parsed.error)
        await handleWebVitals(c.env.DB, event_id, parsed.data)
        break
      }

      case 'order_audit': {
        const parsed = OrderAuditPayloadSchema.safeParse(payload)
        if (!parsed.success) return validationError(c, event_type, parsed.error)
        await handleOrderAudit(c.env.DB, event_id, parsed.data)
        break
      }

      default:
        recordMetric(c.env, 'ingest.event_type', {
          type: String(event_type),
          status: 'rejected_unknown_type'
        })
        return c.json(
          {
            error: 'invalid_event_type',
            message: `Unknown event_type: ${event_type}`
          },
          400
        )
    }

    // Success metric — tagged by event type so Cloudflare Analytics can split.
    recordMetric(c.env, 'ingest.event_type', {
      type: String(event_type),
      status: 'accepted'
    })

    return c.json({
      success: true,
      event_id,
      event_type,
      message: 'Event ingested successfully'
    })

  } catch (error) {
    console.error('Failed to ingest event:', error)
    recordMetric(c.env, 'd1.error', { route: 'ingest' })
    return c.json(
      {
        error: 'ingest_error',
        message: error instanceof Error ? error.message : 'Unknown error'
      },
      500
    )
  }
})

// ============================================================================
// Helpers
// ============================================================================

/**
 * Build a consistent 400 response from a Zod validation failure.
 * Never leak full stack; report only the flattened field errors.
 */
function validationError(
  c: IngestContext,
  eventType: string,
  error: z.ZodError
) {
  const issues = error.issues.map(i => ({
    path: i.path.join('.'),
    message: i.message
  }))
  recordMetric(c.env, 'ingest.event_type', {
    type: eventType,
    status: 'rejected_validation'
  })
  return c.json(
    {
      error: 'invalid_payload',
      message: `Payload failed validation for event_type=${eventType}`,
      issues
    },
    400
  )
}

// ============================================================================
// Existing handlers (heartbeat / alert / position) — preserved unchanged
// ============================================================================

/**
 * Handle heartbeat event - upsert into service_heartbeats table
 */
async function handleHeartbeat(db: D1Database, eventId: string, payload: any) {
  const sql = `
    INSERT INTO service_heartbeats
      (service_name, hostname, last_seen_at, uptime_seconds,
       cpu_percent, ram_percent, disk_free_gb, trading_mode,
       version, created_at, updated_at)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ON CONFLICT(service_name) DO UPDATE SET
      hostname = excluded.hostname,
      last_seen_at = excluded.last_seen_at,
      uptime_seconds = excluded.uptime_seconds,
      cpu_percent = excluded.cpu_percent,
      ram_percent = excluded.ram_percent,
      disk_free_gb = excluded.disk_free_gb,
      trading_mode = excluded.trading_mode,
      version = excluded.version,
      updated_at = CURRENT_TIMESTAMP
  `

  await db.prepare(sql)
    .bind(
      payload.serviceName,
      payload.hostname,
      payload.lastSeenAt,
      payload.uptimeSeconds,
      payload.cpuPercent,
      payload.ramPercent,
      payload.diskFreeGb,
      payload.tradingMode,
      payload.version,
      payload.createdAt,
      payload.updatedAt
    )
    .run()

  console.log(`[INGEST] heartbeat ok: ${payload.serviceName} (event ${eventId})`)
}

/**
 * Handle alert event - insert into alert_history table
 */
async function handleAlert(db: D1Database, eventId: string, payload: any) {
  const sql = `
    INSERT INTO alert_history
      (alert_id, alert_type, severity, message, details_json,
       source_service, created_at, resolved_at, resolved_by)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
  `

  await db.prepare(sql)
    .bind(
      payload.alertId,
      payload.alertType,
      payload.severity,
      payload.message,
      payload.detailsJson,
      payload.sourceService,
      payload.createdAt,
      payload.resolvedAt,
      payload.resolvedBy
    )
    .run()

  console.log(`[INGEST] alert ok: ${payload.alertType} (event ${eventId})`)
}

/**
 * Handle position event - insert into positions_history table
 */
async function handlePosition(db: D1Database, eventId: string, payload: any) {
  const sql = `
    INSERT INTO positions_history
      (position_id, contract_symbol, quantity, cost_basis_avg,
       current_price, unrealized_pnl, realized_pnl, opened_at,
       closed_at, status, created_at)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `

  await db.prepare(sql)
    .bind(
      payload.positionId,
      payload.contractSymbol,
      payload.quantity,
      payload.costBasisAvg,
      payload.currentPrice,
      payload.unrealizedPnl,
      payload.realizedPnl,
      payload.openedAt,
      payload.closedAt,
      payload.status,
      payload.createdAt
    )
    .run()

  console.log(`[INGEST] position ok: ${payload.contractSymbol} (event ${eventId})`)
}

// ============================================================================
// Phase 7.1 — Market-data handlers (UPSERT idempotent)
// ============================================================================

/**
 * account_equity — one row per calendar day. Re-ingesting same date replaces
 * the previous row (latest snapshot wins).
 */
async function handleAccountEquity(
  db: D1Database,
  eventId: string,
  payload: z.infer<typeof AccountEquityPayloadSchema>
) {
  const sql = `
    INSERT OR REPLACE INTO account_equity_daily
      (date, account_value, cash, buying_power, margin_used, margin_used_pct)
    VALUES (?, ?, ?, ?, ?, ?)
  `
  await db.prepare(sql)
    .bind(
      payload.date,
      payload.account_value,
      payload.cash,
      payload.buying_power,
      payload.margin_used,
      payload.margin_used_pct
    )
    .run()

  console.log(`[INGEST] account_equity ok: ${payload.date} (event ${eventId})`)
}

/**
 * market_quote — daily OHLCV per symbol. Replay-safe via (symbol, date) PK.
 */
async function handleMarketQuote(
  db: D1Database,
  eventId: string,
  payload: z.infer<typeof MarketQuotePayloadSchema>
) {
  const sql = `
    INSERT OR REPLACE INTO market_quotes_daily
      (symbol, date, open, high, low, close, volume)
    VALUES (?, ?, ?, ?, ?, ?, ?)
  `
  await db.prepare(sql)
    .bind(
      payload.symbol,
      payload.date,
      payload.open ?? null,
      payload.high ?? null,
      payload.low ?? null,
      payload.close,
      payload.volume ?? null
    )
    .run()

  console.log(`[INGEST] market_quote ok: ${payload.symbol}@${payload.date} (event ${eventId})`)
}

/**
 * vix_snapshot — denormalized curve. Writes the term-structure row AND mirrors
 * each non-null leg into market_quotes_daily so downstream chart endpoints can
 * treat VIX/VIX1D/VIX3M/VIX6M as regular symbols.
 */
async function handleVixSnapshot(
  db: D1Database,
  eventId: string,
  payload: z.infer<typeof VixSnapshotPayloadSchema>
) {
  // Primary: denormalized term-structure row
  const curveSql = `
    INSERT OR REPLACE INTO vix_term_structure
      (date, vix, vix1d, vix3m, vix6m)
    VALUES (?, ?, ?, ?, ?)
  `
  await db.prepare(curveSql)
    .bind(
      payload.date,
      payload.vix ?? null,
      payload.vix1d ?? null,
      payload.vix3m ?? null,
      payload.vix6m ?? null
    )
    .run()

  // Secondary: mirror every present leg into market_quotes_daily as close-only
  const legs: Array<{ symbol: string; close: number | null | undefined }> = [
    { symbol: 'VIX', close: payload.vix },
    { symbol: 'VIX1D', close: payload.vix1d },
    { symbol: 'VIX3M', close: payload.vix3m },
    { symbol: 'VIX6M', close: payload.vix6m }
  ]

  const mirrorSql = `
    INSERT OR REPLACE INTO market_quotes_daily
      (symbol, date, open, high, low, close, volume)
    VALUES (?, ?, ?, ?, ?, ?, ?)
  `

  for (const leg of legs) {
    if (leg.close === null || leg.close === undefined) continue
    await db.prepare(mirrorSql)
      .bind(leg.symbol, payload.date, null, null, null, leg.close, null)
      .run()
  }

  console.log(`[INGEST] vix_snapshot ok: ${payload.date} (event ${eventId})`)
}

/**
 * benchmark_close — pre-normalized benchmark close. Used for chart overlay.
 */
async function handleBenchmarkClose(
  db: D1Database,
  eventId: string,
  payload: z.infer<typeof BenchmarkClosePayloadSchema>
) {
  const sql = `
    INSERT OR REPLACE INTO benchmark_series
      (symbol, date, close, close_normalized)
    VALUES (?, ?, ?, ?)
  `
  await db.prepare(sql)
    .bind(
      payload.symbol,
      payload.date,
      payload.close,
      payload.close_normalized ?? null
    )
    .run()

  console.log(`[INGEST] benchmark_close ok: ${payload.symbol}@${payload.date} (event ${eventId})`)
}

/**
 * position_greeks — rolling time-series. PK is (position_id, snapshot_ts) so
 * two distinct snapshots for the same position are both preserved.
 */
async function handlePositionGreeks(
  db: D1Database,
  eventId: string,
  payload: z.infer<typeof PositionGreeksPayloadSchema>
) {
  const sql = `
    INSERT OR REPLACE INTO position_greeks
      (position_id, snapshot_ts, delta, gamma, theta, vega, iv, underlying_price)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?)
  `
  await db.prepare(sql)
    .bind(
      payload.position_id,
      payload.snapshot_ts,
      payload.delta ?? null,
      payload.gamma ?? null,
      payload.theta ?? null,
      payload.vega ?? null,
      payload.iv ?? null,
      payload.underlying_price ?? null
    )
    .run()

  console.log(
    `[INGEST] position_greeks ok: ${payload.position_id}@${payload.snapshot_ts} (event ${eventId})`
  )
}

/**
 * web_vitals — single browser Core Web Vitals sample. PK (session_id, name,
 * timestamp) ensures re-send of the same metric (e.g. the web-vitals library
 * firing a second time with the same id on a back-forward navigation) does
 * not bloat the table. Timestamp defaults to server-time if the client omits.
 */
async function handleWebVitals(
  db: D1Database,
  eventId: string,
  payload: z.infer<typeof WebVitalsPayloadSchema>
) {
  const ts = payload.timestamp ?? new Date().toISOString()
  const sql = `
    INSERT OR REPLACE INTO web_vitals
      (session_id, name, value, rating, navigation_type, metric_id, timestamp)
    VALUES (?, ?, ?, ?, ?, ?, ?)
  `
  await db.prepare(sql)
    .bind(
      payload.session_id,
      payload.name,
      payload.value,
      payload.rating ?? null,
      payload.navigationType ?? null,
      payload.id ?? null,
      ts
    )
    .run()

  console.log(
    `[INGEST] web_vitals ok: ${payload.name}=${payload.value} (session ${payload.session_id}, event ${eventId})`
  )
}

/**
 * order_audit — one row per order attempt. PK audit_id ensures replays are
 * idempotent. INSERT OR REPLACE semantics so a delayed/retried write overwrites
 * a stale interim row (e.g. a "placed" row that later became "filled" reuses
 * the same audit_id if the upstream chose to — but OrderPlacer mints a fresh
 * GUID per write, so in practice INSERT OR REPLACE just protects against exact
 * duplicate retries).
 */
async function handleOrderAudit(
  db: D1Database,
  eventId: string,
  payload: z.infer<typeof OrderAuditPayloadSchema>
) {
  const sql = `
    INSERT OR REPLACE INTO order_audit_log
      (audit_id, order_id, ts, actor, strategy_id, contract_symbol,
       side, quantity, price, semaphore_status, outcome, override_reason,
       details_json)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `
  await db.prepare(sql)
    .bind(
      payload.audit_id,
      payload.order_id ?? null,
      payload.ts,
      payload.actor,
      payload.strategy_id ?? null,
      payload.contract_symbol,
      payload.side,
      payload.quantity,
      payload.price ?? null,
      payload.semaphore_status,
      payload.outcome,
      payload.override_reason ?? null,
      payload.details_json ?? null
    )
    .run()

  console.log(`[INGEST] order_audit ok: ${payload.outcome} ${payload.contract_symbol} (event ${eventId})`)
}

export { ingest }
