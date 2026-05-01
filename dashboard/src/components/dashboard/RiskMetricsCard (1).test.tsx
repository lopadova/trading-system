import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { RiskMetricsCard } from './RiskMetricsCard'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['risk', 'metrics'], {
    vix: 18.5,
    vix1d: 17.2,
    vix3m: 19.8, // Added missing vix3m property
    delta: 12.3,
    theta: -45.6,
    vega: 78.9,
    ivRankSpy: 0.42,
    buyingPower: 45000,
    marginUsedPct: 28,
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('RiskMetricsCard', () => {
  it('renders without crashing and shows row labels', () => {
    render(wrap(<RiskMetricsCard />))
    expect(screen.getByText('Risk Metrics')).toBeInTheDocument()
    expect(screen.getByText('Portfolio Delta')).toBeInTheDocument()
    expect(screen.getByText('VIX Index')).toBeInTheDocument()
    expect(screen.getByText('Margin Used')).toBeInTheDocument()
  })
})
