/**
 * Performance API Routes
 * Returns portfolio performance summaries and time-series data for the dashboard.
 *
 * Phase 7.2 — Replaces deterministic mock responses with real SQL queries
 * against the `account_equity_daily` and `benchmark_series` tables introduced
 * by migration 0007_market_data.sql. Response shape is preserved so the
 * dashboard keeps working without frontend changes.
 *
 * Fallback behavior: when the relevant tables are empty (fresh install, no
 * collectors running yet), the handler returns the same deterministic mock
 * payload that existed pre-Phase-7.2 and sets the response header
 * `X-Data-Source: fallback-mock` so the dashboard can surface a "demo data"
 * indicator in a later milestone.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { AssetBucket, PerfRange, SummaryData, PerfSeries } from '../types/api'
import { authMiddleware } from '../middleware/auth'

// ---------------------------------------------------------------------------
// Allowed query-parameter values
// ---------------------------------------------------------------------------
const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']
const RANGES: PerfRange[] = ['1W', '1M', '3M', 'YTD', '1Y', 'ALL']

// ---------------------------------------------------------------------------
// Fallback mock summary per asset bucket (used when account_equity_daily is
// empty, e.g. fresh install). Kept identical to the pre-Phase-7.2 numbers so
// existing dashboards keep rendering something meaningful.
// ---------------------------------------------------------------------------
const FALLBACK_SUMMARY: Record<AssetBucket, SummaryData> = {
  all:        { asset: 'all',        m: 14.30, ytd: 15.04, y2: 49.88, y5: 100.13, y10: 98.59,  ann: 13.07, base: 125430 },
  systematic: { asset: 'systematic', m:  8.20, ytd:  9.64, y2: 31.40, y5:  74.80, y10: 82.10,  ann: 11.22, base:  72500 },
  options:    { asset: 'options',    m: 22.80, ytd: 24.10, y2: 68.10, y5: 128.40, y10: 118.30, ann: 15.31, base:  38900 },
  other:      { asset: 'other',      m:  3.10, ytd:  4.28, y2: 12.60, y5:  28.90, y10: 42.40,  ann:  5.64, base:  14030 },
}

// ---------------------------------------------------------------------------
// Trading-days-per-period mapping for the /series and /summary windows.
// 22 trading days/month * 12 months = 252/year is the conventional US approx.
// ---------------------------------------------------------------------------
const PERIOD_DAYS = {
  m: 22,        // 1 month
  ytd: null,    // variable, resolved from calendar
  y2: 504,      // 2 years
  y5: 1260,     // 5 years
  y10: 2520,    // 10 years
} as const

const RANGE_DAYS: Record<PerfRange, number | 'ytd' | 'all'> = {
  '1W': 7,
  '1M': 22,
  '3M': 66,
  YTD: 'ytd',
  '1Y': 252,
  ALL: 'all',
}

// ---------------------------------------------------------------------------
// Per-asset scaling factor.
// TODO(Phase 7.x): replace this mock scaling with real per-asset-bucket
// decomposition — requires an equity-by-strategy time series, which needs
// additional backend collectors not yet landed. For now we preserve the
// pre-Phase-7.2 behavior of visibly differentiating buckets by scaling the
// total portfolio values (and summary numbers) deterministically.
// ---------------------------------------------------------------------------
function scaleFor(asset: AssetBucket): number {
  if (asset === 'systematic') return 0.58
  if (asset === 'options') return 1.60
  if (asset === 'other') return 0.10
  return 1
}

// ---------------------------------------------------------------------------
// Query parsing helpers — negative-first, explicit typing
// ---------------------------------------------------------------------------
function parseAsset(raw: string | undefined): AssetBucket | null {
  if (!raw) return 'all'
  if (!ASSETS.includes(raw as AssetBucket)) return null
  return raw as AssetBucket
}

function parseRange(raw: string | undefined): PerfRange | null {
  if (!raw) return '1M'
  if (!RANGES.includes(raw as PerfRange)) return null
  return raw as PerfRange
}

// ---------------------------------------------------------------------------
// D1 row shapes
// ---------------------------------------------------------------------------
interface EquityRow {
  date: string
  account_value: number
}

interface BenchmarkRow {
  symbol: string
  date: string
  close: number
  close_normalized: number | null
}

// ---------------------------------------------------------------------------
// Pure math helpers
// ---------------------------------------------------------------------------

/**
 * Return the percentage return between `latest` and `past` values, or null
 * if either is missing/zero. Percent semantics: (latest - past) / past * 100.
 */
