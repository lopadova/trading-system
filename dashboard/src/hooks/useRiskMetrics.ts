import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { RiskMetrics } from '../types/risk'

export function useRiskMetrics() {
  return useQuery<RiskMetrics>({
    queryKey: ['risk', 'metrics'],
    queryFn: () => api.get('risk/metrics').json<RiskMetrics>(),
    staleTime: 15_000,
    refetchInterval: 15_000,
  })
}
