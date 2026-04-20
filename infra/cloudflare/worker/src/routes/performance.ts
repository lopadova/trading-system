/**
 * Performance API Routes
 * Returns portfolio performance summaries and time-series data for the dashboard.
 *
 * NOTE: Phase 3 returns deterministic mock data. Real D1-backed aggregation is
 * out of scope here and will be wired in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { AssetBucket, PerfRange, SummaryData, PerfSeries } from '../types/api'

// ---------------------------------------------------------------------------
// Allowed query-parameter values
// ---------------------------------------------------------------------------
const ASSETS: AssetBucket[] = ['all', 'systematic', 'options', 'other']
const RANGES: PerfRange[] = ['1W', '1M', '3M', 'YTD', '1Y', 'ALL']

// ---------------------------------------------------------------------------
// Mock summary data per asset bucket
// ---------------------------------------------------------------------------
const SUMMARY: Record<AssetBucket, SummaryData> = {
  all:        { asset: 'all',        m: 14.30, ytd: 15.04, y2: 49.88, y5: 100.13, y10: 98.59,  ann: 13.07, base: 125430 },
  systematic: { asset: 'systematic', m:  8.20, ytd:  9.64, y2: 31.40, y5:  74.80, y10: 82.10,  ann: 11.22, base:  72500 },
  options:    { asset: 'options',    m: 22.80, ytd: 24.10, y2: 68.10, y5: 128.40, y10: 118.30, ann: 15.31, base:  38900 },
  other:      { asset: 'other',      m:  3.10, ytd:  4.28, y2: 12.60, y5:  28.90, y10: 42.40,  ann:  5.64, base:  14030 },
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
// Deterministic synthetic series per asset bucket (60 points total)
// The caller crops by range using CROP[]
// ---------------------------------------------------------------------------
function generateSeries(asset: AssetBucket): number[] {
  const N = 60
  const growth: Record<AssetBucket, number> = { all: 0.65, systematic: 0.30, options: 1.30, other: 0.10 }
  const base = 100
  const gPerStep = growth[asset] / N
  return Array.from({ length: N }, (_, i) => +(base * (1 + gPerStep * i)).toFixed(2))
}

const SP500 = Array.from({ length: 60 }, (_, i) => +(100 + i * 0.3).toFixed(2))
const SWDA = Array.from({ length: 60 }, (_, i) => +(100 + i * 0.29).toFixed(2))

const CROP: Record<PerfRange, number> = { '1W': 7, '1M': 20, '3M': 42, YTD: 50, '1Y': 60, ALL: 60 }

// ---------------------------------------------------------------------------
// Route handlers
// ---------------------------------------------------------------------------
export const performance = new Hono<{ Bindings: Env }>()

performance.get('/summary', (c) => {
  const asset = parseAsset(c.req.query('asset') ?? undefined)
  if (!asset) return c.json({ error: 'invalid_asset', message: 'asset must be one of all|systematic|options|other' }, 400)
  return c.json(SUMMARY[asset])
})

performance.get('/series', (c) => {
  const asset = parseAsset(c.req.query('asset') ?? undefined)
  const range = parseRange(c.req.query('range') ?? undefined)
  if (!asset) return c.json({ error: 'invalid_asset', message: 'asset must be one of all|systematic|options|other' }, 400)
  if (!range) return c.json({ error: 'invalid_range', message: 'range must be one of 1W|1M|3M|YTD|1Y|ALL' }, 400)

  const portfolioFull = generateSeries(asset)
  const crop = CROP[range]
  const N = portfolioFull.length

  // Crop the tail — most recent `crop` points
  const portfolio = portfolioFull.slice(N - crop)
  const sp500 = SP500.slice(N - crop)
  const swda = SWDA.slice(N - crop)

  // Date window: today minus crop days
  const endDate = new Date('2026-04-20T00:00:00Z')
  const startDate = new Date(endDate)
  startDate.setUTCDate(startDate.getUTCDate() - crop)

  const payload: PerfSeries = {
    asset,
    range,
    portfolio,
    sp500,
    swda,
    startDate: startDate.toISOString(),
    endDate: endDate.toISOString(),
  }
  return c.json(payload)
})
