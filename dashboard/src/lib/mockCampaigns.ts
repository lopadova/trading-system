// Mock campaign data for development and testing
// This will be replaced with real API calls when backend is ready

import type { Campaign, CampaignsResponse, CampaignFilters } from '../types/campaign'

const mockCampaigns: Campaign[] = [
  {
    id: 'camp-001',
    name: 'SPY Put Credit Spreads',
    strategyType: 'put_spread',
    status: 'active',
    description: 'Weekly put credit spreads on SPY, targeting 0.3 delta',
    underlying: 'SPY',
    startDate: '2026-01-15T00:00:00Z',
    capital: 50000,
    currentValue: 54250,
    pnl: {
      realized: 3200,
      unrealized: 1050,
      total: 4250,
      totalPct: 8.5,
      fees: 180,
      netPnL: 4070,
    },
    stats: {
      totalTrades: 24,
      openPositions: 2,
      closedPositions: 22,
      winRate: 0.82,
      avgWin: 215,
      avgLoss: -380,
      maxDrawdown: -850,
      sharpeRatio: 1.8,
    },
    parameters: {
      delta_target: 0.3,
      dte_min: 30,
      dte_max: 45,
      max_positions: 3,
      profit_target: 0.5,
      stop_loss: 2.0,
    },
    tags: ['conservative', 'weekly'],
    lastUpdate: '2026-04-05T10:30:00Z',
  },
  {
    id: 'camp-002',
    name: 'QQQ Iron Condor',
    strategyType: 'iron_condor',
    status: 'active',
    description: 'Monthly iron condors on QQQ with 16 delta strikes',
    underlying: 'QQQ',
    startDate: '2026-02-01T00:00:00Z',
    capital: 30000,
    currentValue: 31890,
    pnl: {
      realized: 1680,
      unrealized: 210,
      total: 1890,
      totalPct: 6.3,
      fees: 240,
      netPnL: 1650,
    },
    stats: {
      totalTrades: 8,
      openPositions: 1,
      closedPositions: 7,
      winRate: 0.75,
      avgWin: 380,
      avgLoss: -520,
      maxDrawdown: -520,
      sharpeRatio: 1.5,
    },
    parameters: {
      delta_target: 0.16,
      dte_target: 45,
      wing_width: 10,
      max_positions: 2,
      profit_target: 0.5,
    },
    tags: ['income', 'neutral'],
    lastUpdate: '2026-04-05T09:15:00Z',
  },
  {
    id: 'camp-003',
    name: 'IWM Naked Puts',
    strategyType: 'naked_put',
    status: 'paused',
    description: 'Selling naked puts on IWM during high IV periods',
    underlying: 'IWM',
    startDate: '2026-03-01T00:00:00Z',
    capital: 25000,
    currentValue: 25450,
    pnl: {
      realized: 580,
      unrealized: -130,
      total: 450,
      totalPct: 1.8,
      fees: 95,
      netPnL: 355,
    },
    stats: {
      totalTrades: 6,
      openPositions: 1,
      closedPositions: 5,
      winRate: 0.67,
      avgWin: 290,
      avgLoss: -410,
      maxDrawdown: -410,
    },
    parameters: {
      delta_target: 0.2,
      dte_min: 30,
      iv_percentile_min: 50,
      max_positions: 2,
    },
    tags: ['aggressive', 'high-iv'],
    lastUpdate: '2026-04-04T16:00:00Z',
  },
  {
    id: 'camp-004',
    name: 'AAPL Covered Calls',
    strategyType: 'covered_call',
    status: 'active',
    description: 'Monthly covered calls on 500 shares of AAPL',
    underlying: 'AAPL',
    startDate: '2025-12-01T00:00:00Z',
    capital: 85000,
    currentValue: 91200,
    pnl: {
      realized: 4850,
      unrealized: 1350,
      total: 6200,
      totalPct: 7.3,
      fees: 120,
      netPnL: 6080,
    },
    stats: {
      totalTrades: 12,
      openPositions: 1,
      closedPositions: 11,
      winRate: 0.92,
      avgWin: 520,
      avgLoss: -180,
      maxDrawdown: -180,
      sharpeRatio: 2.1,
    },
    parameters: {
      shares: 500,
      delta_target: 0.3,
      dte_target: 30,
      strike_offset_pct: 5,
    },
    tags: ['income', 'stock'],
    lastUpdate: '2026-04-05T11:00:00Z',
  },
  {
    id: 'camp-005',
    name: 'SPX Call Spreads (Closed)',
    strategyType: 'call_spread',
    status: 'closed',
    description: 'Experimental call spreads on SPX - closed due to low profitability',
    underlying: 'SPX',
    startDate: '2026-01-10T00:00:00Z',
    endDate: '2026-03-15T00:00:00Z',
    capital: 20000,
    currentValue: 19420,
    pnl: {
      realized: -580,
      unrealized: 0,
      total: -580,
      totalPct: -2.9,
      fees: 210,
      netPnL: -790,
    },
    stats: {
      totalTrades: 10,
      openPositions: 0,
      closedPositions: 10,
      winRate: 0.4,
      avgWin: 180,
      avgLoss: -310,
      maxDrawdown: -950,
    },
    parameters: {
      delta_target: 0.35,
      dte_min: 14,
      dte_max: 21,
      spread_width: 25,
    },
    tags: ['experimental', 'failed'],
    lastUpdate: '2026-03-15T16:00:00Z',
  },
  {
    id: 'camp-006',
    name: 'TLT Put Spreads',
    strategyType: 'put_spread',
    status: 'active',
    description: 'Put credit spreads on TLT for rate volatility exposure',
    underlying: 'TLT',
    startDate: '2026-03-20T00:00:00Z',
    capital: 15000,
    currentValue: 15340,
    pnl: {
      realized: 280,
      unrealized: 60,
      total: 340,
      totalPct: 2.3,
      fees: 45,
      netPnL: 295,
    },
    stats: {
      totalTrades: 4,
      openPositions: 1,
      closedPositions: 3,
      winRate: 0.75,
      avgWin: 150,
      avgLoss: -120,
      maxDrawdown: -120,
    },
    parameters: {
      delta_target: 0.25,
      dte_target: 45,
      spread_width: 2,
      max_positions: 2,
    },
    tags: ['rates', 'volatility'],
    lastUpdate: '2026-04-05T08:45:00Z',
  },
]

