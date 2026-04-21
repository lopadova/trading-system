/**
 * Monthly Returns API Route
 * Returns a year x month matrix of % returns plus yearly totals.
 *
 * Phase 7.2 — Replaces the static MATRIX mock with a real SQL aggregation
 * over `account_equity_daily`. Algorithm:
 *
 *   1. Per month (YYYY-MM), find the first and last account_value in the
 *      month. Use SQL window functions + ROW_NUMBER() to get both sides.
 *   2. Monthly return = (last - first) / first * 100.
 *   3. Group by year → 12-element array keyed by month index 0..11.
 *   4. Yearly total = compound product of non-null monthly returns in that
 *      year.
 *
 * Response shape preserved; fallback to mock when the table is empty.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { AssetBucket, MonthlyReturnsResponse } from '../types/api'
import { authMiddleware } from '../middleware/auth'

const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']

// TODO(Phase 7.x): replace with real per-asset-bucket decomposition.
function scaleFor(asset: AssetBucket): number {
  if (asset === 'systematic') return 0.6
  if (asset === 'options') return 1.5
  if (asset === 'other') return 0.25
  return 1
}

// ---------------------------------------------------------------------------
// D1 row shape produced by the monthly aggregation SQL
// ---------------------------------------------------------------------------
interface MonthlyRow {
  ym: string              // 'YYYY-MM'
  first_value: number
  last_value: number
}

// ---------------------------------------------------------------------------
// Fallback mock (pre-Phase-7.2)
// ---------------------------------------------------------------------------
const FALLBACK_MATRIX: Record<string, (number | null)[]> = {
  '2026': [0.77, 0.53, -0.65, 14.30, null, null, null, null, null, null, null, null],
  '2025': [3.15, 1.57, 2.49, 1.54, 4.98, 0.78, 2.45, 2.09, 0.76, -6.68, 6.12, 2.07],
  '2024': [0.12, 0.76, 0.30, 0.25, 2.08, 0.87, 0.05, 0.19, 2.56, -0.55, 0.37, 0.34],
  '2023': [1.68, 2.65, 0.44, 0.55, 0.56, 0.79, 1.02, 0.68, 0.37, 0.49, -5.55, -2.51],
  '2022': [2.22, 0.60, 0.62, 0.94, 1.05, 2.43, 1.86, 1.70, 1.53, 0.30, 1.00, 1.23],
  '2021': [-3.92, 1.05, 4.32, 7.16, 0.46, 0.10, 3.49, 3.94, 0.40, 0.97, 0.63, 1.38],
  '2020': [null, null, -8.57, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00],
}

const FALLBACK_TOTALS: Record<string, number> = {
  '2026': 15.04,
  '2025': 22.88,
  '2024': 7.55,
  '2023': 0.92,
  '2022': 16.60,
  '2021': 21.42,
  '2020': -8.57,
}

// ---------------------------------------------------------------------------
// Route handler
// ---------------------------------------------------------------------------
export const monthlyReturns = new Hono<{ Bindings: Env }>()

// All monthly-returns routes require authentication
monthlyReturns.use('*', authMiddleware)

monthlyReturns.get('/', async (c) => {
  const asset = (c.req.query('asset') ?? 'all') as AssetBucket
  if (!ASSETS.includes(asset)) {
    return c.json({ error: 'invalid_asset', message: 'asset must be one of all|systematic|options|other' }, 400)
  }

  try {
    // Compute first/last account_value per year-month. We use two passes with
    // ROW_NUMBER() — one ordered ascending (first), one descending (last) —
    // then pair them by (ym, rn=1).
    //
    // SQL decomposed:
    //   ranked_asc:  ROW_NUMBER ASC per ym → rn=1 is first value of month
    //   ranked_desc: ROW_NUMBER DESC per ym → rn=1 is last value of month
    //   final:       INNER JOIN on ym to produce (ym, first_value, last_value)
    const sql =
      "WITH ranked AS (" +
      "  SELECT strftime('%Y-%m', date) AS ym, date, account_value, " +
      "         ROW_NUMBER() OVER (PARTITION BY strftime('%Y-%m', date) ORDER BY date ASC)  AS rn_asc, " +
      "         ROW_NUMBER() OVER (PARTITION BY strftime('%Y-%m', date) ORDER BY date DESC) AS rn_desc " +
      "  FROM account_equity_daily" +
      ") " +
      "SELECT a.ym AS ym, a.account_value AS first_value, b.account_value AS last_value " +
      "FROM ranked a JOIN ranked b ON a.ym = b.ym " +
      "WHERE a.rn_asc = 1 AND b.rn_desc = 1 " +
      "ORDER BY a.ym ASC"

    const res = await c.env.DB.prepare(sql).all<MonthlyRow>()
    const rows = res.results ?? []

    if (rows.length === 0) {
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(fallbackPayload(asset))
    }

    const scale = scaleFor(asset)
    const years: Record<string, (number | null)[]> = {}

    // Initialize year buckets with 12 nulls, then fill indices for months
    // that have data. The matrix is dense (12 entries per year always).
    for (const row of rows) {
      const parts = row.ym.split('-')
      const year = parts[0] ?? ''
      const monthStr = parts[1] ?? ''
      if (!year || !monthStr) continue
      const mIdx = parseInt(monthStr, 10) - 1
      if (mIdx < 0 || mIdx > 11) continue

      let bucket = years[year]
      if (!bucket) {
        bucket = Array.from({ length: 12 }, () => null as number | null)
        years[year] = bucket
      }

      if (row.first_value === 0) continue // guard against div-by-zero
      const rawReturn = ((row.last_value - row.first_value) / row.first_value) * 100
      bucket[mIdx] = +(rawReturn * scale).toFixed(2)
    }

    // Compound each year's monthly returns for the "totals" column.
    const totals: Record<string, number> = {}
    for (const [year, months] of Object.entries(years)) {
      let factor = 1
      for (const r of months) {
        if (r === null) continue
        factor *= 1 + r / 100
      }
      totals[year] = +((factor - 1) * 100).toFixed(2)
    }

    const payload: MonthlyReturnsResponse = { asset, years, totals }
    return c.json(payload)
  } catch (error) {
    console.error('monthly-returns query failed:', error)
    c.header('X-Data-Source', 'fallback-mock')
    return c.json(fallbackPayload(asset))
  }
})

// ---------------------------------------------------------------------------
// Fallback payload (pre-Phase-7.2 mock, per-asset scaled)
// ---------------------------------------------------------------------------
function fallbackPayload(asset: AssetBucket): MonthlyReturnsResponse {
  const scale = scaleFor(asset)
  const years: MonthlyReturnsResponse['years'] = {}
  const totals: MonthlyReturnsResponse['totals'] = {}

  for (const [year, arr] of Object.entries(FALLBACK_MATRIX)) {
    years[year] = arr.map((v) => (v === null ? null : +(v * scale).toFixed(2)))
    const raw = FALLBACK_TOTALS[year] ?? 0
    totals[year] = +(raw * scale).toFixed(2)
  }

  return { asset, years, totals }
}
