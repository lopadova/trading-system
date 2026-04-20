/**
 * Risk API Route
 * Returns the aggregated risk snapshot (Greeks + implied-vol + margin) and
 * the composite Options Trading Semaphore indicator.
 *
 * NOTE: Phase 3 returns deterministic mock data. Real D1/market-data wiring is
 * out of scope here and will be handled in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type {
  RiskMetrics,
  SemaphoreData,
  SemaphoreIndicator,
  SemaphoreRegime,
  SemaphoreStatus,
} from '../types/api'
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

/**
 * GET /api/risk/semaphore
 * Composite indicator telling the operator whether it is safe to sell PUTs on
 * SPX. Returns 4 sub-indicators + an overall status + a 0-100 risk score.
 *
 * Phase 3 returns deterministic mock values. Real wiring to SPX close series,
 * VIX history and VIX3M will be handled in a later phase.
 */
risk.get('/semaphore', (c) => {
  // TODO Phase 7: wire to real market data (SPX close series, VIX history, VIX3M).
  const payload: SemaphoreData = computeSemaphore()
  return c.json(payload)
})

/**
 * Pure computation of the semaphore payload from raw market values.
 *
 * Theory (Lorenzo):
 *  - Indicator 1 — Market Regime: SPX above 200-day MA is bullish baseline.
 *  - Indicator 2 — VIX level: current percentile over last 252 trading days.
 *                  Above 80th pct → RED.
 *  - Indicator 3 — VIX 30d Rolling Yield: re-entry only when > 50th pct (median).
 *  - Indicator 4 — IVTS = VIX / VIX3M:
 *                  > 1.15  → RED   (backwardation, dangerous)
 *                  > 1.00  → ORANGE (mild backwardation)
 *                  < 1.00  → GREEN  (contango, normal)
 *  - Indicator 5 — Overall aggregation:
 *                  any RED among 2/3/4 → overall RED
 *                  regime RED + any ORANGE among 2/3/4 → overall RED
 *                  all GREEN → GREEN
 *                  otherwise → ORANGE
 *
 * Score mapping (0-100, higher = more dangerous):
 *    regime bearish                    → +20
 *    vix percentile above 80th         → +30
 *    rolling yield below 50th pct      → +20
 *    ivts 1.00-1.15                    → +25
 *    ivts > 1.15                       → +50   (dominant)
 */
