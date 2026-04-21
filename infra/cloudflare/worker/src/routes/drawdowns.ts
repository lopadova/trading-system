/**
 * Drawdowns API Route
 * Returns underwater-curve series and the worst historical drawdowns.
 *
 * Phase 7.2 — Replaces the deterministic mock arrays with real SQL queries
 * against `account_equity_daily` (portfolio) and `benchmark_series` (S&P 500
 * overlay). Drawdowns are computed directly in SQL using window functions:
 *
 *   MAX(account_value) OVER (ORDER BY date ROWS BETWEEN UNBOUNDED PRECEDING
 *                            AND CURRENT ROW)
 *
 * which gives the rolling peak up to each row; the drawdown % is then
 * (value - peak) / peak * 100. Groups of consecutive negative-drawdown rows
 * form a "drawdown episode"; the deepest point of each episode is kept and
 * the top N episodes by absolute depth are returned as `worst`.
 *
 * Response shape preserved; fallback-mock used when the tables are empty.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type {
  AssetBucket,
  DrawdownRange,
  DrawdownsResponse,
  WorstDrawdown,
} from '../types/api'
import { authMiddleware } from '../middleware/auth'

// ---------------------------------------------------------------------------
// Allowed query-parameter values
// ---------------------------------------------------------------------------
const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']
const RANGES: DrawdownRange[] = ['Max', '10Y', '5Y', '1Y', 'YTD', '6M']

// Trading-day windows per range. `Max`/`10Y` effectively pull the full
// history (cap at 10Y) when more than ~2520 bars exist.
const RANGE_DAYS: Record<DrawdownRange, number | 'ytd'> = {
  Max: 99999,
  '10Y': 2520,
  '5Y': 1260,
  '1Y': 252,
  YTD: 'ytd',
  '6M': 132,
}

// TODO(Phase 7.x): replace scaling with real per-asset-bucket decomposition.
function scaleFor(asset: AssetBucket): number {
  if (asset === 'systematic') return 0.7
  if (asset === 'options') return 1.4
  if (asset === 'other') return 0.3
  return 1
}

// ---------------------------------------------------------------------------
// D1 row shape — produced by the window-function SQL below.
// ---------------------------------------------------------------------------
interface EquityDrawdownRow {
  date: string
  drawdown_pct: number
}

// ---------------------------------------------------------------------------
// Fallback mock (pre-Phase-7.2)
// ---------------------------------------------------------------------------
const FALLBACK_PORT = [
  0, -1.2, -0.8, -2.1, -4.5, -3.2, -1.8, -0.6, 0, 0,
  -0.5, -1.2, -2.1, -1.4, -0.8, 0, -0.3, -1.1, -3.2, -5.8,
  -8.9, -12.4, -15.8, -18.2, -19.9, -18.5, -16.2, -13.1, -10.4, -7.2,
  -4.8, -2.9, -1.5, -0.6, 0, 0, -0.4, -1.2, -2.8, -3.1,
  -2.4, -1.6, -0.8, 0, -0.3, -1.1, -0.7, -0.2, 0, 0,
  -0.4, -0.9, -1.6, -2.4, -3.1, -3.8, -4.4, -5.1, -5.9, -6.6,
  -7.2, -6.8, -6.2, -5.4, -4.6, -3.8, -3.0, -2.3, -1.6, -1.0,
  -0.5, -0.2, 0, 0, -0.3, -0.5, -0.8, -0.4, -0.1, 0,
]

const FALLBACK_SP = [
  0, -0.8, -0.4, -1.1, -2.3, -1.5, -0.8, -0.2, 0, 0,
  -0.6, -1.8, -3.2, -2.1, -1.3, 0, -0.5, -1.8, -5.4, -9.8,
  -14.2, -18.6, -21.3, -23.8, -25.1, -22.4, -18.8, -14.9, -11.2, -7.8,
  -5.2, -3.1, -1.8, -0.8, 0, 0, -0.6, -1.9, -4.2, -4.8,
  -3.6, -2.4, -1.2, 0, -0.5, -1.8, -1.1, -0.3, 0, 0,
  -0.7, -1.6, -2.8, -4.2, -5.6, -6.8, -8.1, -9.4, -10.8, -12.1,
  -13.2, -12.4, -11.2, -9.6, -8.1, -6.8, -5.4, -4.1, -2.8, -1.7,
  -0.8, -0.3, 0, 0, -0.5, -0.9, -1.4, -0.7, -0.2, 0,
]

const FALLBACK_CROP: Record<DrawdownRange, number> = {
  Max: 80, '10Y': 80, '5Y': 60, '1Y': 12, YTD: 4, '6M': 6,
}

const FALLBACK_WORST: WorstDrawdown[] = [
  { depthPct: -7.92, start: 'Nov 2023', end: 'Jan 2025', months: 14 },
  { depthPct: -6.68, start: 'Oct 2025', end: 'Dec 2025', months: 2 },
  { depthPct: -3.92, start: 'Jan 2021', end: 'Mar 2021', months: 2 },
  { depthPct: -0.65, start: 'Mar 2026', end: 'Apr 2026', months: 1 },
]

// ---------------------------------------------------------------------------
// Route handler
// ---------------------------------------------------------------------------
export const drawdowns = new Hono<{ Bindings: Env }>()

// All drawdown aggregate routes require authentication
drawdowns.use('*', authMiddleware)

drawdowns.get('/', async (c) => {
  const asset = (c.req.query('asset') ?? 'all') as AssetBucket
  const range = (c.req.query('range') ?? '10Y') as DrawdownRange

  if (!ASSETS.includes(asset)) {
    return c.json({ error: 'invalid_asset', message: 'asset must be one of all|systematic|options|other' }, 400)
  }
  if (!RANGES.includes(range)) {
    return c.json({ error: 'invalid_range', message: 'range must be one of Max|10Y|5Y|1Y|YTD|6M' }, 400)
  }

  try {
    // Build the date cutoff. For 'ytd' we filter by calendar year; otherwise
    // by a trailing trading-day window. SQLite's date('now', '-N days') gives
    // a calendar-day cutoff; trading-day vs calendar-day skew is acceptable
    // here since we consume whatever rows exist inside the window.
    const rangeSpec = RANGE_DAYS[range]

    // We compute drawdown with a window function rather than post-processing
    // in JS so that D1 returns only what the chart needs. The CTE computes
    // running-peak; the SELECT computes percent drawdown from peak.
    let portfolioSql: string
    let portfolioBinds: unknown[] = []
    if (rangeSpec === 'ytd') {
      portfolioSql =
        "WITH running AS (" +
        "  SELECT date, account_value, " +
        "         MAX(account_value) OVER (ORDER BY date " +
        "           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peak " +
        "  FROM account_equity_daily " +
        "  WHERE date >= strftime('%Y-01-01', 'now') " +
        ") " +
        "SELECT date, " +
        "       CASE WHEN peak > 0 THEN (account_value - peak) * 100.0 / peak ELSE 0 END AS drawdown_pct " +
        "FROM running ORDER BY date ASC"
    } else {
      const days = rangeSpec
      portfolioSql =
        "WITH running AS (" +
        "  SELECT date, account_value, " +
        "         MAX(account_value) OVER (ORDER BY date " +
        "           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peak " +
        "  FROM account_equity_daily " +
        "  WHERE date >= date('now', ?) " +
        ") " +
        "SELECT date, " +
        "       CASE WHEN peak > 0 THEN (account_value - peak) * 100.0 / peak ELSE 0 END AS drawdown_pct " +
        "FROM running ORDER BY date ASC"
      portfolioBinds = [`-${days} days`]
    }

    const portfolioRes = await c.env.DB
      .prepare(portfolioSql)
      .bind(...portfolioBinds)
      .all<EquityDrawdownRow>()
    const portfolioRows = portfolioRes.results ?? []

    if (portfolioRows.length === 0) {
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(fallbackPayload(asset, range))
    }

    // Same window for S&P 500 (symbol 'SPX'). Same SQL shape with an extra
    // symbol filter.
    let sp500Sql: string
    let sp500Binds: unknown[]
    if (rangeSpec === 'ytd') {
      sp500Sql =
        "WITH running AS (" +
        "  SELECT date, close, " +
        "         MAX(close) OVER (ORDER BY date " +
        "           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peak " +
        "  FROM market_quotes_daily " +
        "  WHERE symbol = 'SPX' AND date >= strftime('%Y-01-01', 'now') " +
        ") " +
        "SELECT date, " +
        "       CASE WHEN peak > 0 THEN (close - peak) * 100.0 / peak ELSE 0 END AS drawdown_pct " +
        "FROM running ORDER BY date ASC"
      sp500Binds = []
    } else {
      sp500Sql =
        "WITH running AS (" +
        "  SELECT date, close, " +
        "         MAX(close) OVER (ORDER BY date " +
        "           ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS peak " +
        "  FROM market_quotes_daily " +
        "  WHERE symbol = 'SPX' AND date >= date('now', ?) " +
        ") " +
        "SELECT date, " +
        "       CASE WHEN peak > 0 THEN (close - peak) * 100.0 / peak ELSE 0 END AS drawdown_pct " +
        "FROM running ORDER BY date ASC"
      sp500Binds = [`-${rangeSpec as number} days`]
    }

    const sp500Res = await c.env.DB
      .prepare(sp500Sql)
      .bind(...sp500Binds)
      .all<EquityDrawdownRow>()
    const sp500Rows = sp500Res.results ?? []

    const scale = scaleFor(asset)
    const portfolioSeries = portfolioRows.map((r) => +(r.drawdown_pct * scale).toFixed(2))
    const sp500Series = sp500Rows.map((r) => +r.drawdown_pct.toFixed(2))

    const worst = computeWorst(portfolioRows, 4).map((w) => ({
      ...w,
      depthPct: +(w.depthPct * scale).toFixed(2),
    }))

    const payload: DrawdownsResponse = {
      asset,
      range,
      portfolioSeries,
      sp500Series,
      worst,
    }
    return c.json(payload)
  } catch (error) {
    console.error('drawdowns query failed:', error)
    c.header('X-Data-Source', 'fallback-mock')
    return c.json(fallbackPayload(asset, range))
  }
})

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Identify contiguous drawdown episodes (a run of rows with drawdown_pct < 0
 * bounded by zero or recovery). For each, record the deepest negative value
 * and the start/end dates. Return the top N by absolute depth.
 */
