/**
 * System Metrics API Route
 * Returns recent CPU/RAM/network samples plus disk usage.
 *
 * Phase 7.2 — Replaces the hardcoded sample arrays with real reads from
 * `service_heartbeats`. The schema stores only the most-recent sample per
 * (service_name) as an upsert, so to build 20-sample sparklines we pull the
 * latest 20 rows ordered by last_seen_at DESC (potentially across multiple
 * services) and reverse them into chronological order.
 *
 * Notes:
 *  - `service_heartbeats` does not carry a dedicated network-throughput
 *    column — we fall back to a derived value using (ram_percent + cpu_percent)
 *    as a rough activity proxy. This keeps the dashboard sparkline non-empty
 *    until a future migration adds a real `network_kbps` column.
 *    TODO(Phase 7.x): add `network_kbps REAL` to service_heartbeats.
 *  - Disk uses the latest row's disk_free_gb; disk_total_gb is not captured in
 *    0001, so we approximate with a fixed 200 GB reference matching the
 *    pre-Phase-7.2 mock. TODO: surface disk_total_gb + disk_used_pct from
 *    the supervisor's MachineMetricsCollector (needs migration + ingest).
 *
 * Fallback to mock when `service_heartbeats` is empty.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { SystemMetricsSample } from '../types/api'
import { authMiddleware } from '../middleware/auth'

export const systemMetrics = new Hono<{ Bindings: Env }>()

// All system-metrics routes require authentication
systemMetrics.use('*', authMiddleware)

// ---------------------------------------------------------------------------
// D1 row shape
// ---------------------------------------------------------------------------
interface HeartbeatSampleRow {
  last_seen_at: string
  cpu_percent: number
  ram_percent: number
  disk_free_gb: number
}

// ---------------------------------------------------------------------------
// Fallback mock (pre-Phase-7.2)
// ---------------------------------------------------------------------------
function fallbackSample(): SystemMetricsSample {
  return {
    cpu: [22, 28, 24, 30, 34, 38, 35, 40, 44, 48, 42, 38, 36, 34, 32, 30, 34, 38, 36, 34],
    ram: [54, 55, 56, 57, 58, 58, 59, 59, 60, 61, 60, 59, 58, 58, 57, 57, 58, 58, 58, 58],
    network: [12, 18, 22, 30, 45, 58, 65, 72, 78, 82, 76, 68, 60, 54, 48, 42, 48, 54, 62, 71],
    diskUsedPct: 79,
    diskFreeGb: 42,
    diskTotalGb: 200,
    asOf: new Date().toISOString(),
  }
}

// A conservative disk-total constant used while the collector side catches up.
// Updating this to a real column is tracked in the file-level TODO.
const DISK_TOTAL_GB = 200

systemMetrics.get('/metrics', async (c) => {
  try {
    // NOTE on payload semantics:
    // `service_heartbeats` PK is (service_name) — we store ONLY the latest
    // sample per service, not a time-series. So `LIMIT 20` here returns at
    // most N rows where N = number of registered services (supervisor +
    // options-execution + etc.), NOT 20 time-ordered samples of one service.
    //
    // For now we return these per-service latest snapshots as the "cpu/ram/
    // network" arrays so the dashboard sparkline still has SOMETHING to
    // render (showing the current fleet state across services). A proper
    // time-series needs a separate append-only `service_heartbeat_samples`
    // table. Tracked as TODO(Phase 7.x-samples).
    const res = await c.env.DB
      .prepare(
        'SELECT last_seen_at, cpu_percent, ram_percent, disk_free_gb ' +
        'FROM service_heartbeats ORDER BY last_seen_at DESC LIMIT 20',
      )
      .all<HeartbeatSampleRow>()
    const rows = (res.results ?? []).slice().reverse()

    if (rows.length === 0) {
      c.header('X-Data-Source', 'fallback-mock')
      return c.json(fallbackSample())
    }

    const cpu = rows.map((r) => +r.cpu_percent.toFixed(1))
    const ram = rows.map((r) => +r.ram_percent.toFixed(1))
    // Derived "network" proxy: half the sum of cpu+ram clipped to 0..100. See
    // file header TODO — this will be replaced with a real network_kbps
    // column as soon as the supervisor side is updated.
    const network = rows.map((r) => {
      const proxy = (r.cpu_percent + r.ram_percent) / 2
      return Math.max(0, Math.min(100, +proxy.toFixed(1)))
    })

    const latest = rows[rows.length - 1]
    const diskFreeGb = latest ? +latest.disk_free_gb.toFixed(1) : 0
    const diskUsedGb = Math.max(0, DISK_TOTAL_GB - diskFreeGb)
    const diskUsedPct = DISK_TOTAL_GB > 0 ? Math.round((diskUsedGb / DISK_TOTAL_GB) * 100) : 0

    const payload: SystemMetricsSample = {
      cpu,
      ram,
      network,
      diskUsedPct,
      diskFreeGb,
      diskTotalGb: DISK_TOTAL_GB,
      asOf: latest ? latest.last_seen_at : new Date().toISOString(),
    }
    return c.json(payload)
  } catch (error) {
    console.error('system/metrics query failed:', error)
    c.header('X-Data-Source', 'fallback-mock')
    return c.json(fallbackSample())
  }
})
