import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { MonthlyPerfSection } from './MonthlyPerfSection'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['monthly-returns', 'all'], {
    asset: 'all',
    years: {
      '2026': [1.2, 0.8, -0.5, 2.1, null, null, null, null, null, null, null, null],
      '2025': [2.5, 1.8, -1.2, 0.5, 3.1, -0.8, 1.9, 2.3, -0.4, 1.1, 0.7, 2.0],
    },
    totals: {
      '2026': 3.6,
      '2025': 13.5,
    },
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('MonthlyPerfSection', () => {
  it('renders all 12 month header cells', () => {
    render(wrap(<MonthlyPerfSection asset="all" />))
    expect(screen.getByText('Jan')).toBeInTheDocument()
    expect(screen.getByText('Feb')).toBeInTheDocument()
    expect(screen.getByText('Mar')).toBeInTheDocument()
    expect(screen.getByText('Apr')).toBeInTheDocument()
    expect(screen.getByText('May')).toBeInTheDocument()
    expect(screen.getByText('Jun')).toBeInTheDocument()
    expect(screen.getByText('Jul')).toBeInTheDocument()
    expect(screen.getByText('Aug')).toBeInTheDocument()
    expect(screen.getByText('Sep')).toBeInTheDocument()
    expect(screen.getByText('Oct')).toBeInTheDocument()
    expect(screen.getByText('Nov')).toBeInTheDocument()
    expect(screen.getByText('Dec')).toBeInTheDocument()
  })

  it('renders year rows sorted descending', () => {
    render(wrap(<MonthlyPerfSection asset="all" />))
    expect(screen.getByText('2026')).toBeInTheDocument()
    expect(screen.getByText('2025')).toBeInTheDocument()
  })

  it('disables locked tabs', () => {
    render(wrap(<MonthlyPerfSection asset="all" />))
    const lockedTab = screen.getByRole('button', { name: /Compounded Returns/i })
    expect(lockedTab).toBeDisabled()
  })
})
