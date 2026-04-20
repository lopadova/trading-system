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
