// Drawdown data types for the DrawdownsSection widget

import type { AssetBucket } from './performance'

export type DrawdownRange = 'Max' | '10Y' | '5Y' | '1Y' | 'YTD' | '6M'

export interface WorstDrawdown {
  depthPct: number
  start: string
  end: string
  months: number
}

export interface DrawdownsData {
  asset: AssetBucket
  range: DrawdownRange
  portfolioSeries: number[]
  sp500Series: number[]
  worst: WorstDrawdown[]
}
