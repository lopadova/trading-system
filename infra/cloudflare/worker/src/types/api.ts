/**
 * Shared API Types
 * Used by dashboard-facing Cloudflare Worker endpoints.
 */

export type AssetBucket = 'all' | 'systematic' | 'options' | 'other'
export type PerfRange = '1W' | '1M' | '3M' | 'YTD' | '1Y' | 'ALL'
export type DrawdownRange = 'Max' | '10Y' | '5Y' | '1Y' | 'YTD' | '6M'

export interface SummaryData {
  asset: AssetBucket
  m: number
  ytd: number
  y2: number
  y5: number
  y10: number
  ann: number
  base: number
}

export interface PerfSeries {
  asset: AssetBucket
  range: PerfRange
  portfolio: number[]
  sp500: number[]
  swda: number[]
  startDate: string
  endDate: string
}

export interface WorstDrawdown {
  depthPct: number
  start: string
  end: string
  months: number
}

export interface DrawdownsResponse {
  asset: AssetBucket
  range: DrawdownRange
  portfolioSeries: number[]
  sp500Series: number[]
  worst: WorstDrawdown[]
}

export interface MonthlyReturnsResponse {
  asset: AssetBucket
  years: Record<string, (number | null)[]>
  totals: Record<string, number>
}

export interface RiskMetrics {
  vix: number | null
  vix1d: number | null
  delta: number
  theta: number
  vega: number
  ivRankSpy: number | null
  buyingPower: number
  marginUsedPct: number
}

/**
 * Options Trading Semaphore — composite indicator for SPX options operator.
 * Theory (from Lorenzo):
 *   - regime:            SPX vs 200-day MA
 *   - vix_level:         VIX percentile over last 252 trading days
 *   - vix_rolling_yield: 30d rolling yield of VIX curve (VIX vs VIX3M)
 *   - ivts:              VIX / VIX3M ratio (backwardation signal)
 *   - overall:           aggregate status derived from the 4 indicators
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
  // The current reading. Number when purely numeric, string when categorical
  // (e.g. "SPX > MA200").
  value: number | string
  // Human-readable explanation (e.g. "0.92 (contango)")
  detail: string
}

/**
 * Live quote row — mirrors the per-symbol display in the reference image:
 * price + absolute change + percent change (e.g. SPX 6767.54 +62.42 +0.93%).
 */
export interface SemaphoreQuote {
  price: number
  change: number
  changePct: number
}

export type SemaphoreRegime = 'BULLISH' | 'BEARISH'

export interface SemaphoreData {
  // 0 = safe/green, 100 = dangerous/red. Computed as a weighted sum of risk
  // contributions from the four sub-indicators.
  score: number
  status: SemaphoreStatus
  // Always 5 items in the order [regime, vix_level, vix_rolling_yield, ivts, overall]
  indicators: SemaphoreIndicator[]
  asOf: string
  // Extra display fields used by the dashboard gauge card (reference layout):
  // SPX / VIX quotes, long-term regime label, and a pre-formatted Exchange Time
  // (America/New_York) string.
  spx: SemaphoreQuote
  vix: SemaphoreQuote
  regime: SemaphoreRegime
  exchangeTime: string
}

export interface SystemMetricsSample {
  cpu: number[]
  ram: number[]
  network: number[]
  diskUsedPct: number
  diskFreeGb: number
  diskTotalGb: number
  asOf: string
}

export interface ExposureSegment {
  label: string
  value: number
  color: string
}

export interface PositionsBreakdownResponse {
  byStrategy: ExposureSegment[]
  byAsset: ExposureSegment[]
}

export type ActivityIcon =
  | 'check-circle-2'
  | 'alert-triangle'
  | 'play'
  | 'x-circle'
  | 'repeat'
  | 'trending-up'
  | 'refresh-cw'
  | 'file-text'

export type ActivityTone = 'green' | 'red' | 'yellow' | 'blue' | 'purple' | 'muted'

export interface ActivityEvent {
  id: string
  icon: ActivityIcon
  tone: ActivityTone
  title: string
  subtitle: string
  timestamp: string
}

export interface ActivityResponse {
  events: ActivityEvent[]
}

export interface AlertsSummary {
  total: number
  critical: number
  warning: number
  info: number
}

export interface CampaignsSummary {
  active: number
  paused: number
  draft: number
  detail: string
}
