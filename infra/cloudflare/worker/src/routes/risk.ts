/**
 * Risk API Route
 * Returns the aggregated risk snapshot (Greeks + implied-vol + margin) and
 * the composite Options Trading Semaphore indicator.
 *
 * Phase 7.2 — Replaces hardcoded values with real D1 queries:
 *   /metrics   → vix_term_structure + position_greeks + account_equity_daily
 *                + market_quotes_daily (IV rank)
 *   /semaphore → computed from SPX history vs 200-day MA, VIX 1Y percentile,
 *                30-day rolling yield percentile, IVTS ratio
 *
 * Response shapes preserved. When underlying tables are empty we fall back
 * to the pre-Phase-7.2 mock payload and set `X-Data-Source: fallback-mock`.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type {
  RiskMetrics,
  SemaphoreData,
  SemaphoreIndicator,
  SemaphoreRegime,
  SemaphoreStatus,
} from '../types/api'
import { authMiddleware } from '../middleware/auth'

export const risk = new Hono<{ Bindings: Env }>()

// All risk routes require authentication
risk.use('*', authMiddleware)

// ---------------------------------------------------------------------------
// /metrics handler
// ---------------------------------------------------------------------------
risk.get('/metrics', async (c) => {
  try {
    const metrics = await computeMetrics(c.env.DB)
    if (!metrics) {
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(fallbackMetrics())
    }
    return c.json(metrics)
  } catch (error) {
    console.error('risk/metrics query failed:', error)
    c.header('X-Data-Source', 'fallback-mock')
    return c.json(fallbackMetrics())
  }
})

/**
 * Aggregate the risk metrics from D1. Returns null ONLY when every single
 * source table is empty — that way a partially-populated database still gives
 * us a best-effort answer rather than falling through to the mock.
 */
async function computeMetrics(db: D1Database): Promise<RiskMetrics | null> {
  // Latest VIX curve row
  const vixRow = await db
    .prepare('SELECT vix, vix1d, vix3m FROM vix_term_structure ORDER BY date DESC LIMIT 1')
    .first<{ vix: number | null; vix1d: number | null; vix3m: number | null }>()

  // Aggregated Greeks: latest snapshot per position, then SUM.
  const greeksRow = await db
    .prepare(
      "WITH latest AS (" +
      "  SELECT position_id, MAX(snapshot_ts) AS max_ts FROM position_greeks GROUP BY position_id" +
      ") " +
      "SELECT " +
      "  COALESCE(SUM(g.delta), 0) AS delta_sum, " +
      "  COALESCE(SUM(g.theta), 0) AS theta_sum, " +
      "  COALESCE(SUM(g.vega),  0) AS vega_sum, " +
      "  COUNT(*) AS n " +
      "FROM position_greeks g " +
      "JOIN latest l ON l.position_id = g.position_id AND l.max_ts = g.snapshot_ts",
    )
    .first<{ delta_sum: number; theta_sum: number; vega_sum: number; n: number }>()

  // Latest account_equity row (buying power + margin pct)
  const equityRow = await db
    .prepare('SELECT buying_power, margin_used_pct FROM account_equity_daily ORDER BY date DESC LIMIT 1')
    .first<{ buying_power: number; margin_used_pct: number }>()

  // IV rank for SPY (percentile of current IV over last 1Y). We source the IV
  // from `market_quotes_daily` — the spec notes this depends on collectors
  // eventually writing IV data; for now we compute rank from SPX close
  // percentile as a proxy if SPY IV is absent. When no proxy exists, return
  // null for ivRankSpy (matches the RiskMetrics type which permits null).
  const ivRank = await computeIvRankSpy(db)

  const allEmpty = !vixRow && (!greeksRow || greeksRow.n === 0) && !equityRow && ivRank === null
  if (allEmpty) return null

  return {
    vix: vixRow?.vix ?? null,
    vix1d: vixRow?.vix1d ?? null,
    vix3m: vixRow?.vix3m ?? null,
    delta: greeksRow ? +greeksRow.delta_sum.toFixed(2) : 0,
    theta: greeksRow ? +greeksRow.theta_sum.toFixed(2) : 0,
    vega: greeksRow ? +greeksRow.vega_sum.toFixed(2) : 0,
    ivRankSpy: ivRank,
    buyingPower: equityRow ? +equityRow.buying_power.toFixed(2) : 0,
    marginUsedPct: equityRow ? +equityRow.margin_used_pct.toFixed(2) : 0,
  }
}

