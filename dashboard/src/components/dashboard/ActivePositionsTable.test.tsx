import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { ActivePositionsTable } from './ActivePositionsTable'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['positions', { status: 'open' }], {
    positions: [
      {
        id: 'POS-TEST-1',
        symbol: 'SPY',
        type: 'option',
        side: 'short',
        status: 'open',
        quantity: 10,
        entryPrice: 450,
        currentPrice: 452.25,
        marketValue: 45225,
        costBasis: 45000,
        unrealizedPnl: 225,
        unrealizedPnlPct: 0.5,
        avgCost: 450,
        openDate: '2026-04-01T14:30:00Z',
        lastUpdate: new Date().toISOString(),
        strategy: 'Iron Condor',
        expiration: '2026-05-20',
        campaign: 'Theta Harvest',
        campaignId: 'CAMP-1',
      },
    ],
    summary: {
      totalPositions: 1,
      totalMarketValue: 45225,
      totalUnrealizedPnl: 225,
      totalUnrealizedPnlPct: 0.5,
      openPositions: 1,
      closedPositions: 0,
    },
    timestamp: new Date().toISOString(),
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('ActivePositionsTable', () => {
  it('renders header and seeded position', () => {
    render(wrap(<ActivePositionsTable />))
    expect(screen.getByText('Active Positions')).toBeInTheDocument()
    expect(screen.getByText('SPY')).toBeInTheDocument()
    expect(screen.getByText('Theta Harvest')).toBeInTheDocument()
    expect(screen.getByText('Iron Condor')).toBeInTheDocument()
  })
})
