import { render, screen } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { RecentActivity } from './RecentActivity'

function wrap(ui: ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  const now = Date.now()
  qc.setQueryData(['activity', 'recent', 8], {
    events: [
      {
        id: 'EVT-1',
        icon: 'check-circle-2',
        tone: 'green',
        title: 'Order filled',
        subtitle: 'SPY 450 Call x10',
        timestamp: new Date(now - 5 * 60 * 1000).toISOString(),
      },
      {
        id: 'EVT-2',
        icon: 'alert-triangle',
        tone: 'yellow',
        title: 'IVTS threshold reached',
        subtitle: 'IVTS 1.18',
        timestamp: new Date(now - 15 * 60 * 1000).toISOString(),
      },
    ],
  })
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('RecentActivity', () => {
  it('renders header and event titles', () => {
    render(wrap(<RecentActivity />))
    expect(screen.getByText('Recent Activity')).toBeInTheDocument()
    expect(screen.getByText('Order filled')).toBeInTheDocument()
    expect(screen.getByText('IVTS threshold reached')).toBeInTheDocument()
  })
})
