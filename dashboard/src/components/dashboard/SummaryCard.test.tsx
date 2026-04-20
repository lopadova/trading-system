import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { SummaryCard } from './SummaryCard'

function Wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  qc.setQueryData(['performance', 'summary', 'all'], {
    asset: 'all',
    m: 14.3,
    ytd: 15.04,
    y2: 49.88,
    y5: 100.13,
    y10: 98.59,
    ann: 13.07,
    base: 125430,
  })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

beforeEach(() => {
  globalThis.fetch = vi.fn() as unknown as typeof fetch
})

describe('SummaryCard', () => {
  it('renders 6 horizons as %', async () => {
    render(
      <Wrapper>
        <SummaryCard asset="all" />
      </Wrapper>,
    )
    expect(await screen.findByText('+14.30%')).toBeInTheDocument()
    expect(screen.getByText('+15.04%')).toBeInTheDocument()
    expect(screen.getByText('+49.88%')).toBeInTheDocument()
    expect(screen.getByText('+100.13%')).toBeInTheDocument()
    expect(screen.getByText('+98.59%')).toBeInTheDocument()
    expect(screen.getByText('+13.07%')).toBeInTheDocument()
  })

  it('toggles to € unit', async () => {
    render(
      <Wrapper>
        <SummaryCard asset="all" />
      </Wrapper>,
    )
    fireEvent.click(screen.getByRole('button', { name: '€' }))
    // 14.3% of 125430 = 17,936.49 — rounded down to 17,936 by toLocaleString
    expect(await screen.findByText(/€17,936/)).toBeInTheDocument()
  })
})
