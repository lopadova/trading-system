/**
 * Monthly Returns API Route
 * Returns a year x month matrix of % returns plus yearly totals.
 *
 * NOTE: Phase 3 returns deterministic mock data. Real D1-backed aggregation is
 * out of scope here and will be wired in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { AssetBucket, MonthlyReturnsResponse } from '../types/api'
import { authMiddleware } from '../middleware/auth'

// ---------------------------------------------------------------------------
// Static mock matrix: 12 entries per year, nulls for months outside reporting
// window (either pre-inception or future months).
// ---------------------------------------------------------------------------
const MATRIX: Record<string, (number | null)[]> = {
  '2026': [0.77, 0.53, -0.65, 14.30, null, null, null, null, null, null, null, null],
  '2025': [3.15, 1.57, 2.49, 1.54, 4.98, 0.78, 2.45, 2.09, 0.76, -6.68, 6.12, 2.07],
  '2024': [0.12, 0.76, 0.30, 0.25, 2.08, 0.87, 0.05, 0.19, 2.56, -0.55, 0.37, 0.34],
  '2023': [1.68, 2.65, 0.44, 0.55, 0.56, 0.79, 1.02, 0.68, 0.37, 0.49, -5.55, -2.51],
  '2022': [2.22, 0.60, 0.62, 0.94, 1.05, 2.43, 1.86, 1.70, 1.53, 0.30, 1.00, 1.23],
  '2021': [-3.92, 1.05, 4.32, 7.16, 0.46, 0.10, 3.49, 3.94, 0.40, 0.97, 0.63, 1.38],
  '2020': [null, null, -8.57, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00, 0.00],
}

const TOTALS: Record<string, number> = {
  '2026': 15.04,
  '2025': 22.88,
  '2024': 7.55,
  '2023': 0.92,
  '2022': 16.60,
  '2021': 21.42,
  '2020': -8.57,
}

// Asset-specific scaling so buckets look distinct
function scaleFor(asset: AssetBucket): number {
  if (asset === 'systematic') return 0.6
  if (asset === 'options') return 1.5
  if (asset === 'other') return 0.25
  return 1
}

const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']

// ---------------------------------------------------------------------------
// Route handler
// ---------------------------------------------------------------------------
export const monthlyReturns = new Hono<{ Bindings: Env }>()

// All monthly-returns routes require authentication
monthlyReturns.use('*', authMiddleware)

monthlyReturns.get('/', (c) => {
  const asset = (c.req.query('asset') ?? 'all') as AssetBucket
  if (!ASSETS.includes(asset)) {
    return c.json({ error: 'invalid_asset', message: 'asset must be one of all|systematic|options|other' }, 400)
  }

  const s = scaleFor(asset)
  const years: MonthlyReturnsResponse['years'] = {}
  const totals: MonthlyReturnsResponse['totals'] = {}

  for (const [year, arr] of Object.entries(MATRIX)) {
    years[year] = arr.map((v) => (v === null ? null : +(v * s).toFixed(2)))
    // TOTALS is keyed by the same years as MATRIX — default to 0 if a future
    // year slipped into MATRIX without a TOTALS entry (belt-and-braces).
    const raw = TOTALS[year] ?? 0
    totals[year] = +(raw * s).toFixed(2)
  }

  const payload: MonthlyReturnsResponse = { asset, years, totals }
  return c.json(payload)
})
