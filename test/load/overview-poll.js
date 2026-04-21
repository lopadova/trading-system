/*
 * Phase 7.6 — k6 load test for the Overview page poll fan-out.
 *
 * Scenario: 100 concurrent VUs polling the 4 endpoints that the Overview
 * page calls on its 30s refresh interval. Each VU:
 *   1. Hits all 4 endpoints in parallel (mirrors the dashboard's real
 *      React-Query behaviour).
 *   2. Sleeps 30s.
 *   3. Repeats for the duration of the run.
 *
 * Thresholds:
 *   - p(95) HTTP duration < 200ms for 2xx responses. The Cloudflare edge
 *     SLA is lower but we pad for D1 cold-start jitter + our own margins.
 *   - HTTP failure rate < 1%. D1 is flaky under burst; anything > 1%
 *     means the Worker is mis-routing or the auth cache is flapping.
 *
 * Run locally:
 *   k6 run \
 *     -e WORKER_URL=https://trading-system.example.workers.dev \
 *     -e API_KEY=... \
 *     test/load/overview-poll.js
 *
 * See docs/ops/LOAD_TESTING.md for full instructions and baseline numbers.
 */

import http from 'k6/http'
import { check, sleep, group } from 'k6'
import { Counter } from 'k6/metrics'

// ----------------------------------------------------------------------------
// Configuration (via k6 env flags)
// ----------------------------------------------------------------------------
const WORKER_URL = (__ENV.WORKER_URL || 'http://localhost:8787').replace(/\/+$/, '')
const API_KEY = __ENV.API_KEY || ''
const POLL_INTERVAL_SEC = Number(__ENV.POLL_INTERVAL_SEC || 30)
const VUS = Number(__ENV.VUS || 100)
const DURATION = __ENV.DURATION || '10m'

// Custom metric so we can see per-endpoint volume in the summary.
const endpointHits = new Counter('endpoint_hits')

export const options = {
  // Constant VU scenario is the right shape for "steady state poller"
  // behaviour. A ramp-up would stress-test the edge but not the steady
  // state we actually care about for the Overview page.
  scenarios: {
    overview_poll: {
      executor: 'constant-vus',
      vus: VUS,
      duration: DURATION,
      gracefulStop: '30s',
    },
  },
  thresholds: {
    // Success-only latency: focus on the happy path. Failures are bounded
    // separately by the http_req_failed threshold.
    'http_req_duration{expected_response:true}': ['p(95)<200'],
    // 1% failure ceiling. Above that means something's actually broken.
    http_req_failed: ['rate<0.01'],
  },
  // Keep the summary terse; we read the JSON output elsewhere.
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
}

// ----------------------------------------------------------------------------
// Endpoints that the Overview page calls
// ----------------------------------------------------------------------------
// Note: the real Worker routes are `/api/campaigns/summary` and
// `/api/system/metrics`. The task brief listed hyphenated aliases; we map
// them to the canonical paths here.
const ENDPOINTS = [
  { name: 'performance_summary', path: '/api/performance/summary?asset=all' },
  { name: 'risk_metrics', path: '/api/risk/metrics' },
  { name: 'system_metrics', path: '/api/system/metrics' },
  { name: 'campaigns_summary', path: '/api/campaigns/summary' },
]

function commonHeaders() {
  const h = {
    Accept: 'application/json',
    'User-Agent': 'k6/overview-poll (trading-system phase7.6)',
  }
  if (API_KEY) {
    h['X-Api-Key'] = API_KEY
  }
  return h
}

// ----------------------------------------------------------------------------
// VU iteration
// ----------------------------------------------------------------------------
export default function () {
  // Fire all 4 requests in parallel — this is what the browser does via
  // React Query's independent queries. http.batch keeps the load profile
  // realistic (one TCP / HTTP-2 connection pool per VU, not four).
  const requests = ENDPOINTS.map(ep => ({
    method: 'GET',
    url: `${WORKER_URL}${ep.path}`,
    params: { headers: commonHeaders(), tags: { endpoint: ep.name } },
  }))

  group('overview_poll_cycle', () => {
    const responses = http.batch(requests)

    responses.forEach((res, i) => {
      const ep = ENDPOINTS[i]
      endpointHits.add(1, { endpoint: ep.name })

      check(res, {
        [`${ep.name}: status is 200`]: r => r.status === 200,
        [`${ep.name}: duration < 500ms`]: r => r.timings.duration < 500,
        [`${ep.name}: has body`]: r => r.body && r.body.length > 0,
      })
    })
  })

  sleep(POLL_INTERVAL_SEC)
}

// ----------------------------------------------------------------------------
// Summary hook — write a compact JSON next to stdout so LOAD_TESTING.md can
// point operators at it. k6 also accepts --summary-export if you want the
// raw metrics; this summary is the operator-friendly version.
// ----------------------------------------------------------------------------
export function handleSummary(data) {
  const p95 = data.metrics['http_req_duration{expected_response:true}']?.values?.['p(95)']
  const failRate = data.metrics.http_req_failed?.values?.rate
  const hits = data.metrics.endpoint_hits?.values?.count ?? 0

  const lines = [
    '',
    '================ Overview-poll k6 run ================',
    `  VUs                  : ${VUS}`,
    `  Duration             : ${DURATION}`,
    `  Poll interval        : ${POLL_INTERVAL_SEC}s`,
    `  Total endpoint hits  : ${hits}`,
    `  p(95) duration (2xx) : ${p95 ? p95.toFixed(1) + 'ms' : 'n/a'}`,
    `  http_req_failed rate : ${failRate != null ? (failRate * 100).toFixed(3) + '%' : 'n/a'}`,
    '  (thresholds: p(95) < 200ms, fail < 1%)',
    '======================================================',
    '',
  ].join('\n')

  return {
    stdout: lines,
    // Also spit out the full raw k6 JSON for CI post-processing if anyone
    // wants to diff baselines later.
    'summary.json': JSON.stringify(data, null, 2),
  }
}