function applyFilters(campaigns: Campaign[], filters: CampaignFilters): Campaign[] {
  let filtered = [...campaigns]

  // Search filter (name, underlying, description)
  if (filters.search) {
    const search = filters.search.toLowerCase()
    filtered = filtered.filter(
      (c) =>
        c.name.toLowerCase().includes(search) ||
        c.underlying.toLowerCase().includes(search) ||
        c.description?.toLowerCase().includes(search)
    )
  }

  // Status filter
  if (filters.status) {
    filtered = filtered.filter((c) => c.status === filters.status)
  }

  // Strategy type filter
  if (filters.strategyType) {
    filtered = filtered.filter((c) => c.strategyType === filters.strategyType)
  }

  // Underlying filter
  if (filters.underlying) {
    const underlying = filters.underlying.toLowerCase()
    filtered = filtered.filter((c) => c.underlying.toLowerCase().includes(underlying))
  }

  // Date range filters
  if (filters.dateFrom) {
    filtered = filtered.filter((c) => c.startDate >= filters.dateFrom!)
  }

  if (filters.dateTo) {
    filtered = filtered.filter((c) => {
      const campaignDate = c.endDate ?? c.lastUpdate
      return campaignDate <= filters.dateTo!
    })
  }

  return filtered
}

function calculateSummary(campaigns: Campaign[]) {
  const totalCampaigns = campaigns.length
  const activeCampaigns = campaigns.filter((c) => c.status === 'active').length
  const pausedCampaigns = campaigns.filter((c) => c.status === 'paused').length
  const closedCampaigns = campaigns.filter((c) => c.status === 'closed').length

  const totalCapital = campaigns.reduce((sum, c) => sum + c.capital, 0)
  const totalPnL = campaigns.reduce((sum, c) => sum + c.pnl.netPnL, 0)
  const totalPnLPct = totalCapital > 0 ? (totalPnL / totalCapital) * 100 : 0

  const avgWinRate =
    campaigns.length > 0
      ? campaigns.reduce((sum, c) => sum + c.stats.winRate, 0) / campaigns.length
      : 0

  return {
    totalCampaigns,
    activeCampaigns,
    pausedCampaigns,
    closedCampaigns,
    totalCapital,
    totalPnL,
    totalPnLPct,
    avgWinRate,
  }
}

export async function fetchCampaigns(filters: CampaignFilters): Promise<CampaignsResponse> {
  // Simulate API delay
  await new Promise((resolve) => setTimeout(resolve, 300))

  const filtered = applyFilters(mockCampaigns, filters)
  const summary = calculateSummary(filtered)

  return {
    campaigns: filtered,
    summary,
    timestamp: new Date().toISOString(),
  }
}

export async function fetchCampaign(id: string): Promise<Campaign | null> {
  // Simulate API delay
  await new Promise((resolve) => setTimeout(resolve, 200))

  const campaign = mockCampaigns.find((c) => c.id === id)
  return campaign ?? null
}

export async function updateCampaignStatus(
  id: string,
  status: 'active' | 'paused'
): Promise<Campaign | null> {
  // Simulate API delay
  await new Promise((resolve) => setTimeout(resolve, 300))

  const campaign = mockCampaigns.find((c) => c.id === id)
  if (!campaign) {
    return null
  }

  // In real implementation, this would call the API
  // For mock, we just return the campaign with updated status
  return { ...campaign, status, lastUpdate: new Date().toISOString() }
}
