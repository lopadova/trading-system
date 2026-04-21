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
  // VIX3M — 3-month VIX constant maturity. Used together with VIX to compute
  // the IVTS ratio shown in the Options Trading Semaphore.
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

// ============================================================================
// Phase 7.1 — Ingest payload shapes
// ============================================================================
// These mirror the Zod schemas in src/routes/ingest.ts and are used both as
// contract documentation for the .NET supervisor side AND as the input shape
// for dashboard-facing queries in Phase 7.2. Each type corresponds to a new
// table introduced in migration 0007_market_data.sql.

/**
 * Daily account equity snapshot (drives performance + drawdown charts).
 * Primary key: date (one row per calendar day).
 */
export interface AccountEquityPayload {
  date: string              // ISO date (YYYY-MM-DD)
  account_value: number
  cash: number
  buying_power: number
  margin_used: number
  margin_used_pct: number
}

/**
 * Generic daily OHLCV per symbol (SPX, VIX, SPY, SWDA, etc.).
 * Primary key: (symbol, date).
 */
export interface MarketQuotePayload {
  symbol: string
  date: string              // ISO date (YYYY-MM-DD)
  open?: number | null
  high?: number | null
  low?: number | null
  close: number
  volume?: number | null
}

/**
 * VIX curve snapshot for a given date. The ingest handler also denormalizes
 * each non-null leg into market_quotes_daily so the chart endpoints have a
 * single source of truth.
 * Primary key: date.
 */
export interface VixSnapshotPayload {
  date: string              // ISO date (YYYY-MM-DD)
  vix?: number | null
  vix1d?: number | null
  vix3m?: number | null
  vix6m?: number | null
}

/**
 * Pre-normalized benchmark close (base-100 from normalize_base_date).
 * Primary key: (symbol, date).
 */
export interface BenchmarkClosePayload {
  symbol: string
  date: string              // ISO date (YYYY-MM-DD)
  close: number
  close_normalized?: number | null
}

/**
 * Per-position Greeks snapshot (rolling time-series).
 * Primary key: (position_id, snapshot_ts).
 */
export interface PositionGreeksPayload {
  position_id: string
  snapshot_ts: string       // ISO 8601 timestamp
  delta?: number | null
  gamma?: number | null
  theta?: number | null
  vega?: number | null
  iv?: number | null
  underlying_price?: number | null
}

export type MarketDataEventType =
  | 'account_equity'
  | 'market_quote'
  | 'vix_snapshot'
  | 'benchmark_close'
  | 'position_greeks'
