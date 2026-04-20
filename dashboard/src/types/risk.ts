// Risk metrics types for the RiskMetricsCard widget and the Options Trading
// Semaphore widget. Mirrors the shape defined in the Cloudflare Worker
// (infra/cloudflare/worker/src/types/api.ts).

export interface RiskMetrics {
  vix: number | null
  vix1d: number | null
  vix3m: number | null
  delta: number
  theta: number
  vega: number
  ivRankSpy: number | null
  buyingPower: number
  marginUsedPct: number
}

/**
 * Options Trading Semaphore — composite indicator for SPX options operator.
 */
export type SemaphoreStatus = 'green' | 'orange' | 'red'

export type SemaphoreIndicatorId =
  | 'regime'
  | 'vix_level'
  | 'vix_rolling_yield'
  | 'ivts'
  | 'overall'

export interface SemaphoreIndicator {
  id: SemaphoreIndicatorId
  label: string
  status: SemaphoreStatus
  value: number | string
  detail: string
}

export interface SemaphoreQuote {
  price: number
  change: number
  changePct: number
}

export type SemaphoreRegime = 'BULLISH' | 'BEARISH'

export interface SemaphoreData {
  // 0 = safe/green, 100 = dangerous/red.
  score: number
  status: SemaphoreStatus
  // Always 5 items in the order [regime, vix_level, vix_rolling_yield, ivts, overall]
  indicators: SemaphoreIndicator[]
  asOf: string
  spx: SemaphoreQuote
  vix: SemaphoreQuote
  regime: SemaphoreRegime
  exchangeTime: string
}
