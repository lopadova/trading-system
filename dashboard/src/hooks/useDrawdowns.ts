import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { DrawdownsData, DrawdownRange } from '../types/drawdown'
import type { AssetBucket } from '../types/performance'

export function useDrawdowns(asset: AssetBucket, range: DrawdownRange) {
  return useQuery<DrawdownsData>({
    queryKey: ['drawdowns', asset, range],
    queryFn: () => api.get(`drawdowns?asset=${asset}&range=${range}`).json<DrawdownsData>(),
    staleTime: 60_000,
  })
}
