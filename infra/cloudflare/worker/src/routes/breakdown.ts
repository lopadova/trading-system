/**
 * Positions Breakdown API Route
 * Returns exposure segments split by strategy and by asset class.
 *
 * Phase 7.2 — Replaces the hardcoded segment arrays with real D1 aggregation
 * over `active_positions`. Two groupings are produced:
 *
 *   byStrategy: GROUP BY strategy_name, summing quantity * price per row
 *   byAsset:    same rows classified into an asset bucket via the
 *               strategy-name → asset-bucket heuristic (see STRATEGY_TO_ASSET)
 *
 * `active_positions` has no dedicated `market_value` or `multiplier` column
 * in the 0001 schema, so exposure is computed with
 *     ABS(quantity * COALESCE(current_price, entry_price))
 * This gives a stable dollar-value proxy that matches what the Positions
 * table already surfaces to the dashboard.
 *
 * TODO(Phase 7.x): add a `market_value` or `multiplier` column to
 * active_positions so this route does not have to re-derive per-row
 * exposure on every request.
 *
 * Fallback to pre-Phase-7.2 mock when the table is empty.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { PositionsBreakdownResponse, ExposureSegment } from '../types/api'
import { authMiddleware } from '../middleware/auth'

export const breakdown = new Hono<{ Bindings: Env }>()

// All breakdown routes require authentication
breakdown.use('*', authMiddleware)

// ---------------------------------------------------------------------------
// Color palette — stable hex codes the dashboard currently expects. Centralize
// here so future label additions keep a single source of truth.
// ---------------------------------------------------------------------------
const COLOR = {
  blue:   '#2f81f7', // Iron Condor / Systematic
  purple: '#a371f7', // Put Spread / Options
  green:  '#3fb950', // Call Spread / Other
  yellow: '#d29922', // Long Call
  red:    '#f85149', // Short Strangle
  muted:  '#8b949e', // Catch-all fallback color
} as const

// Map strategy_name (case-insensitive substring match) to the color used by
// the donut segments. Ordered for stable color assignment when new labels
// appear in the future.
const STRATEGY_COLORS: { match: RegExp; color: string }[] = [
  { match: /iron\s*condor/i, color: COLOR.blue },
  { match: /put\s*spread/i,  color: COLOR.purple },
  { match: /call\s*spread/i, color: COLOR.green },
  { match: /long\s*call/i,   color: COLOR.yellow },
  { match: /short\s*strangle|strangle/i, color: COLOR.red },
]

function colorForStrategy(label: string): string {
  for (const entry of STRATEGY_COLORS) {
    if (entry.match.test(label)) return entry.color
  }
  return COLOR.muted
}

// ---------------------------------------------------------------------------
// Strategy → asset bucket classifier. Until the supervisor surfaces an
// explicit `asset_bucket` column on active_positions, we classify by
// strategy name. Options-family strategies land in Options; systematic
// rules-engine strategies in Systematic; everything else (incl. manual
// trades) in Other.
// ---------------------------------------------------------------------------
function assetBucketOf(strategy: string): 'Options' | 'Systematic' | 'Other' {
  const s = strategy.toLowerCase()
  if (/\b(call|put|strangle|straddle|condor|spread|option)\b/.test(s)) return 'Options'
  if (/\b(systematic|trend|momentum|mean[\s-]?reversion|breakout|rules?)\b/.test(s)) return 'Systematic'
  return 'Other'
}

const ASSET_COLORS: Record<'Options' | 'Systematic' | 'Other', string> = {
  Options: COLOR.purple,
  Systematic: COLOR.blue,
  Other: COLOR.green,
}

// ---------------------------------------------------------------------------
// D1 row shape
// ---------------------------------------------------------------------------
interface AggRow {
  strategy_name: string
  value: number
}

// ---------------------------------------------------------------------------
// Fallback mock (pre-Phase-7.2)
// ---------------------------------------------------------------------------
function fallbackPayload(): PositionsBreakdownResponse {
  return {
    byStrategy: [
      { label: 'Iron Condor',    value: 18400, color: COLOR.blue },
      { label: 'Put Spread',     value: 12200, color: COLOR.purple },
      { label: 'Call Spread',    value:  6800, color: COLOR.green },
      { label: 'Long Call',      value:  3400, color: COLOR.yellow },
      { label: 'Short Strangle', value:  2100, color: COLOR.red },
    ],
    byAsset: [
      { label: 'Options',    value: 28900, color: COLOR.purple },
      { label: 'Systematic', value: 10200, color: COLOR.blue },
      { label: 'Other',      value:  3800, color: COLOR.green },
    ],
  }
}

breakdown.get('/', async (c) => {
  try {
    // Aggregate exposure per strategy_name. We compute exposure per row as
    // |quantity * COALESCE(current_price, entry_price)| since neither a
    // dedicated market_value nor multiplier lives on the table yet.
    const res = await c.env.DB
      .prepare(
        "SELECT strategy_name, " +
        "       SUM(ABS(quantity * COALESCE(current_price, entry_price))) AS value " +
        "FROM active_positions " +
        "GROUP BY strategy_name " +
        "ORDER BY value DESC",
      )
      .all<AggRow>()
    const rows = res.results ?? []

    if (rows.length === 0) {
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(fallbackPayload())
    }

    const byStrategy: ExposureSegment[] = rows.map((r) => ({
      label: r.strategy_name,
      value: +Number(r.value ?? 0).toFixed(2),
      color: colorForStrategy(r.strategy_name),
    }))

    // Bucket aggregation: sum byStrategy values into Options/Systematic/Other.
    const bucketSums: Record<string, number> = { Options: 0, Systematic: 0, Other: 0 }
    for (const seg of byStrategy) {
      const bucket = assetBucketOf(seg.label)
      bucketSums[bucket] = (bucketSums[bucket] ?? 0) + seg.value
    }

    const byAsset: ExposureSegment[] = (['Options', 'Systematic', 'Other'] as const)
      .filter((key) => (bucketSums[key] ?? 0) > 0)
      .map((key) => ({
        label: key,
        value: +(bucketSums[key] ?? 0).toFixed(2),
        color: ASSET_COLORS[key],
      }))

    // In the (extremely unlikely) edge case where all rows have zero quantity
    // and zero price, both byStrategy and byAsset end up all-zero — still
    // surface them so the dashboard knows there are positions but no exposure,
    // rather than dropping into the fallback path.
    const payload: PositionsBreakdownResponse = { byStrategy, byAsset }
    return c.json(payload)
  } catch (error) {
    console.error('breakdown query failed:', error)
    c.header('X-Data-Source', 'fallback-mock')
    return c.json(fallbackPayload())
  }
})