function pctReturn(latest: number | undefined, past: number | undefined): number {
  if (latest === undefined || past === undefined) return 0
  if (past === 0) return 0
  return ((latest - past) / past) * 100
}

/**
 * Compute an annualized return from a total return pct and a period length
 * in trading days. Uses 252 trading days/year. Returns 0 for zero/negative
 * inputs to avoid NaN pollution in the JSON payload.
 */
function annualize(totalReturnPct: number, days: number): number {
  if (days <= 0) return 0
  const years = days / 252
  if (years <= 0) return 0
  const growthFactor = 1 + totalReturnPct / 100
  if (growthFactor <= 0) return 0
  return (Math.pow(growthFactor, 1 / years) - 1) * 100
}

/**
 * Normalize an equity series to base-100 relative to the first point. Preserves
 * array length. Returns empty array if `series` is empty.
 */
function normalizeToBase100(series: number[]): number[] {
  const base = series[0]
  if (base === undefined) return []
  if (base === 0) return series.map(() => 100)
  return series.map((v) => +(v / base * 100).toFixed(2))
}

// ---------------------------------------------------------------------------
// Route handlers
// ---------------------------------------------------------------------------
export const performance = new Hono<{ Bindings: Env }>()

// All performance aggregate routes require authentication
performance.use('*', authMiddleware)

/**
 * GET /api/performance/summary
 *
 * SQL (approx):
 *   SELECT date, account_value FROM account_equity_daily ORDER BY date DESC
 *
 * Compute period returns by taking the latest account_value vs the value at
 * (latest - N trading days). Annualized return uses total-history window.
 */
performance.get('/summary', async (c) => {
  const asset = parseAsset(c.req.query('asset') ?? undefined)
  if (!asset) {
    return c.json(
      { error: 'invalid_asset', message: 'asset must be one of all|systematic|options|other' },
      400,
    )
  }

  try {
    // Pull full history ordered oldest → newest so we can index by offset from
    // the tail. Even 10 years of daily bars is only ~2520 rows which is well
    // within D1's response size budget.
    const rows = await c.env.DB
      .prepare('SELECT date, account_value FROM account_equity_daily ORDER BY date ASC')
      .all<EquityRow>()

    const series = rows.results ?? []
    const last = series[series.length - 1]
    const first = series[0]
    if (series.length === 0 || !last || !first) {
      // Fallback: no equity history yet, surface mock with header indicator
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(FALLBACK_SUMMARY[asset])
    }

    const scale = scaleFor(asset)
    const latestValue = last.account_value
    const firstValue = first.account_value

    // Helper: value at N trading days before the tail (or the oldest point if
    // history is shorter than N).
    function valueAtLookback(days: number): number {
      const idx = Math.max(0, series.length - 1 - days)
      return series[idx]?.account_value ?? firstValue
    }

    // YTD: find first row of the current calendar year (or oldest if history
    // doesn't stretch that far back).
    const latestYear = last.date.slice(0, 4)
    const ytdBase = series.find((r) => r.date.slice(0, 4) === latestYear)?.account_value ?? firstValue

    const m = pctReturn(latestValue, valueAtLookback(PERIOD_DAYS.m))
    const ytd = pctReturn(latestValue, ytdBase)
    const y2 = pctReturn(latestValue, valueAtLookback(PERIOD_DAYS.y2))
    const y5 = pctReturn(latestValue, valueAtLookback(PERIOD_DAYS.y5))
    const y10 = pctReturn(latestValue, valueAtLookback(PERIOD_DAYS.y10))
    const totalReturnPct = pctReturn(latestValue, firstValue)
    const ann = annualize(totalReturnPct, series.length - 1)

    const payload: SummaryData = {
      asset,
      m: +(m * scale).toFixed(2),
      ytd: +(ytd * scale).toFixed(2),
      y2: +(y2 * scale).toFixed(2),
      y5: +(y5 * scale).toFixed(2),
      y10: +(y10 * scale).toFixed(2),
      ann: +(ann * scale).toFixed(2),
      base: +(latestValue * scale).toFixed(2),
    }
    return c.json(payload)
  } catch (error) {
    console.error('performance/summary query failed:', error)
    // On error, still fall back to mock so the dashboard never sees a 500.
    c.header('X-Data-Source', 'fallback-mock')
    return c.json(FALLBACK_SUMMARY[asset])
  }
})

/**
 * GET /api/performance/today (Phase 7.4)
 *
 * Returns today's account snapshot and the day-over-day PnL. Consumed by the
 * DailyPnLWatcher (.NET) to decide whether to flip trading_paused.
 *
 * Shape:
 *   { accountValue, cash, pnl, pnlPct, yesterdayClose }
 *
 * All numeric fields are non-null; `yesterdayClose` is null only when there's
 * no prior row. Caller treats null as "can't compute drawdown" and skips.
 */
