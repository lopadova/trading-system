/**
 * Bot Data Types
 * TypeScript interfaces for query results
 */

/**
 * Portfolio query result
 */
export interface PortfolioData {
  pnlToday: number | null
  pnlMTD: number | null
  pnlYTD: number | null
  winRate: number | null
  activeCampaigns: Array<{
    name: string
    daysElapsed: number
    pnl: number
    status: string
  }>
  ivts: number | null
  ivtsState: string | null
  spx: number | null
  vix: number | null
  vix3m: number | null
  timestamp: string
}

/**
 * Service status result
 */
export interface StatusData {
  services: Array<{
    name: string
    status: 'running' | 'stopped' | 'unknown'
    lastHeartbeat: string | null
    ageMinutes: number | null
  }>
  timestamp: string
}

/**
 * Risk metrics result
 */
export interface RiskData {
  campaigns: Array<{
    name: string
    pnl: number | null
    stop: number | null
    delta: number | null
    theta: number | null
    spxCurrent: number | null
    wingLower: number | null
    wingUpper: number | null
    daysElapsed: number | null
    maxDays: number | null
  }>
  timestamp: string
}

/**
 * Market data result
 */
export interface MarketData {
  spx: number | null
  spxChange: number | null
  vix: number | null
  vix3m: number | null
  ivts: number | null
  ivtsState: string | null
  ivtsSparkline: number[] // Last 30 days
  timestamp: string
}

/**
 * Alert log result
 */
export interface AlertsData {
  alerts: Array<{
    severity: 'info' | 'warning' | 'critical'
    message: string
    createdAt: string
    resolved: boolean
  }>
  timestamp: string
}

/**
 * Campaigns list result
 */
export interface CampaignsData {
  campaigns: Array<{
    id: string
    name: string
    status: string
    positionsCount: number
    pnl: number | null
    daysElapsed: number
  }>
  timestamp: string
}

/**
 * Strategies list result
 */
export interface StrategiesData {
  strategies: Array<{
    name: string
    status: string
    lastSignal: string | null
  }>
  timestamp: string
}
