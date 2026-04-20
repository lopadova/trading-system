import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import type { CampaignsSummary } from '../types/campaign'

export function useCampaignsSummary() {
  return useQuery<CampaignsSummary>({
    queryKey: ['campaigns', 'summary'],
    queryFn: () => api.get('campaigns/summary').json<CampaignsSummary>(),
    staleTime: 30_000,
  })
}