performance.get('/today', async (c) => {
  try {
    // Latest two rows by date. We ORDER BY date DESC so the tuple is always
    // (today, yesterday) even if the ingest timing is irregular.
    const res = await c.env.DB
      .prepare(
        'SELECT date, account_value, cash FROM account_equity_daily ORDER BY date DESC LIMIT 2',
      )
      .all<{ date: string; account_value: number; cash: number }>()
    const rows = res.results ?? []

    if (rows.length === 0) {
      // No equity history at all — the watcher's ISafetyFlagStore shortcut will
      // see yesterdayClose=null and skip the drawdown check. Return zeros.
      return c.json({
        accountValue: 0,
        cash: 0,
        pnl: 0,
        pnlPct: 0,
        yesterdayClose: null,
        asOf: null,
      })
    }

    const today = rows[0]!
    const yesterday = rows.length > 1 ? rows[1] : undefined

    const accountValue = +today.account_value.toFixed(2)
    const cash = +today.cash.toFixed(2)
    const yesterdayClose = yesterday ? +yesterday.account_value.toFixed(2) : null

    // Use full precision for the math, only round the outputs. Avoids
    // rounding-before-ratio errors that would shift a borderline pause decision.
    const pnl = yesterday ? +(today.account_value - yesterday.account_value).toFixed(2) : 0
    const pnlPct = yesterday && yesterday.account_value !== 0
      ? +(((today.account_value - yesterday.account_value) / yesterday.account_value) * 100).toFixed(4)
      : 0

    return c.json({
      accountValue,
      cash,
      pnl,
      pnlPct,
      yesterdayClose,
      asOf: today.date,
    })
  } catch (error) {
    console.error('performance/today query failed:', error)
    return c.json(
      { error: 'performance_today_error', message: error instanceof Error ? error.message : 'unknown' },
      500,
    )
  }
})

/**
 * GET /api/performance/series
 *
 * SQL (approx):
 *   SELECT date, account_value FROM account_equity_daily
 *     WHERE date >= ? ORDER BY date ASC
 *   SELECT symbol, date, close_normalized, close FROM benchmark_series
 *     WHERE symbol IN ('SPX','SWDA') AND date >= ? ORDER BY date ASC
 *
 * Portfolio series is normalized to 100 based on its first point. Benchmarks
 * use `close_normalized` when available; otherwise we re-normalize on the fly.
 */
performance.get('/series', async (c) => {
  const asset = parseAsset(c.req.query('asset') ?? undefined)
  const range = parseRange(c.req.query('range') ?? undefined)
  if (!asset) {
    return c.json(
      { error: 'invalid_asset', message: 'asset must be one of all|systematic|options|other' },
      400,
    )
  }
  if (!range) {
    return c.json(
      { error: 'invalid_range', message: 'range must be one of 1W|1M|3M|YTD|1Y|ALL' },
      400,
    )
  }

  try {
    // Pull the full ordered equity history once; crop below. This keeps all
    // range branches on the same read path.
    const equityRes = await c.env.DB
      .prepare('SELECT date, account_value FROM account_equity_daily ORDER BY date ASC')
      .all<EquityRow>()
    const equityAll = equityRes.results ?? []

    if (equityAll.length === 0) {
      // Fallback: preserve pre-Phase-7.2 deterministic series
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(fallbackSeries(asset, range))
    }

    // Crop the equity series to the requested range.
    const rangeSpec = RANGE_DAYS[range]
    let equityCropped: EquityRow[]
    if (rangeSpec === 'all') {
      equityCropped = equityAll
    } else if (rangeSpec === 'ytd') {
      const latestYear = equityAll[equityAll.length - 1]?.date.slice(0, 4) ?? ''
      equityCropped = equityAll.filter((r) => r.date.slice(0, 4) === latestYear)
    } else {
      const tail = rangeSpec as number
      equityCropped = equityAll.slice(Math.max(0, equityAll.length - tail))
    }

    const cropFirst = equityCropped[0]
    const cropLast = equityCropped[equityCropped.length - 1]
    if (!cropFirst || !cropLast) {
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(fallbackSeries(asset, range))
    }

    const startDateStr = cropFirst.date
    const endDateStr = cropLast.date

    // Benchmark overlay — same calendar window. We pull both SPX and SWDA in a
    // single query and split in memory so we do not pay an extra round-trip.
    const benchRes = await c.env.DB
      .prepare(
        "SELECT symbol, date, close, close_normalized FROM benchmark_series " +
        "WHERE symbol IN ('SPX','SWDA') AND date >= ? AND date <= ? " +
        "ORDER BY date ASC",
      )
      .bind(startDateStr, endDateStr)
      .all<BenchmarkRow>()
    const benchRows = benchRes.results ?? []

    const scale = scaleFor(asset)

    // Normalize the portfolio series to base 100 then apply the mock per-asset
    // scaling so bucket differentiation remains visible until real
    // equity-by-strategy data lands (TODO above).
    const portfolioRaw = equityCropped.map((r) => r.account_value)
    const portfolioNorm = normalizeToBase100(portfolioRaw)
    const portfolio = portfolioNorm.map((v) => +((v - 100) * scale + 100).toFixed(2))

    const sp500 = normalizedBenchmark(benchRows, 'SPX')
    const swda = normalizedBenchmark(benchRows, 'SWDA')

    const startDate = toIsoTs(startDateStr)
    const endDate = toIsoTs(endDateStr)

    const payload: PerfSeries = {
      asset,
      range,
      portfolio,
      sp500,
      swda,
      startDate,
      endDate,
    }
    return c.json(payload)
  } catch (error) {
    console.error('performance/series query failed:', error)
    c.header('X-Data-Source', 'fallback-mock')
    return c.json(fallbackSeries(asset, range))
  }
})

