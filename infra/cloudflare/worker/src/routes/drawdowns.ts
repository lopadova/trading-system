/**
 * Drawdowns API Route
 * Returns underwater-curve series and the worst historical drawdowns.
 *
 * NOTE: Phase 3 returns deterministic mock data. Real D1-backed aggregation is
 * out of scope here and will be wired in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type {
  AssetBucket,
  DrawdownRange,
  DrawdownsResponse,
  WorstDrawdown,
} from '../types/api'

// ---------------------------------------------------------------------------
// Mock series — 80 points, negative values represent drawdown %
// ---------------------------------------------------------------------------
const PORT = [
  0, -1.2, -0.8, -2.1, -4.5, -3.2, -1.8, -0.6, 0, 0,
  -0.5, -1.2, -2.1, -1.4, -0.8, 0, -0.3, -1.1, -3.2, -5.8,
  -8.9, -12.4, -15.8, -18.2, -19.9, -18.5, -16.2, -13.1, -10.4, -7.2,
  -4.8, -2.9, -1.5, -0.6, 0, 0, -0.4, -1.2, -2.8, -3.1,
  -2.4, -1.6, -0.8, 0, -0.3, -1.1, -0.7, -0.2, 0, 0,
  -0.4, -0.9, -1.6, -2.4, -3.1, -3.8, -4.4, -5.1, -5.9, -6.6,
  -7.2, -6.8, -6.2, -5.4, -4.6, -3.8, -3.0, -2.3, -1.6, -1.0,
  -0.5, -0.2, 0, 0, -0.3, -0.5, -0.8, -0.4, -0.1, 0,
]

const SP = [
  0, -0.8, -0.4, -1.1, -2.3, -1.5, -0.8, -0.2, 0, 0,
  -0.6, -1.8, -3.2, -2.1, -1.3, 0, -0.5, -1.8, -5.4, -9.8,
  -14.2, -18.6, -21.3, -23.8, -25.1, -22.4, -18.8, -14.9, -11.2, -7.8,
  -5.2, -3.1, -1.8, -0.8, 0, 0, -0.6, -1.9, -4.2, -4.8,
  -3.6, -2.4, -1.2, 0, -0.5, -1.8, -1.1, -0.3, 0, 0,
  -0.7, -1.6, -2.8, -4.2, -5.6, -6.8, -8.1, -9.4, -10.8, -12.1,
  -13.2, -12.4, -11.2, -9.6, -8.1, -6.8, -5.4, -4.1, -2.8, -1.7,
  -0.8, -0.3, 0, 0, -0.5, -0.9, -1.4, -0.7, -0.2, 0,
]

// ---------------------------------------------------------------------------
// Range crops (number of tail points to include)
// ---------------------------------------------------------------------------
const CROP: Record<DrawdownRange, number> = {
  Max: 80,
  '10Y': 80,
  '5Y': 60,
  '1Y': 12,
  YTD: 4,
  '6M': 6,
}

const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']
const RANGES: DrawdownRange[] = ['Max', '10Y', '5Y', '1Y', 'YTD', '6M']

// Asset-specific scaling so buckets have distinct-looking curves
function scaleFor(asset: AssetBucket): number {
  if (asset === 'systematic') return 0.7
  if (asset === 'options') return 1.4
  if (asset === 'other') return 0.3
  return 1
}

const WORST: WorstDrawdown[] = [
  { depthPct: -7.92, start: 'Nov 2023', end: 'Jan 2025', months: 14 },
  { depthPct: -6.68, start: 'Oct 2025', end: 'Dec 2025', months: 2 },
  { depthPct: -3.92, start: 'Jan 2021', end: 'Mar 2021', months: 2 },
  { depthPct: -0.65, start: 'Mar 2026', end: 'Apr 2026', months: 1 },
]

// ---------------------------------------------------------------------------
// Route handler
// ---------------------------------------------------------------------------
export const drawdowns = new Hono<{ Bindings: Env }>()

drawdowns.get('/', (c) => {
  const asset = (c.req.query('asset') ?? 'all') as AssetBucket
  const range = (c.req.query('range') ?? '10Y') as DrawdownRange

  if (!ASSETS.includes(asset)) {
    return c.json({ error: 'invalid_asset', message: 'asset must be one of all|systematic|options|other' }, 400)
  }
  if (!RANGES.includes(range)) {
    return c.json({ error: 'invalid_range', message: 'range must be one of Max|10Y|5Y|1Y|YTD|6M' }, 400)
  }

  const crop = CROP[range]
  const s = scaleFor(asset)

  const payload: DrawdownsResponse = {
    asset,
    range,
    portfolioSeries: PORT.slice(-crop).map((v) => +(v * s).toFixed(2)),
    sp500Series: SP.slice(-crop),
    worst: WORST.map((w) => ({ ...w, depthPct: +(w.depthPct * s).toFixed(2) })),
  }
  return c.json(payload)
})