/**
 * Compute IV rank (0..1) for SPY/SPX over the last 1Y.
 *
 * IV rank = (count of rows with close <= current close) / total count.
 *
 * This is a placeholder that uses SPX close as the "IV proxy" — if/when real
 * implied-vol data arrives via the ingest pipeline, swap the symbol/column
 * below without changing the response contract. Returns null when <2
 * historical points exist (can't compute a meaningful percentile).
 */
async function computeIvRankSpy(db: D1Database): Promise<number | null> {
  const latest = await db
    .prepare(
      "SELECT close FROM market_quotes_daily " +
      "WHERE symbol IN ('SPX','SPY') AND date >= date('now', '-365 days') " +
      "ORDER BY date DESC LIMIT 1",
    )
    .first<{ close: number }>()
  if (!latest) return null

  const counts = await db
    .prepare(
      "SELECT " +
      "  SUM(CASE WHEN close <= ? THEN 1 ELSE 0 END) AS at_or_below, " +
      "  COUNT(*) AS total " +
      "FROM market_quotes_daily " +
      "WHERE symbol IN ('SPX','SPY') AND date >= date('now', '-365 days')",
    )
    .bind(latest.close)
    .first<{ at_or_below: number; total: number }>()
  if (!counts || counts.total < 2) return null
  return +(counts.at_or_below / counts.total).toFixed(2)
}

function fallbackMetrics(): RiskMetrics {
  return {
    vix: 15.84,
    vix1d: 11.20,
    vix3m: 17.20,
    delta: 42.3,
    theta: -58.20,
    vega: 124.5,
    ivRankSpy: 0.34,
    buyingPower: 48200,
    marginUsedPct: 61,
  }
}

// ---------------------------------------------------------------------------
// /semaphore handler
// ---------------------------------------------------------------------------

/**
 * GET /api/risk/semaphore
 * Composite indicator telling the operator whether it is safe to sell PUTs on
 * SPX. Returns 4 sub-indicators + an overall status + a 0-100 risk score.
 *
 * Phase 7.2 wiring:
 *  - SPX regime    → latest SPX close vs 200-day SMA (market_quotes_daily)
 *  - VIX level     → current VIX vs 80th percentile of last 1Y (vix_term_structure)
 *  - Rolling yield → (VIX/VIX3M - 1) current vs 50th pct of last 1Y series
 *  - IVTS          → VIX / VIX3M current
 */
risk.get('/semaphore', async (c) => {
  try {
    const payload = await computeSemaphoreFromDb(c.env.DB)
    if (!payload) {
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(computeSemaphore())
    }
    return c.json(payload)
  } catch (error) {
    console.error('risk/semaphore query failed:', error)
    c.header('X-Data-Source', 'fallback-mock')
    return c.json(computeSemaphore())
  }
})

interface SpxContext {
  price: number
  prevClose: number | null
  ma200: number
}

interface VixContext {
  current: number
  ma200FromTable: number | null  // for IV-rank-style calcs
  percentile: number             // 0..100
  p80: number                    // 80th percentile value for display
  prevClose: number | null
}

interface RollingYieldContext {
  current: number
  percentile: number // 0..100
}

/**
 * Load the market-data inputs required by the semaphore computation. Returns
 * null if any critical input is missing — the caller falls back to the mock
 * in that case, surfacing `X-Data-Source: fallback-mock` to the dashboard.
 */
async function loadSemaphoreInputs(
  db: D1Database,
): Promise<
  | { spx: SpxContext; vix: VixContext; rollingYield: RollingYieldContext | null; vix3m: number; asOf: string }
  | null
