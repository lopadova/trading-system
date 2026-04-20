import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { ActivityResponse } from '../types/activity'

export function useRecentActivity(limit = 8) {
  return useQuery<ActivityResponse>({
    queryKey: ['activity', 'recent', limit],
    queryFn: () => api.get(`activity/recent?limit=${limit}`).json<ActivityResponse>(),
    staleTime: 30_000,
    refetchInterval: 30_000,
  })
}
