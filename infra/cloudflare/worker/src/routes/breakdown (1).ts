/**
 * Positions Breakdown API Route
 * Returns exposure segments split by strategy and by asset class.
 *
 * NOTE: Phase 3 returns deterministic mock data. Real D1 aggregation of
 * `active_positions` is out of scope and will be handled in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { PositionsBreakdownResponse } from '../types/api'

export const breakdown = new Hono<{ Bindings: Env }>()

breakdown.get('/', (c) => {
  const payload: PositionsBreakdownResponse = {
    byStrategy: [
      { label: 'Iron Condor',    value: 18400, color: '#2f81f7' },
      { label: 'Put Spread',     value: 12200, color: '#a371f7' },
      { label: 'Call Spread',    value:  6800, color: '#3fb950' },
      { label: 'Long Call',      value:  3400, color: '#d29922' },
      { label: 'Short Strangle', value:  2100, color: '#f85149' },
    ],
    byAsset: [
      { label: 'Options',    value: 28900, color: '#a371f7' },
      { label: 'Systematic', value: 10200, color: '#2f81f7' },
      { label: 'Other',      value:  3800, color: '#3fb950' },
    ],
  }
  return c.json(payload)
})
