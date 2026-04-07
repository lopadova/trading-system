// Campaign types for the Trading System Dashboard

export type CampaignStatus = 'active' | 'paused' | 'closed' | 'pending'
export type StrategyType = 'put_spread' | 'call_spread' | 'iron_condor' | 'naked_put' | 'covered_call' | 'other'

export interface CampaignPnL {
  realized: number
  unrealized: number
  total: number
  totalPct: number
  fees: number
  netPnL: number
}

export interface CampaignStats {
  totalTrades: number
  openPositions: number
  closedPositions: number
  winRate: number
  avgWin: number
  avgLoss: number
  maxDrawdown: number
  sharpeRatio?: number
}

export interface Campaign {
  id: string
  name: string
  strategyType: StrategyType
  status: CampaignStatus
  description?: string | undefined
  underlying: string // Symbol (e.g., SPY, QQQ)
  startDate: string
  endDate?: string | undefined
  capital: number
  currentValue: number
  pnl: CampaignPnL
  stats: CampaignStats
  parameters: Record<string, string | number | boolean>
  tags?: string[] | undefined
  lastUpdate: string
}

export interface CampaignsResponse {
  campaigns: Campaign[]
  summary: {
    totalCampaigns: number
    activeCampaigns: number
    pausedCampaigns: number
    closedCampaigns: number
    totalCapital: number
    totalPnL: number
    totalPnLPct: number
    avgWinRate: number
  }
  timestamp: string
}

export interface CampaignFilters {
  search?: string | undefined
  status?: CampaignStatus | undefined
  strategyType?: StrategyType | undefined
  underlying?: string | undefined
  dateFrom?: string | undefined
  dateTo?: string | undefined
}
