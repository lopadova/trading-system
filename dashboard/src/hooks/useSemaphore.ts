import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { SemaphoreData } from '../types/risk'

/**
 * Fetch the Options Trading Semaphore composite indicator.
 * Refetches every 15 seconds to keep the operator console current.
 */
export function useSemaphore() {
  return useQuery<SemaphoreData>({
    queryKey: ['risk', 'semaphore'],
    queryFn: () => api.get('risk/semaphore').json<SemaphoreData>(),
    staleTime: 15_000,
    refetchInterval: 15_000,
  })
}
