import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { SystemPerfMini } from './SystemPerfMini'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['system', 'metrics', 'sample'], {
    cpu: [20, 30, 35, 42, 48],
    ram: [50, 52, 55, 60, 63],
    network: [10, 25, 18, 30, 22],
    diskUsedPct: 45,
    diskFreeGb: 275,
    diskTotalGb: 500,
    asOf: '2026-04-20T14:30:00.000Z',
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('SystemPerfMini', () => {
  it('renders header, live badge, and panels', () => {
    render(wrap(<SystemPerfMini />))
    expect(screen.getByText('System Performance')).toBeInTheDocument()
    expect(screen.getByText('LIVE')).toBeInTheDocument()
    expect(screen.getByText('CPU')).toBeInTheDocument()
    expect(screen.getByText('RAM')).toBeInTheDocument()
    expect(screen.getByText('Network')).toBeInTheDocument()
    expect(screen.getByText('Disk free')).toBeInTheDocument()
  })
})
