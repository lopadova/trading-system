import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { SemaphoreCard } from './SemaphoreCard'
import type { SemaphoreData } from '../../types/risk'

const MOCK: SemaphoreData = {
  score: 22,
  status: 'green',
  asOf: '2026-04-20T15:45:52.000Z',
  spx: { price: 6767.54, change: 62.42, changePct: 0.93 },
  vix: { price: 18.56, change: -1.96, changePct: -9.55 },
  regime: 'BULLISH',
  exchangeTime: '20-Apr-2026 15:45:52 ET',
  indicators: [
    {
      id: 'regime',
      label: 'Market Regime',
      status: 'green',
      value: 'SPX > MA200',
      detail: 'SPX 6767.54 above 200-day MA 6350.00',
    },
    {
      id: 'vix_level',
      label: 'VIX Level (1Y pct)',
      status: 'green',
      value: 18.56,
      detail: '18.56 (42nd pct, 1Y)',
    },
    {
      id: 'vix_rolling_yield',
      label: 'VIX Rolling Yield (30d)',
      status: 'green',
      value: 0.62,
      detail: '0.62 (72nd pct, 1Y)',
    },
    {
      id: 'ivts',
      label: 'IVTS (VIX / VIX3M)',
      status: 'green',
      value: 0.926,
      detail: '0.93 (contango)',
    },
    {
      id: 'overall',
      label: 'Operatività',
      status: 'green',
      value: 22,
      detail: 'Safe to sell PUTs on SPX',
    },
  ],
}

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['risk', 'semaphore'], MOCK)
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('SemaphoreCard', () => {
  it('renders title, gauge score, SPX/VIX quotes and indicator rows', () => {
    render(wrap(<SemaphoreCard />))
    expect(screen.getByText('Options Trading Semaphore')).toBeInTheDocument()
    // Status label
    expect(screen.getByText('OPERATIVE')).toBeInTheDocument()
    // Regime
    expect(screen.getByText('BULLISH')).toBeInTheDocument()
    // Quote symbols
    expect(screen.getByText('SPX')).toBeInTheDocument()
    expect(screen.getByText('VIX')).toBeInTheDocument()
    // Indicator labels (overall is hidden because it's shown as the big dot)
    expect(screen.getByText('Market Regime')).toBeInTheDocument()
    expect(screen.getByText('VIX Level (1Y pct)')).toBeInTheDocument()
    expect(screen.getByText('VIX Rolling Yield (30d)')).toBeInTheDocument()
    expect(screen.getByText('IVTS (VIX / VIX3M)')).toBeInTheDocument()
    // Exchange time
    expect(screen.getByText('20-Apr-2026 15:45:52 ET')).toBeInTheDocument()
  })

  it('accepts a data prop (bypassing the hook)', () => {
    const redData: SemaphoreData = { ...MOCK, status: 'red', score: 85 }
    // Use a fresh QueryClient with no cached data to ensure we are reading the prop.
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
    render(
      <QueryClientProvider client={qc}>
        <SemaphoreCard data={redData} />
      </QueryClientProvider>
    )
    expect(screen.getByText('HALT')).toBeInTheDocument()
  })
})
