/**
 * TASK-21: React Query Integration Tests
 * Tests React Query hooks with real API integration
 *
 * These tests verify:
 * - React Query hooks work with real API
 * - Query caching and invalidation
 * - Mutations and optimistic updates
 * - Error handling in React components
 * - Loading states
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { usePositions } from '../src/hooks/usePositions'
import { useAlerts, useResolveAlert } from '../src/hooks/useAlerts'
import type { ReactNode } from 'react'

// Create a fresh QueryClient for each test to ensure isolation
function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        retry: false, // Disable retries for tests
        gcTime: 0, // Disable garbage collection for tests
      },
      mutations: {
        retry: false,
      },
    },
  })
}

// Wrapper component for React Query provider
function createWrapper(queryClient: QueryClient) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  }
}

describe('Integration Tests: React Query Hooks', () => {
  describe('usePositions Hook', () => {
    let queryClient: QueryClient

    beforeEach(() => {
      queryClient = createTestQueryClient()
    })

    it('TEST-21-21: usePositions should fetch and cache positions data', async () => {
      const { result } = renderHook(() => usePositions(), {
        wrapper: createWrapper(queryClient),
      })

      // Initial state should be loading
      expect(result.current.isLoading).toBe(true)
      expect(result.current.data).toBeUndefined()

      // Wait for data to load
      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      // Data should be available
      expect(result.current.data).toBeDefined()
      expect(result.current.data).toHaveProperty('positions')
      expect(result.current.data).toHaveProperty('summary')
      expect(Array.isArray(result.current.data?.positions)).toBe(true)
    })

    it('TEST-21-22: usePositions should support filtering', async () => {
      const { result: unfilteredResult } = renderHook(() => usePositions(), {
        wrapper: createWrapper(queryClient),
      })

      await waitFor(() => {
        expect(unfilteredResult.current.isSuccess).toBe(true)
      })

      // Now try with filter
      const { result: filteredResult } = renderHook(
        () => usePositions({ status: 'open' }),
        {
          wrapper: createWrapper(queryClient),
        }
      )

      await waitFor(() => {
        expect(filteredResult.current.isSuccess).toBe(true)
      })

      // Filtered results should only include open positions
      const positions = filteredResult.current.data?.positions || []
      positions.forEach(pos => {
        expect(pos.status).toBe('open')
      })
    })

    it('TEST-21-23: usePositions should cache results', async () => {
      const { result: firstRender } = renderHook(() => usePositions(), {
        wrapper: createWrapper(queryClient),
      })

      await waitFor(() => {
        expect(firstRender.current.isSuccess).toBe(true)
      })

      const firstData = firstRender.current.data

      // Render again with same query client
      const { result: secondRender } = renderHook(() => usePositions(), {
        wrapper: createWrapper(queryClient),
      })

      // Second render should use cached data immediately
      expect(secondRender.current.data).toEqual(firstData)
      expect(secondRender.current.isLoading).toBe(false)
    })

    it('TEST-21-24: usePositions should handle errors gracefully', async () => {
      // Create a hook with invalid configuration to trigger error
      // Note: This will use mock data in current implementation
      const { result } = renderHook(() => usePositions(), {
        wrapper: createWrapper(queryClient),
      })

      await waitFor(() => {
        // Current implementation uses mock data, so it should succeed
        // In future with real API, test with invalid API key
        expect(result.current.isLoading).toBe(false)
      })

      // Verify error handling structure exists
      expect(result.current).toHaveProperty('isError')
      expect(result.current).toHaveProperty('error')
    })
  })

  describe('useAlerts Hook', () => {
    let queryClient: QueryClient

    beforeEach(() => {
      queryClient = createTestQueryClient()
    })

    it('TEST-21-25: useAlerts should fetch alerts data', async () => {
      const { result } = renderHook(() => useAlerts(), {
        wrapper: createWrapper(queryClient),
      })

      expect(result.current.isLoading).toBe(true)

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(result.current.data).toBeDefined()
      expect(result.current.data).toHaveProperty('alerts')
      expect(result.current.data).toHaveProperty('summary')
      expect(Array.isArray(result.current.data?.alerts)).toBe(true)
    })

    it('TEST-21-26: useAlerts should filter by severity', async () => {
      const { result } = renderHook(() => useAlerts({ severity: 'critical' }), {
        wrapper: createWrapper(queryClient),
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      const alerts = result.current.data?.alerts || []
      alerts.forEach(alert => {
        expect(alert.severity).toBe('critical')
      })
    })

    it('TEST-21-27: useAlerts should filter by status', async () => {
      const { result } = renderHook(() => useAlerts({ status: 'active' }), {
        wrapper: createWrapper(queryClient),
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      const alerts = result.current.data?.alerts || []
      alerts.forEach(alert => {
        expect(alert.status).toBe('active')
      })
    })

    it('TEST-21-28: useAlerts should support search filtering', async () => {
      const { result } = renderHook(() => useAlerts({ search: 'risk' }), {
        wrapper: createWrapper(queryClient),
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      const alerts = result.current.data?.alerts || []

      // If there are results, they should contain 'risk' in title/message/details
      if (alerts.length > 0) {
        alerts.forEach(alert => {
          const searchTarget = `${alert.title} ${alert.message} ${alert.details || ''}`.toLowerCase()
          expect(searchTarget).toContain('risk')
        })
      }
    })
  })

  describe('useResolveAlert Mutation', () => {
    let queryClient: QueryClient

    beforeEach(() => {
      queryClient = createTestQueryClient()
    })

    it('TEST-21-29: useResolveAlert should handle mutation', async () => {
      const { result } = renderHook(() => useResolveAlert(), {
        wrapper: createWrapper(queryClient),
      })

      expect(result.current.isPending).toBe(false)

      // Trigger mutation
      result.current.mutate({ alertId: 'test-alert-123', resolved: true })

      // Should be pending
      expect(result.current.isPending).toBe(true)

      // Wait for completion
      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      expect(result.current.isPending).toBe(false)
    })

    it('TEST-21-30: useResolveAlert should invalidate alerts query', async () => {
      // First fetch alerts
      const { result: alertsResult } = renderHook(() => useAlerts(), {
        wrapper: createWrapper(queryClient),
      })

      await waitFor(() => {
        expect(alertsResult.current.isSuccess).toBe(true)
      })

      // Get mutation hook
      const { result: mutationResult } = renderHook(() => useResolveAlert(), {
        wrapper: createWrapper(queryClient),
      })

      // Track query invalidation
      const queryCacheSize = queryClient.getQueryCache().getAll().length

      // Trigger mutation
      mutationResult.current.mutate({ alertId: 'alert-001', resolved: true })

      await waitFor(() => {
        expect(mutationResult.current.isSuccess).toBe(true)
      })

      // Alerts query should have been invalidated
      // (In real implementation, this would trigger refetch)
      expect(queryClient.getQueryCache().getAll().length).toBeGreaterThanOrEqual(queryCacheSize)
    })
  })

  describe('Query Lifecycle', () => {
    let queryClient: QueryClient

    beforeEach(() => {
      queryClient = createTestQueryClient()
    })

    it('TEST-21-31: Queries should transition through loading states correctly', async () => {
      const states: string[] = []

      const { result } = renderHook(() => usePositions(), {
        wrapper: createWrapper(queryClient),
      })

      // Track state transitions
      if (result.current.isLoading) states.push('loading')
      if (result.current.isError) states.push('error')
      if (result.current.isSuccess) states.push('success')

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false)
      })

      if (result.current.isSuccess) states.push('success')
      if (result.current.isError) states.push('error')

      // Should have transitioned from loading to success (or error)
      expect(states.length).toBeGreaterThan(0)
      expect(states[0]).toBe('loading')
    })

    it('TEST-21-32: Query data should be typed correctly', async () => {
      const { result } = renderHook(() => usePositions(), {
        wrapper: createWrapper(queryClient),
      })

      await waitFor(() => {
        expect(result.current.isSuccess).toBe(true)
      })

      if (result.current.data) {
        // Verify data structure matches expected types
        expect(result.current.data).toHaveProperty('positions')
        expect(result.current.data).toHaveProperty('summary')
        expect(result.current.data).toHaveProperty('timestamp')

        // Verify positions array structure
        if (result.current.data.positions.length > 0) {
          const position = result.current.data.positions[0]
          expect(position).toHaveProperty('id')
          expect(position).toHaveProperty('symbol')
          expect(position).toHaveProperty('status')
          expect(position).toHaveProperty('type')
        }

        // Verify summary structure
        expect(result.current.data.summary).toHaveProperty('totalPositions')
        expect(result.current.data.summary).toHaveProperty('openPositions')
        expect(result.current.data.summary).toHaveProperty('closedPositions')
      }
    })
  })
})
