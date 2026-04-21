/**
 * Web Vitals reporter (Phase 7.3 observability).
 *
 * Subscribes to the five Core Web Vitals metrics from the `web-vitals` library
 * and ships each sample to the Worker `/api/v1/ingest` endpoint as an event of
 * type `web_vitals`. Failures are silent — vitals are advisory telemetry, not
 * critical user-facing data, so a 500 from the Worker MUST NOT spam the
 * browser console or break the app.
 *
 * Session correlation: we mint a random session id once per tab (kept in
 * sessionStorage) so all metrics from the same browsing session share an id.
 * This lets the Worker later correlate metrics with the same user journey.
 */

import { onCLS, onINP, onLCP, onFCP, onTTFB, type Metric } from 'web-vitals'
import { api } from './api'

const SESSION_KEY = 'trading-dashboard-session-id'

function getSessionId(): string {
  // sessionStorage is per-tab, so closing + reopening the tab starts a new
  // session — which matches how we want to analyse vitals (per-visit).
  try {
    const existing = sessionStorage.getItem(SESSION_KEY)
    if (existing) return existing
    const fresh = crypto.randomUUID()
    sessionStorage.setItem(SESSION_KEY, fresh)
    return fresh
  } catch {
    // sessionStorage can be blocked (e.g. strict privacy mode). Fall back to a
    // per-call random id — the Worker tolerates duplicates because PK is
    // (session_id, name, timestamp) and the timestamp changes each dispatch.
    return crypto.randomUUID()
  }
}

function reportMetric(metric: Metric): void {
  const sessionId = getSessionId()
  const payload = {
    session_id: sessionId,
    name: metric.name,
    value: metric.value,
    rating: metric.rating,
    navigationType: metric.navigationType,
    id: metric.id,
    timestamp: new Date().toISOString()
  }

  // Fire-and-forget POST. We explicitly do NOT await so the page is never
  // blocked by vitals telemetry. Errors are caught + swallowed.
  void api
    .post('v1/ingest', {
      json: {
        event_id: `wv-${sessionId}-${metric.id}`,
        event_type: 'web_vitals',
        payload
      }
    })
    .catch(() => {
      // Intentionally silent — vitals telemetry must never surface as a UX issue.
    })
}

/**
 * Call once at app bootstrap (main.tsx) to start streaming web vitals to the
 * Worker. Safe to call on every render — the web-vitals library guards against
 * duplicate subscriptions internally.
 */
export function reportWebVitals(): void {
  onCLS(reportMetric)
  onINP(reportMetric)
  onLCP(reportMetric)
  onFCP(reportMetric)
  onTTFB(reportMetric)
}
