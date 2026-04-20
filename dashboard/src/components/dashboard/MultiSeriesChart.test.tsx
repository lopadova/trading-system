import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { MultiSeriesChart } from './MultiSeriesChart'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['performance', 'series', 'all', '1M'], {
    asset: 'all',
    range: '1M',
    portfolio: Array.from({ length: 20 }, (_, i) => 100 + i),
    sp500: Array.from({ length: 20 }, (_, i) => 100 + i * 0.8),
    swda: Array.from({ length: 20 }, (_, i) => 100 + i * 0.7),
    startDate: '2026-03-31T00:00:00.000Z',
    endDate: '2026-04-20T00:00:00.000Z',
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('MultiSeriesChart', () => {
  it('renders 3 toggle chips and all range buttons', () => {
    render(wrap(<MultiSeriesChart asset="all" />))
    expect(screen.getByRole('button', { name: /Portfolio/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /S&P 500/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /SWDA/ })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'ALL' })).toBeInTheDocument()
  })

  it('toggles a series off', () => {
    render(wrap(<MultiSeriesChart asset="all" />))
    const btn = screen.getByRole('button', { name: /SWDA/ })
    fireEvent.click(btn)
    expect(btn.getAttribute('aria-pressed')).toBe('false')
  })
})
