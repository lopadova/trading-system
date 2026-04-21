/**
 * Analytics Engine metrics helper (Phase 7.3).
 *
 * Fire-and-forget counter / tag emission backed by Cloudflare Analytics
 * Engine. Each call writes exactly one data point with:
 *   - `indexes[0]` = metric name (used for fast filtering in Cloudflare UI)
 *   - `blobs`      = [name, ...tagValues] (human-readable labels for UI)
 *   - `doubles`    = [1] (counter increment)
 *
 * Design rationale:
 *   - The Analytics Engine binding is *optional*. Local `wrangler dev` without
 *     it must not throw; we just no-op. Same for test suites.
 *   - All writes are wrapped in try/catch. A metric write failing MUST NEVER
 *     block a response path or surface as an error to the client.
 *   - Tags are a flat Record<string,string> for readability at the call site.
 *     We sort keys to make label order deterministic across invocations.
 *
 * Usage:
 *   import { recordMetric } from '../lib/metrics'
 *   recordMetric(c.env, 'ingest.event_type', { type: 'heartbeat', status: 'accepted' })
 */

import type { Env } from '../types/env'

/**
 * Emit a single Analytics Engine data point. Silently no-ops when the METRICS
 * binding is absent (local dev, early deployment before wrangler.toml sync).
 *
 * @param env   Worker environment (optionally contains METRICS)
 * @param name  Metric name, e.g. 'ingest.event_type', 'auth.failure'
 * @param tags  Key-value labels. Serialized as blobs for dashboard grouping.
 */
export function recordMetric(
  env: Pick<Env, 'METRICS'>,
  name: string,
  tags: Record<string, string> = {}
): void {
  const dataset = env.METRICS
  if (!dataset) return

  try {
    // Sort keys so the blobs array is deterministic across calls, which makes
    // downstream queries (ORDER BY blob2, blob3) predictable.
    const sortedKeys = Object.keys(tags).sort()
    const tagValues = sortedKeys.map((k) => tags[k] ?? '')

    dataset.writeDataPoint({
      blobs: [name, ...tagValues],
      indexes: [name],
      doubles: [1]
    })
  } catch (error) {
    // Analytics Engine failure is non-fatal by design. Log once; never re-throw.
    console.error('[METRICS] writeDataPoint failed:', error)
  }
}
