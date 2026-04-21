import type { Page, Route } from '@playwright/test'

/**
 * Canned JSON responses for the dashboard's Worker-backed queries so E2E
 * specs can run without a live Cloudflare Worker. Every field present on
 * the real API is represented here — if a route shape drifts on the backend,
 * the corresponding contract test (tests/Contract/) will fail first and
 * flag the fixture for update.
 *
 * Phase 7.6 — see docs/ops/RUNBOOK.md Playbook 8 (Test regressions) for the
 * procedure when a Playwright E2E starts failing after an API change.
 */

type JsonValue = string | number | boolean | null | JsonValue[] | { [k: string]: JsonValue }

const SEMAPHORE_GREEN: JsonValue = {
  score: 22,
  status: 'green',
  regime: 'BULLISH',
  asOf: '2026-04-20T14:30:00Z',
  exchangeTime: '14:30:00 NYT',
  spx: { price: 5410.25, change: 12.18, changePct: 0.226 },
  vix: { price: 14.22, change: -0.34, changePct: -2.335 },
  indicators: [
    { id: 'regime', label: 'Long-term regime', status: 'green', value: 'BULLISH', detail: 'SPX > 200d MA' },
    { id: 'vix_level', label: 'VIX level', status: 'green', value: 14.22, detail: 'below 20 threshold' },
    { id: 'vix_rolling_yield', label: 'VIX rolling yield', status: 'green', value: 0.92, detail: 'contango, curve healthy' },
    { id: 'ivts', label: 'IV term structure', status: 'green', value: 1.05, detail: 'above 0.95 floor' },
    { id: 'overall', label: 'Overall', status: 'green', value: 22, detail: 'OPERATIVE' },
  ],
}

const SUMMARY: JsonValue = {
  asset: 'all',
  m: 2.1,
  ytd: 12.4,
  y2: 18.8,
  y5: 55.3,
  y10: 140.1,
  ann: 9.2,
  base: 123456.78,
}

const CAMPAIGNS_SUMMARY: JsonValue = {
  active: 3,
  paused: 1,
  detail: '3 running · 1 paused',
}

const POSITIONS: JsonValue = {
  positions: [
    {
      positionId: 'p-1',
      contractSymbol: 'SPX   250321P05000000',
      quantity: 2,
      costBasisAvg: 15.5,
      currentPrice: 14.1,
      unrealizedPnl: -280,
      realizedPnl: 0,
      openedAt: '2026-04-15T10:00:00Z',
      status: 'open',
    },
  ],
}

const ALERTS_SUMMARY: JsonValue = { critical: 0, warnings: 1, info: 3 }

const RISK_METRICS: JsonValue = {
  vix: 14.22,
  vix1d: null,
  vix3m: 15.1,
  delta: 0.31,
  theta: -48.2,
  vega: 12.8,
  ivRankSpy: 32,
  buyingPower: 98000,
  marginUsedPct: 12,
}

const SYSTEM_METRICS: JsonValue = {
  services: [
    { serviceName: 'TradingSupervisorService', lastSeenAt: '2026-04-20T14:30:00Z', cpuPercent: 4.2, ramPercent: 18.1, uptimeSeconds: 86400 },
    { serviceName: 'OptionsExecutionService', lastSeenAt: '2026-04-20T14:30:00Z', cpuPercent: 3.8, ramPercent: 16.7, uptimeSeconds: 86400 },
  ],
}

// Everything else (activity, breakdown, etc.) — return an empty-ish shape so
// the widgets render their zero state without throwing.
const EMPTY_LIST: JsonValue = { items: [] }

/**
 * Table mapping URL fragments → response JSON. Longer / more-specific
 * fragments are matched first so "/risk/semaphore" does not collide with
 * "/risk/metrics".
 */
const MOCK_TABLE: Array<{ match: (url: string) => boolean; body: JsonValue }> = [
  { match: u => u.includes('/risk/semaphore'), body: SEMAPHORE_GREEN },
  { match: u => u.includes('/risk/metrics'), body: RISK_METRICS },
  { match: u => u.includes('/performance/summary'), body: SUMMARY },
  { match: u => u.includes('/performance/series'), body: { series: [] } },
  { match: u => u.includes('/performance/today'), body: { points: [] } },
  { match: u => u.includes('/campaigns/summary'), body: CAMPAIGNS_SUMMARY },
  { match: u => u.includes('/system/metrics'), body: SYSTEM_METRICS },
  { match: u => u.includes('/alerts/summary-24h'), body: ALERTS_SUMMARY },
  { match: u => u.includes('/alerts/unresolved'), body: { alerts: [] } },
  { match: u => u.includes('/positions'), body: POSITIONS },
  { match: u => u.includes('/activity/recent'), body: EMPTY_LIST },
  { match: u => u.includes('/drawdowns'), body: { drawdowns: [] } },
  { match: u => u.includes('/monthly-returns'), body: { months: [] } },
  { match: u => u.includes('/heartbeats'), body: { heartbeats: [] } },
  { match: u => u.includes('/audit/orders'), body: { orders: [] } },
  { match: u => u.includes('/breakdown'), body: EMPTY_LIST },
]

/**
 * Attach a route handler that intercepts every call going to the Worker API
 * base (ky + VITE_API_URL default in api.ts) and returns deterministic JSON.
 * Unknown endpoints fall through with a 204 No Content so React Query's
 * error boundary never trips — the intent is that unmocked widgets render
 * their empty/loading state rather than a crashed panel.
 */
export async function installApiMocks(page: Page): Promise<void> {
  await page.route(/\/api\//, async (route: Route) => {
    const url = route.request().url()
    const entry = MOCK_TABLE.find(m => m.match(url))
    if (!entry) {
      await route.fulfill({ status: 204, body: '' })
      return
    }
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(entry.body),
    })
  })
}
