import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { AlertsSummary } from '../types/alert'

export function useAlertsSummary() {
  return useQuery<AlertsSummary>({
    queryKey: ['alerts', 'summary-24h'],
    queryFn: () => api.get('alerts/summary-24h').json<AlertsSummary>(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}
