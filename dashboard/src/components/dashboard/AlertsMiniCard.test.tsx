import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { AlertsMiniCard } from './AlertsMiniCard'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['alerts', 'summary-24h'], {
    total: 12,
    critical: 2,
    warning: 4,
    info: 6,
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('AlertsMiniCard', () => {
  it('renders totals and breakdown badges', () => {
    render(wrap(<AlertsMiniCard />))
    expect(screen.getByText('12')).toBeInTheDocument()
    expect(screen.getByText('2 critical')).toBeInTheDocument()
    expect(screen.getByText('4 warning')).toBeInTheDocument()
    expect(screen.getByText('6 info')).toBeInTheDocument()
  })
})