export function computeSemaphore(): SemaphoreData {
  // ---- Raw mock readings (plausible GREEN scenario) ----
  // SPX + VIX quotes chosen to match the reference screenshot so the UI can be
  // evaluated against the design spec. Real-time wiring comes in a later phase.
  const spxPrice = 6767.54
  const spxChange = 62.42
  const spxChangePct = 0.93
  const spxMa200 = 6350 // below current price → bullish regime

  const vixPrice = 18.56
  const vixChange = -1.96
  const vixChangePct = -9.55
  const vix1yP80 = 24.5 // 80th percentile of VIX over last 252 trading days
  const vixPercentile = 42 // current VIX percentile vs 1Y (0-100)
  const rollingYield = 0.62 // 30d normalized curve slope
  const rollingYieldPercentile = 72 // where current yield sits vs last year
  const vix3m = 20.05
  const ivts = vixPrice / vix3m // ≈ 0.926

  // ---- Indicator 1 — Market Regime ----
  const regimeBullish = spxPrice > spxMa200
  const regimeLabel: SemaphoreRegime = regimeBullish ? 'BULLISH' : 'BEARISH'
  const regime: SemaphoreIndicator = {
    id: 'regime',
    label: 'Market Regime',
    status: regimeBullish ? 'green' : 'red',
    value: regimeBullish ? 'SPX > MA200' : 'SPX < MA200',
    detail: regimeBullish
      ? `SPX ${spxPrice.toFixed(2)} above 200-day MA ${spxMa200.toFixed(2)}`
      : `SPX ${spxPrice.toFixed(2)} below 200-day MA ${spxMa200.toFixed(2)}`,
  }

  // ---- Indicator 2 — VIX Level vs 1Y history ----
  // RED if above 80th percentile (pre-emptive stop).
  const vixAbove80thPct = vixPrice > vix1yP80
  const vix_level: SemaphoreIndicator = {
    id: 'vix_level',
    label: 'VIX Level (1Y pct)',
    status: vixAbove80thPct ? 'red' : 'green',
    value: vixPrice,
    detail: `${vixPrice.toFixed(2)} (${vixPercentile}th pct, 1Y)`,
  }

  // ---- Indicator 3 — VIX 30d Rolling Yield ----
  // Re-entry threshold: only GREEN when yield > median (50th pct).
  const rollingYieldAboveMedian = rollingYieldPercentile > 50
  const vix_rolling_yield: SemaphoreIndicator = {
    id: 'vix_rolling_yield',
    label: 'VIX Rolling Yield (30d)',
    status: rollingYieldAboveMedian ? 'green' : 'orange',
    value: rollingYield,
    detail: `${rollingYield.toFixed(2)} (${rollingYieldPercentile}th pct, 1Y)`,
  }

  // ---- Indicator 4 — IVTS = VIX / VIX3M ----
  // > 1.15 RED (backwardation), > 1.00 ORANGE, else GREEN (contango).
  const ivtsStatus: SemaphoreStatus =
    ivts > 1.15 ? 'red' : ivts > 1.0 ? 'orange' : 'green'
  const ivtsLabel =
    ivts > 1.15
      ? 'strong backwardation'
      : ivts > 1.0
        ? 'mild backwardation'
        : 'contango'
  const ivtsIndicator: SemaphoreIndicator = {
    id: 'ivts',
    label: 'IVTS (VIX / VIX3M)',
    status: ivtsStatus,
    value: Number(ivts.toFixed(3)),
    detail: `${ivts.toFixed(2)} (${ivtsLabel})`,
  }

  // ---- Indicator 5 — Overall ----
  // Rule of thumb:
  //   - any RED among 2/3/4 → overall RED
  //   - regime RED + any ORANGE among 2/3/4 → overall RED
  //   - all GREEN → GREEN
  //   - otherwise → ORANGE
  const marketStatuses: SemaphoreStatus[] = [
    vix_level.status,
    vix_rolling_yield.status,
    ivtsIndicator.status,
  ]
  const anyRed = marketStatuses.some(s => s === 'red')
  const anyOrange = marketStatuses.some(s => s === 'orange')
  const allGreen =
    regime.status === 'green' && marketStatuses.every(s => s === 'green')

  let overallStatus: SemaphoreStatus
  if (anyRed) {
    overallStatus = 'red'
  } else if (regime.status === 'red' && anyOrange) {
    overallStatus = 'red'
  } else if (allGreen) {
    overallStatus = 'green'
  } else {
    overallStatus = 'orange'
  }

  // ---- Score (0-100, higher = more dangerous) ----
  let score = 0
  if (regime.status === 'red') score += 20
  if (vixAbove80thPct) score += 30
  if (!rollingYieldAboveMedian) score += 20
  if (ivts > 1.15) {
    score += 50
  } else if (ivts > 1.0) {
    score += 25
  }
  // Clamp to [0, 100] — in pathological cases the sum could exceed 100.
  if (score > 100) score = 100
  if (score < 0) score = 0

  const overall: SemaphoreIndicator = {
    id: 'overall',
    label: 'Operatività',
    status: overallStatus,
    value: score,
    detail:
      overallStatus === 'green'
        ? 'Safe to sell PUTs on SPX'
        : overallStatus === 'orange'
          ? 'Caution — hold existing, no new entries'
          : 'Stop operativity — consider closing positions',
  }

  // Pre-format an Exchange Time string in America/New_York (ET) so the UI can
  // display it without pulling in a tz library. Falls back to UTC-ISO if the
  // runtime does not have Intl timezone support.
  const nowUtc = new Date()
  const exchangeTime = formatExchangeTime(nowUtc)

  return {
    score,
    status: overallStatus,
    indicators: [regime, vix_level, vix_rolling_yield, ivtsIndicator, overall],
    asOf: nowUtc.toISOString(),
    spx: {
      price: spxPrice,
      change: spxChange,
      changePct: spxChangePct,
    },
    vix: {
      price: vixPrice,
      change: vixChange,
      changePct: vixChangePct,
    },
    regime: regimeLabel,
    exchangeTime,
  }
}

/**
 * Format a UTC Date as "DD-MMM-YYYY HH:MM:SS ET" using America/New_York
 * timezone. Workers runtime supports Intl.DateTimeFormat with timeZone option.
 */
function formatExchangeTime(d: Date): string {
  try {
    // Use en-GB to get day-first ordering. Produces e.g. "25 Nov 2025, 15:45:52".
    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: 'America/New_York',
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    }).formatToParts(d)

    const lookup: Record<string, string> = {}
    for (const p of parts) {
      if (p.type !== 'literal') lookup[p.type] = p.value
    }
    const date = `${lookup.day}-${lookup.month}-${lookup.year}`
    const time = `${lookup.hour}:${lookup.minute}:${lookup.second}`
    return `${date} ${time} ET`
  } catch {
    // Fallback if Intl timezone fails: return UTC ISO-ish string.
    return `${d.toISOString().replace('T', ' ').slice(0, 19)} UTC`
  }
}
