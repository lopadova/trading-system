import { useQuery } from '@tanstack/react-query'
// import ky from 'ky' // Will be used when API is implemented
import type { PositionsResponse, PositionFilters } from '../types/position'

// Mock data for initial implementation (will be replaced with real API)
function generateMockPositions(): PositionsResponse {
  const positions = [
    {
      id: 'POS-001',
      symbol: 'SPY',
      underlying: 'SPY',
      strategy: 'Iron Condor',
      type: 'option' as const,
      side: 'short' as const,
      status: 'open' as const,
      quantity: 10,
      entryPrice: 450.50,
      currentPrice: 452.25,
      marketValue: 45225.00,
      costBasis: 45050.00,
      unrealizedPnl: 175.00,
      unrealizedPnlPct: 0.39,
      avgCost: 450.50,
      openDate: '2026-04-01T14:30:00Z',
      strike: 450,
      expiration: '2026-04-18',
      right: 'call' as const,
      greeks: {
        delta: -0.45,
        gamma: 0.012,
        theta: -0.08,
        vega: 0.15,
        iv: 0.18,
      },
      lastUpdate: new Date().toISOString(),
      campaign: 'Theta Harvest',
      campaignId: 'CAMP-001',
    },
    {
      id: 'POS-002',
      symbol: 'QQQ',
      underlying: 'QQQ',
      strategy: 'Credit Spread',
      type: 'option' as const,
      side: 'short' as const,
      status: 'open' as const,
      quantity: 5,
      entryPrice: 380.00,
      currentPrice: 378.50,
      marketValue: 18925.00,
      costBasis: 19000.00,
      unrealizedPnl: -75.00,
      unrealizedPnlPct: -0.39,
      avgCost: 380.00,
      openDate: '2026-04-02T10:15:00Z',
      strike: 380,
      expiration: '2026-04-18',
      right: 'put' as const,
      greeks: {
        delta: 0.32,
        gamma: 0.008,
        theta: -0.06,
        vega: 0.12,
        iv: 0.22,
      },
      lastUpdate: new Date().toISOString(),
      campaign: 'Weekly Premium',
      campaignId: 'CAMP-002',
    },
    {
      id: 'POS-003',
      symbol: 'AAPL',
      type: 'stock' as const,
      side: 'long' as const,
      status: 'open' as const,
      quantity: 100,
      entryPrice: 175.25,
      currentPrice: 178.50,
      marketValue: 17850.00,
      costBasis: 17525.00,
      unrealizedPnl: 325.00,
      unrealizedPnlPct: 1.85,
      avgCost: 175.25,
      openDate: '2026-03-28T09:30:00Z',
      lastUpdate: new Date().toISOString(),
      campaign: null,
      campaignId: null,
    },
    {
      id: 'POS-004',
      symbol: 'TSLA',
      underlying: 'TSLA',
      strategy: 'Covered Call',
      type: 'option' as const,
      side: 'short' as const,
      status: 'closed' as const,
      quantity: 2,
      entryPrice: 220.00,
      currentPrice: 215.00,
      marketValue: 0,
      costBasis: 4400.00,
      unrealizedPnl: 0,
      unrealizedPnlPct: 0,
      realizedPnl: 150.00,
      avgCost: 220.00,
      openDate: '2026-03-15T11:00:00Z',
      closeDate: '2026-04-04T15:45:00Z',
      strike: 220,
      expiration: '2026-04-18',
      right: 'call' as const,
      lastUpdate: new Date().toISOString(),
      campaign: null,
      campaignId: null,
    },
  ]

  const totalMarketValue = positions
    .filter((p) => p.status === 'open')
    .reduce((sum, p) => sum + p.marketValue, 0)

  const totalUnrealizedPnl = positions
    .filter((p) => p.status === 'open')
    .reduce((sum, p) => sum + p.unrealizedPnl, 0)

  const totalCostBasis = positions
    .filter((p) => p.status === 'open')
    .reduce((sum, p) => sum + p.costBasis, 0)

  return {
    positions,
    summary: {
      totalPositions: positions.length,
      totalMarketValue,
      totalUnrealizedPnl,
      totalUnrealizedPnlPct: totalCostBasis > 0 ? (totalUnrealizedPnl / totalCostBasis) * 100 : 0,
      openPositions: positions.filter((p) => p.status === 'open').length,
      closedPositions: positions.filter((p) => p.status === 'closed').length,
    },
    timestamp: new Date().toISOString(),
  }
}

// API client function (currently using mock data)
async function fetchPositions(filters?: PositionFilters): Promise<PositionsResponse> {
  // TODO: Replace with real API call when Cloudflare Worker is ready
  // const apiKey = import.meta.env.VITE_API_KEY
  // const response = await ky.get('/api/v1/positions', {
  //   headers: { 'X-Api-Key': apiKey },
  //   searchParams: filters as Record<string, string>,
  // }).json<PositionsResponse>()

  // Simulate network delay
  await new Promise((resolve) => setTimeout(resolve, 300))

  const data = generateMockPositions()

  // Apply filters to mock data
  if (filters) {
    data.positions = data.positions.filter((pos) => {
      if (filters.symbol && !pos.symbol.toLowerCase().includes(filters.symbol.toLowerCase())) {
        return false
      }
      if (filters.strategy && pos.strategy && !pos.strategy.toLowerCase().includes(filters.strategy.toLowerCase())) {
        return false
      }
      if (filters.status && pos.status !== filters.status) {
        return false
      }
      if (filters.type && pos.type !== filters.type) {
        return false
      }
      if (filters.minPnl !== undefined && pos.unrealizedPnl < filters.minPnl) {
        return false
      }
      if (filters.maxPnl !== undefined && pos.unrealizedPnl > filters.maxPnl) {
        return false
      }
      return true
    })

    // Recalculate summary after filtering
    const totalMarketValue = data.positions
      .filter((p) => p.status === 'open')
      .reduce((sum, p) => sum + p.marketValue, 0)

    const totalUnrealizedPnl = data.positions
      .filter((p) => p.status === 'open')
      .reduce((sum, p) => sum + p.unrealizedPnl, 0)

    const totalCostBasis = data.positions
      .filter((p) => p.status === 'open')
      .reduce((sum, p) => sum + p.costBasis, 0)

    data.summary = {
      totalPositions: data.positions.length,
      totalMarketValue,
      totalUnrealizedPnl,
      totalUnrealizedPnlPct: totalCostBasis > 0 ? (totalUnrealizedPnl / totalCostBasis) * 100 : 0,
      openPositions: data.positions.filter((p) => p.status === 'open').length,
      closedPositions: data.positions.filter((p) => p.status === 'closed').length,
    }
  }

  return data
}

// React Query hook for positions
export function usePositions(filters?: PositionFilters) {
  return useQuery({
    queryKey: ['positions', filters],
    queryFn: () => fetchPositions(filters),
    staleTime: 10_000, // 10 seconds
    refetchInterval: 30_000, // Auto-refresh every 30 seconds
  })
}
