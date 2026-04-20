import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { DrawdownsSection } from './DrawdownsSection'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['drawdowns', 'all', '10Y'], {
    asset: 'all',
    range: '10Y',
    portfolioSeries: Array.from({ length: 40 }, (_, i) => -Math.abs(Math.sin(i / 4) * 10)),
    sp500Series: Array.from({ length: 40 }, (_, i) => -Math.abs(Math.cos(i / 3) * 8)),
    worst: [
      { depthPct: -18.5, start: '2020-02', end: '2020-07', months: 5 },
      { depthPct: -12.3, start: '2022-01', end: '2022-10', months: 9 },
      { depthPct: -8.7, start: '2023-03', end: '2023-06', months: 3 },
      { depthPct: -5.1, start: '2024-11', end: '2025-01', months: 2 },
    ],
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('DrawdownsSection', () => {
  it('renders headings and 4 worst-drawdown rows', () => {
    render(wrap(<DrawdownsSection asset="all" />))
    expect(screen.getByText('Drawdowns')).toBeInTheDocument()
    expect(screen.getByText('Worst Drawdowns')).toBeInTheDocument()
    // Each row shows depth as formatted percent — one per row
    expect(screen.getByText('-18.50%')).toBeInTheDocument()
    expect(screen.getByText('-12.30%')).toBeInTheDocument()
    expect(screen.getByText('-8.70%')).toBeInTheDocument()
    expect(screen.getByText('-5.10%')).toBeInTheDocument()
  })
})
