/**
 * System Metrics API Route
 * Returns recent CPU/RAM/network samples plus disk usage.
 *
 * NOTE: Phase 3 returns deterministic mock data. Real telemetry wiring is
 * out of scope and will be handled in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { SystemMetricsSample } from '../types/api'

export const systemMetrics = new Hono<{ Bindings: Env }>()

systemMetrics.get('/metrics', (c) => {
  const payload: SystemMetricsSample = {
    cpu: [22, 28, 24, 30, 34, 38, 35, 40, 44, 48, 42, 38, 36, 34, 32, 30, 34, 38, 36, 34],
    ram: [54, 55, 56, 57, 58, 58, 59, 59, 60, 61, 60, 59, 58, 58, 57, 57, 58, 58, 58, 58],
    network: [12, 18, 22, 30, 45, 58, 65, 72, 78, 82, 76, 68, 60, 54, 48, 42, 48, 54, 62, 71],
    diskUsedPct: 79,
    diskFreeGb: 42,
    diskTotalGb: 200,
    asOf: new Date().toISOString(),
  }
  return c.json(payload)
})
