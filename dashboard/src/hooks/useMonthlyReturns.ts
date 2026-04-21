import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { AssetBucket } from '../types/performance'

export interface MonthlyReturnsData {
  asset: AssetBucket
  years: Record<string, (number | null)[]>
  totals: Record<string, number>
}

export function useMonthlyReturns(asset: AssetBucket) {
  return useQuery<MonthlyReturnsData>({
    queryKey: ['monthly-returns', asset],
    queryFn: () => api.get(`monthly-returns?asset=${asset}`).json<MonthlyReturnsData>(),
    staleTime: 120_000,
  })
}