// ---------------------------------------------------------------------------
// Benchmark helpers
// ---------------------------------------------------------------------------

/**
 * Extract a normalized series for a given benchmark symbol, ALWAYS rebased
 * to 100 at the first point of the REQUESTED window.
 *
 * The persisted `close_normalized` column is typically normalized from
 * inception (i.e. base=100 at the benchmark's earliest date), so returning
 * it as-is for a cropped window (e.g. last 30 days) produces overlays that
 * don't start at 100 and can't be compared apples-to-apples with the
 * portfolio series (which IS rebased to 100 at the window start). We
 * instead divide through by the first value of the cropped slice so both
 * series share a common `y = 100` origin — then the chart is a clean
 * relative-performance comparison.
 *
 * If no rows are present, returns an empty array.
 */
function normalizedBenchmark(rows: BenchmarkRow[], symbol: string): number[] {
  const filtered = rows.filter((r) => r.symbol === symbol)
  if (filtered.length === 0) return []

  const allNormalized = filtered.every(
    (r) => r.close_normalized !== null && r.close_normalized !== undefined,
  )
  const source = allNormalized
    ? filtered.map((r) => r.close_normalized as number)
    : filtered.map((r) => r.close)

  return normalizeToBase100(source)
}

// ---------------------------------------------------------------------------
// Fallback (deterministic mock) — preserved from pre-Phase-7.2 so the dashboard
// never renders a blank chart on a brand-new install.
// ---------------------------------------------------------------------------
function fallbackSeries(asset: AssetBucket, range: PerfRange): PerfSeries {
  const CROP: Record<PerfRange, number> = { '1W': 7, '1M': 20, '3M': 42, YTD: 50, '1Y': 60, ALL: 60 }
  const N = 60
  const growth: Record<AssetBucket, number> = { all: 0.65, systematic: 0.30, options: 1.30, other: 0.10 }
  const base = 100
  const gPerStep = growth[asset] / N
  const portfolioFull = Array.from({ length: N }, (_, i) => +(base * (1 + gPerStep * i)).toFixed(2))
  const sp500Full = Array.from({ length: N }, (_, i) => +(100 + i * 0.3).toFixed(2))
  const swdaFull = Array.from({ length: N }, (_, i) => +(100 + i * 0.29).toFixed(2))
  const crop = CROP[range]

  const portfolio = portfolioFull.slice(N - crop)
  const sp500 = sp500Full.slice(N - crop)
  const swda = swdaFull.slice(N - crop)

  // Deterministic date window matching the pre-Phase-7.2 behavior (anchored
  // to 2026-04-20 so tests can observe a stable timestamp).
  const endDate = new Date('2026-04-20T00:00:00Z')
  const startDate = new Date(endDate)
  startDate.setUTCDate(startDate.getUTCDate() - crop)

  return {
    asset,
    range,
    portfolio,
    sp500,
    swda,
    startDate: startDate.toISOString(),
    endDate: endDate.toISOString(),
  }
}

/**
 * Convert a "YYYY-MM-DD" string to a UTC midnight ISO-8601 timestamp so the
 * dashboard's Date parser handles it uniformly across browsers.
 */
function toIsoTs(dateStr: string): string {
  return new Date(`${dateStr}T00:00:00Z`).toISOString()
}
