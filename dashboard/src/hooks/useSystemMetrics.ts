import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { SystemMetricsSample } from '../types/system'

// Rolling CPU/RAM/Network samples + disk usage — drives SystemPerfMini widget
export function useSystemMetricsSample() {
  return useQuery<SystemMetricsSample>({
    queryKey: ['system', 'metrics', 'sample'],
    queryFn: () => api.get('system/metrics').json<SystemMetricsSample>(),
    staleTime: 15_000,
    refetchInterval: 15_000,
  })
}
