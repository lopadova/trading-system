// Shared test — exercises one hook end-to-end to prove the pattern is wired
// correctly (query key, fetch via `api`, JSON parsing, React Query state).
// The remaining 9 hooks in this batch follow the exact same shape.

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { usePerformanceSummary } from './usePerformanceSummary'

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={client}>{children}</QueryClientProvider>
}

beforeEach(() => {
  // ky uses the global fetch under the hood — stub it to return a fixed payload
  globalThis.fetch = vi.fn(
    async () =>
      new Response(
        JSON.stringify({
          asset: 'all',
          m: 14.3,
          ytd: 15.04,
          y2: 49.88,
          y5: 100.13,
          y10: 98.59,
          ann: 13.07,
          base: 125430,
        }),
        { headers: { 'content-type': 'application/json' } },
      ),
  ) as unknown as typeof fetch
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('usePerformanceSummary', () => {
  it('fetches and returns summary data', async () => {
    const { result } = renderHook(() => usePerformanceSummary('all'), { wrapper })
    await waitFor(() => expect(result.current.data).toBeDefined())
    expect(result.current.data?.m).toBe(14.3)
  })
})