> {
  // --- SPX: latest close + 200-day MA ---
  const spxRow = await db
    .prepare(
      "SELECT close FROM market_quotes_daily " +
      "WHERE symbol = 'SPX' ORDER BY date DESC LIMIT 1",
    )
    .first<{ close: number }>()
  if (!spxRow) return null

  const spxPrev = await db
    .prepare(
      "SELECT close FROM market_quotes_daily " +
      "WHERE symbol = 'SPX' ORDER BY date DESC LIMIT 1 OFFSET 1",
    )
    .first<{ close: number }>()

  const spxMa = await db
    .prepare(
      "SELECT AVG(close) AS ma FROM (" +
      "  SELECT close FROM market_quotes_daily " +
      "  WHERE symbol = 'SPX' ORDER BY date DESC LIMIT 200" +
      ")",
    )
    .first<{ ma: number | null }>()
  const spxMa200 = spxMa?.ma ?? spxRow.close

  // --- VIX: latest value + 1Y series for percentile calculations ---
  const vixLatestRow = await db
    .prepare('SELECT date, vix, vix3m FROM vix_term_structure ORDER BY date DESC LIMIT 1')
    .first<{ date: string; vix: number | null; vix3m: number | null }>()
  if (!vixLatestRow || vixLatestRow.vix === null || vixLatestRow.vix3m === null) return null

  const vixCurrent = vixLatestRow.vix
  const vix3mCurrent = vixLatestRow.vix3m

  const vixPrevRow = await db
    .prepare(
      'SELECT vix FROM vix_term_structure ORDER BY date DESC LIMIT 1 OFFSET 1',
    )
    .first<{ vix: number | null }>()

  const vix1yRes = await db
    .prepare(
      "SELECT vix, vix3m, date FROM vix_term_structure " +
      "WHERE date >= date('now', '-365 days') AND vix IS NOT NULL AND vix3m IS NOT NULL " +
      "ORDER BY date ASC",
    )
    .all<{ vix: number; vix3m: number; date: string }>()
  const vix1y = vix1yRes.results ?? []

  const vixValues = vix1y.map((r) => r.vix).sort((a, b) => a - b)
  const vixPct = percentileRank(vixValues, vixCurrent)
  const p80 = valueAtPercentile(vixValues, 80)

  // --- Rolling yield: 30d series of (VIX/VIX3M - 1)*100 ---
  let rollingYield: RollingYieldContext | null = null
  if (vix1y.length >= 30) {
    const yieldsAll = vix1y.map((r) => ((r.vix / r.vix3m) - 1) * 100)
    // Current = mean of last 30 trading-day values. Percentile = current vs
    // the full 1Y series of such rolling-mean snapshots.
    const last30 = yieldsAll.slice(-30)
    const current = last30.reduce((s, v) => s + v, 0) / last30.length
    // Build the rolling-mean series for the percentile denominator.
    const rollingSeries: number[] = []
    for (let i = 29; i < yieldsAll.length; i++) {
      const window = yieldsAll.slice(i - 29, i + 1)
      const m = window.reduce((s, v) => s + v, 0) / window.length
      rollingSeries.push(m)
    }
    const sortedSeries = rollingSeries.slice().sort((a, b) => a - b)
    const pct = percentileRank(sortedSeries, current)
    rollingYield = { current, percentile: pct }
  }

  return {
    spx: {
      price: spxRow.close,
      prevClose: spxPrev?.close ?? null,
      ma200: spxMa200,
    },
    vix: {
      current: vixCurrent,
      ma200FromTable: null,
      percentile: vixPct,
      p80,
      prevClose: vixPrevRow?.vix ?? null,
    },
    rollingYield,
    vix3m: vix3mCurrent,
    asOf: new Date().toISOString(),
  }
}

/**
 * Return the percentile rank (0..100) of `value` within a SORTED ascending
 * array. Uses the fraction of values strictly below, then scales to 0..100.
 */
function percentileRank(sortedAsc: number[], value: number): number {
  if (sortedAsc.length === 0) return 50
  let below = 0
  for (const v of sortedAsc) {
    if (v < value) below++
    else break
  }
  return Math.round((below / sortedAsc.length) * 100)
}

/**
 * Return the value at the given percentile (0..100) in a SORTED ascending
 * array using linear interpolation.
 */
function valueAtPercentile(sortedAsc: number[], pct: number): number {
  if (sortedAsc.length === 0) return 0
  if (sortedAsc.length === 1) return sortedAsc[0] ?? 0
  const clamped = Math.max(0, Math.min(100, pct))
  const idx = (clamped / 100) * (sortedAsc.length - 1)
  const lo = Math.floor(idx)
  const hi = Math.ceil(idx)
  const loVal = sortedAsc[lo]
  const hiVal = sortedAsc[hi]
  if (loVal === undefined || hiVal === undefined) return 0
  if (lo === hi) return loVal
  const frac = idx - lo
  return loVal + (hiVal - loVal) * frac
}

