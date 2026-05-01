// Risk metrics types for the RiskMetricsCard widget

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
