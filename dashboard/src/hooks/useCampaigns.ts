// React Query hook for fetching campaigns with filters

import { useQuery } from '@tanstack/react-query'
import { fetchCampaigns } from '../lib/mockCampaigns'
import type { CampaignFilters, CampaignsResponse } from '../types/campaign'

export function useCampaigns(filters: CampaignFilters) {
  return useQuery<CampaignsResponse, Error>({
    queryKey: ['campaigns', filters],
    queryFn: () => fetchCampaigns(filters),
    staleTime: 30_000, // 30 seconds
    gcTime: 5 * 60_000, // 5 minutes
  })
}
