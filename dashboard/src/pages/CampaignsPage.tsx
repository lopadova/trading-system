// Campaigns page component
// Main view for displaying and managing trading campaigns

import { useCampaigns } from '../hooks/useCampaigns'
import { useCampaignFilterStore } from '../stores/campaignFilterStore'
import { useToastStore } from '../stores/toastStore'
import { CampaignsSummary } from '../components/campaigns/CampaignsSummary'
import { CampaignFilters } from '../components/campaigns/CampaignFilters'
import { CampaignCard } from '../components/campaigns/CampaignCard'
import { Card, CardContent } from '../components/ui/Card'
import { RefreshCw, FolderOpen } from 'lucide-react'
import { updateCampaignStatus } from '../lib/mockCampaigns'
import { useQueryClient } from '@tanstack/react-query'

export function CampaignsPage() {
  const getFilters = useCampaignFilterStore((state) => state.getFilters)
  const addToast = useToastStore((state) => state.addToast)
  const queryClient = useQueryClient()

  const { data, isLoading, isError, error, refetch, isFetching } = useCampaigns(getFilters())

  // Handle campaign status change
  const handleStatusChange = async (id: string, status: 'active' | 'paused') => {
    try {
      await updateCampaignStatus(id, status)
      addToast('success', `Campaign ${status === 'active' ? 'resumed' : 'paused'} successfully`)
      // Invalidate queries to refetch data
      await queryClient.invalidateQueries({ queryKey: ['campaigns'] })
    } catch (err) {
      addToast('error', `Failed to update campaign: ${String(err)}`)
    }
  }

  // Handle campaign card click
  const handleCampaignClick = (id: string) => {
    // In future, this could open a modal with campaign details
    // For now, just show a toast
    addToast('info', `Campaign details for ${id} (coming soon)`)
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-center h-64">
          <div className="flex flex-col items-center gap-4">
            <RefreshCw className="h-8 w-8 animate-spin text-muted" />
            <p className="text-muted">Loading campaigns...</p>
          </div>
        </div>
      </div>
    )
  }

  // Error state
  if (isError) {
    return (
      <div className="space-y-6">
        <Card>
          <CardContent className="py-12">
            <div className="text-center">
              <p className="text-danger text-lg font-semibold mb-2">Failed to load campaigns</p>
              <p className="text-muted text-sm mb-4">{String(error)}</p>
              <button
                onClick={() => refetch()}
                className="px-4 py-2 bg-accent text-white rounded-lg hover:bg-accent/90 transition-colors"
              >
                Retry
              </button>
            </div>
          </CardContent>
        </Card>
      </div>
    )
  }

  // No data state
  if (!data) {
    return (
      <div className="space-y-6">
        <Card>
          <CardContent className="py-12">
            <div className="text-center text-muted">
              <p>No campaign data available</p>
            </div>
          </CardContent>
        </Card>
      </div>
    )
  }

  // Empty state (no campaigns match filters)
  if (data.campaigns.length === 0) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <h1 className="text-2xl font-bold">Campaigns</h1>
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className="flex items-center gap-2 px-4 py-2 bg-accent text-white rounded-lg hover:bg-accent/90 transition-colors disabled:opacity-50"
          >
            <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
            Refresh
          </button>
        </div>

        <CampaignFilters />

        <Card>
          <CardContent className="py-12">
            <div className="text-center">
              <FolderOpen className="h-12 w-12 text-muted mx-auto mb-4" />
              <p className="text-muted text-lg font-semibold mb-2">No campaigns found</p>
              <p className="text-muted text-sm">Try adjusting your filters</p>
            </div>
          </CardContent>
        </Card>
      </div>
    )
  }

  // Main content
  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Campaigns</h1>
        <button
          onClick={() => refetch()}
          disabled={isFetching}
          className="flex items-center gap-2 px-4 py-2 bg-accent text-white rounded-lg hover:bg-accent/90 transition-colors disabled:opacity-50"
        >
          <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
          Refresh
        </button>
      </div>

      {/* Summary Cards */}
      <CampaignsSummary data={data} />

      {/* Filters */}
      <CampaignFilters />

      {/* Campaign Count */}
      <div className="flex items-center justify-between text-sm text-muted">
        <span>
          Showing {data.campaigns.length} campaign{data.campaigns.length !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Campaign Cards Grid */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {data.campaigns.map((campaign) => (
          <CampaignCard
            key={campaign.id}
            campaign={campaign}
            onStatusChange={handleStatusChange}
            onClick={handleCampaignClick}
          />
        ))}
      </div>
    </div>
  )
}
