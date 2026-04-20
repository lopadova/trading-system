import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { SummaryData, AssetBucket } from '../types/performance'

export function usePerformanceSummary(asset: AssetBucket) {
  return useQuery<SummaryData>({
    queryKey: ['performance', 'summary', asset],
    queryFn: () => api.get(`performance/summary?asset=${asset}`).json<SummaryData>(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}
