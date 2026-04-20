import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { PerfSeries, AssetBucket, PerfRange } from '../types/performance'

export function usePerformanceSeries(asset: AssetBucket, range: PerfRange) {
  return useQuery<PerfSeries>({
    queryKey: ['performance', 'series', asset, range],
    queryFn: () => api.get(`performance/series?asset=${asset}&range=${range}`).json<PerfSeries>(),
    staleTime: 30_000,
  })
}