/**
 * Build the full SemaphoreData payload from already-loaded inputs.
 * Extracted so both the real and mock paths share the exact same indicator +
 * score formulae.
 *
 * Score mapping (0-100, higher = more dangerous):
 *    regime bearish                    → +20
 *    vix percentile above 80th         → +30
 *    rolling yield below 50th pct      → +20
 *    ivts 1.00-1.15                    → +25
 *    ivts > 1.15                       → +50   (dominant)
 */
function buildSemaphorePayload(input: {
  spxPrice: number
  spxChange: number
  spxChangePct: number
  spxMa200: number
  vixPrice: number
  vixChange: number
  vixChangePct: number
  vix1yP80: number
  vixPercentile: number
  rollingYield: number
  rollingYieldPercentile: number
  vix3m: number
  asOf: Date
}): SemaphoreData {
  const {
    spxPrice, spxChange, spxChangePct, spxMa200,
    vixPrice, vixChange, vixChangePct, vix1yP80, vixPercentile,
    rollingYield, rollingYieldPercentile, vix3m, asOf,
  } = input

  const ivts = vix3m === 0 ? 0 : vixPrice / vix3m

  // ---- Indicator 1 — Market Regime ----
  const regimeBullish = spxPrice > spxMa200
  const regimeLabel: SemaphoreRegime = regimeBullish ? 'BULLISH' : 'BEARISH'
  const regime: SemaphoreIndicator = {
    id: 'regime',
    label: 'Market Regime',
    status: regimeBullish ? 'green' : 'red',
    value: regimeBullish ? 'SPX > MA200' : 'SPX < MA200',
    detail: regimeBullish
      ? `SPX ${spxPrice.toFixed(2)} above 200-day MA ${spxMa200.toFixed(2)}`
      : `SPX ${spxPrice.toFixed(2)} below 200-day MA ${spxMa200.toFixed(2)}`,
  }

  // ---- Indicator 2 — VIX Level vs 1Y history ----
  const vixAbove80thPct = vixPrice > vix1yP80
  const vix_level: SemaphoreIndicator = {
    id: 'vix_level',
    label: 'VIX Level (1Y pct)',
    status: vixAbove80thPct ? 'red' : 'green',
    value: vixPrice,
    detail: `${vixPrice.toFixed(2)} (${vixPercentile}th pct, 1Y)`,
  }

  // ---- Indicator 3 — VIX 30d Rolling Yield ----
  const rollingYieldAboveMedian = rollingYieldPercentile > 50
  const vix_rolling_yield: SemaphoreIndicator = {
    id: 'vix_rolling_yield',
    label: 'VIX Rolling Yield (30d)',
    status: rollingYieldAboveMedian ? 'green' : 'orange',
    value: +rollingYield.toFixed(2),
    detail: `${rollingYield.toFixed(2)} (${rollingYieldPercentile}th pct, 1Y)`,
  }

  // ---- Indicator 4 — IVTS = VIX / VIX3M ----
  const ivtsStatus: SemaphoreStatus =
    ivts > 1.15 ? 'red' : ivts > 1.0 ? 'orange' : 'green'
  const ivtsLabel =
    ivts > 1.15
      ? 'strong backwardation'
      : ivts > 1.0
        ? 'mild backwardation'
        : 'contango'
  const ivtsIndicator: SemaphoreIndicator = {
    id: 'ivts',
    label: 'IVTS (VIX / VIX3M)',
    status: ivtsStatus,
    value: Number(ivts.toFixed(3)),
    detail: `${ivts.toFixed(2)} (${ivtsLabel})`,
  }

  // ---- Indicator 5 — Overall aggregation ----
  const marketStatuses: SemaphoreStatus[] = [
    vix_level.status,
    vix_rolling_yield.status,
    ivtsIndicator.status,
  ]
  const anyRed = marketStatuses.some((s) => s === 'red')
  const anyOrange = marketStatuses.some((s) => s === 'orange')
  const allGreen =
    regime.status === 'green' && marketStatuses.every((s) => s === 'green')

  let overallStatus: SemaphoreStatus
  if (anyRed) {
    overallStatus = 'red'
  } else if (regime.status === 'red' && anyOrange) {
    overallStatus = 'red'
  } else if (allGreen) {
    overallStatus = 'green'
  } else {
    overallStatus = 'orange'
  }

  // ---- Score (0-100) ----
  let score = 0
  if (regime.status === 'red') score += 20
  if (vixAbove80thPct) score += 30
  if (!rollingYieldAboveMedian) score += 20
  if (ivts > 1.15) {
    score += 50
  } else if (ivts > 1.0) {
    score += 25
  }
  if (score > 100) score = 100
  if (score < 0) score = 0

  const overall: SemaphoreIndicator = {
    id: 'overall',
    label: 'Operatività',
    status: overallStatus,
    value: score,
    detail:
      overallStatus === 'green'
        ? 'Safe to sell PUTs on SPX'
        : overallStatus === 'orange'
          ? 'Caution — hold existing, no new entries'
          : 'Stop operativity — consider closing positions',
  }

  return {
    score,
    status: overallStatus,
    indicators: [regime, vix_level, vix_rolling_yield, ivtsIndicator, overall],
    asOf: asOf.toISOString(),
    spx: { price: spxPrice, change: spxChange, changePct: spxChangePct },
    vix: { price: vixPrice, change: vixChange, changePct: vixChangePct },
    regime: regimeLabel,
    exchangeTime: formatExchangeTime(asOf),
  }
}