export function computeWorst(
  rows: EquityDrawdownRow[],
  topN: number,
): WorstDrawdown[] {
  const episodes: WorstDrawdown[] = []

  let epStart: string | null = null
  let epEnd: string | null = null
  let epDepth = 0

  for (let i = 0; i < rows.length; i++) {
    const row = rows[i]
    if (!row) continue
    const dd = row.drawdown_pct

    if (dd < 0) {
      if (epStart === null) epStart = row.date
      if (dd < epDepth) epDepth = dd
      epEnd = row.date
      continue
    }

    // dd >= 0 — episode ends (if one was open).
    if (epStart !== null && epEnd !== null) {
      episodes.push({
        depthPct: +epDepth.toFixed(2),
        start: formatMonthYear(epStart),
        end: formatMonthYear(epEnd),
        months: monthsBetween(epStart, epEnd),
      })
      epStart = null
      epEnd = null
      epDepth = 0
    }
  }

  // Close an open episode that runs to the end of the series.
  if (epStart !== null && epEnd !== null) {
    episodes.push({
      depthPct: +epDepth.toFixed(2),
      start: formatMonthYear(epStart),
      end: formatMonthYear(epEnd),
      months: monthsBetween(epStart, epEnd),
    })
  }

  // Deepest first (most negative is worst).
  episodes.sort((a, b) => a.depthPct - b.depthPct)
  return episodes.slice(0, topN)
}

