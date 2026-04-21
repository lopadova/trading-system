// Performance-related shared types for dashboard widgets

export type AssetBucket = 'all' | 'systematic' | 'options' | 'other'
export type PerfRange = '1W' | '1M' | '3M' | 'YTD' | '1Y' | 'ALL'

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
