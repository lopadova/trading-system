// Position types for the Trading System Dashboard

export type PositionStatus = 'open' | 'closed' | 'pending'
export type PositionSide = 'long' | 'short'
export type PositionType = 'option' | 'stock' | 'future'

export interface Greeks {
  delta: number
  gamma: number
  theta: number
  vega: number
  iv?: number // Implied Volatility
}

export interface Position {
  id: string
  symbol: string
  underlying?: string // For options
  strategy?: string
  type: PositionType
  side: PositionSide
  status: PositionStatus
  quantity: number
  entryPrice: number
  currentPrice: number
  marketValue: number
  costBasis: number
  unrealizedPnl: number
  unrealizedPnlPct: number
  realizedPnl?: number
  avgCost: number
  openDate: string
  closeDate?: string
  greeks?: Greeks
  strike?: number // For options
  expiration?: string // For options
  right?: 'call' | 'put' // For options
  lastUpdate: string
  // Worker enrichment — set when the position is part of an active campaign
  campaign: string | null
  campaignId: string | null
}

export interface PositionsResponse {
  positions: Position[]
  summary: {
    totalPositions: number
    totalMarketValue: number
    totalUnrealizedPnl: number
    totalUnrealizedPnlPct: number
    openPositions: number
    closedPositions: number
  }
  timestamp: string
}

export interface PositionFilters {
  symbol?: string | undefined
  strategy?: string | undefined
  status?: PositionStatus | undefined
  type?: PositionType | undefined
  minPnl?: number | undefined
  maxPnl?: number | undefined
}