/**
 * Format YYYY-MM-DD → "MMM YYYY" (e.g. "Nov 2023"). Using a static month
 * name array avoids the culture-dependent `toLocaleString` and keeps the
 * output identical across every Worker isolate.
 */
function formatMonthYear(iso: string): string {
  const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']
  const parts = iso.split('-')
  const y = parts[0] ?? ''
  const mStr = parts[1] ?? ''
  const m = parseInt(mStr, 10) - 1
  const monthName = MONTHS[m] ?? ''
  return `${monthName} ${y}`
}

/**
 * Integer months (inclusive) between two YYYY-MM-DD strings. Returns at
 * least 1 so an episode that starts and ends in the same month still counts
 * as 1 month of duration (matches the pre-Phase-7.2 mock format).
 */
function monthsBetween(startIso: string, endIso: string): number {
  const startParts = startIso.split('-')
  const endParts = endIso.split('-')
  const ys = parseInt(startParts[0] ?? '0', 10)
  const ms = parseInt(startParts[1] ?? '0', 10)
  const ye = parseInt(endParts[0] ?? '0', 10)
  const me = parseInt(endParts[1] ?? '0', 10)
  const diff = (ye - ys) * 12 + (me - ms)
  return Math.max(1, diff + 1)
}

/**
 * Deterministic mock payload used when D1 has no data or the query fails.
 * Preserves the pre-Phase-7.2 output shape and numbers so the dashboard
 * never renders a blank underwater chart.
 */
function fallbackPayload(asset: AssetBucket, range: DrawdownRange): DrawdownsResponse {
  const crop = FALLBACK_CROP[range]
  const s = scaleFor(asset)
  return {
    asset,
    range,
    portfolioSeries: FALLBACK_PORT.slice(-crop).map((v) => +(v * s).toFixed(2)),
    sp500Series: FALLBACK_SP.slice(-crop),
    worst: FALLBACK_WORST.map((w) => ({ ...w, depthPct: +(w.depthPct * s).toFixed(2) })),
  }
}