/**
 * Compute the semaphore payload from real D1 data. Returns null when
 * critical inputs (SPX close or VIX curve) are missing.
 */
async function computeSemaphoreFromDb(db: D1Database): Promise<SemaphoreData | null> {
  const inputs = await loadSemaphoreInputs(db)
  if (!inputs) return null

  const spxPrev = inputs.spx.prevClose
  const spxChange = spxPrev === null ? 0 : inputs.spx.price - spxPrev
  const spxChangePct = spxPrev === null || spxPrev === 0 ? 0 : (spxChange / spxPrev) * 100

  const vixPrev = inputs.vix.prevClose
  const vixChange = vixPrev === null ? 0 : inputs.vix.current - vixPrev
  const vixChangePct = vixPrev === null || vixPrev === 0 ? 0 : (vixChange / vixPrev) * 100

  const rollingYield = inputs.rollingYield?.current ?? 0
  const rollingYieldPercentile = inputs.rollingYield?.percentile ?? 50

  return buildSemaphorePayload({
    spxPrice: inputs.spx.price,
    spxChange: +spxChange.toFixed(2),
    spxChangePct: +spxChangePct.toFixed(2),
    spxMa200: inputs.spx.ma200,
    vixPrice: inputs.vix.current,
    vixChange: +vixChange.toFixed(2),
    vixChangePct: +vixChangePct.toFixed(2),
    vix1yP80: inputs.vix.p80,
    vixPercentile: inputs.vix.percentile,
    rollingYield,
    rollingYieldPercentile,
    vix3m: inputs.vix3m,
    asOf: new Date(inputs.asOf),
  })
}

/**
 * Pure mock-backed semaphore computation. Used as the fallback payload when
 * D1 has no data yet. Same formulae as buildSemaphorePayload so the two
 * never drift apart.
 */
export function computeSemaphore(): SemaphoreData {
  // Mock readings chosen to match the pre-Phase-7.2 reference screenshot.
  return buildSemaphorePayload({
    spxPrice: 6767.54,
    spxChange: 62.42,
    spxChangePct: 0.93,
    spxMa200: 6350,
    vixPrice: 18.56,
    vixChange: -1.96,
    vixChangePct: -9.55,
    vix1yP80: 24.5,
    vixPercentile: 42,
    rollingYield: 0.62,
    rollingYieldPercentile: 72,
    vix3m: 20.05,
    asOf: new Date(),
  })
}

/**
 * Format a UTC Date as "DD-MMM-YYYY HH:MM:SS ET" using America/New_York
 * timezone. Workers runtime supports Intl.DateTimeFormat with timeZone option.
 */
function formatExchangeTime(d: Date): string {
  try {
    // Use en-GB to get day-first ordering. Produces e.g. "25 Nov 2025, 15:45:52".
    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: 'America/New_York',
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    }).formatToParts(d)

    const lookup: Record<string, string> = {}
    for (const p of parts) {
      if (p.type !== 'literal') lookup[p.type] = p.value
    }
    const date = `${lookup.day}-${lookup.month}-${lookup.year}`
    const time = `${lookup.hour}:${lookup.minute}:${lookup.second}`
    return `${date} ${time} ET`
  } catch {
    return `${d.toISOString().replace('T', ' ').slice(0, 19)} UTC`
  }
}
