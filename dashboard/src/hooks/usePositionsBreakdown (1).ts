import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { PositionsBreakdownData } from '../types/breakdown'

export function usePositionsBreakdown() {
  return useQuery<PositionsBreakdownData>({
    queryKey: ['positions', 'breakdown'],
    queryFn: () => api.get('positions/breakdown').json<PositionsBreakdownData>(),
    staleTime: 60_000,
  })
}
