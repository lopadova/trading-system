/**
 * Risk API Route
 * Returns the aggregated risk snapshot (Greeks + implied-vol + margin).
 *
 * NOTE: Phase 3 returns deterministic mock data. Real D1/market-data wiring is
 * out of scope here and will be handled in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { RiskMetrics } from '../types/api'
import { authMiddleware } from '../middleware/auth'

export const risk = new Hono<{ Bindings: Env }>()

// All risk routes require authentication
risk.use('*', authMiddleware)

risk.get('/metrics', (c) => {
  const payload: RiskMetrics = {
    vix: 15.84,
    vix1d: 11.20,
    delta: 42.3,
    theta: -58.20,
    vega: 124.5,
    ivRankSpy: 0.34,
    buyingPower: 48200,
    marginUsedPct: 61,
  }
  return c.json(payload)
})
