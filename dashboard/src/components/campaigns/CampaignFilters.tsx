// Campaign filtering controls
// Provides UI for filtering campaigns by various criteria

import { useCampaignFilterStore } from '../../stores/campaignFilterStore'
import { Card, CardContent } from '../ui/Card'
import { Input } from '../ui/Input'
import { Select } from '../ui/Select'
import { X } from 'lucide-react'
import type { CampaignStatus, StrategyType } from '../../types/campaign'

const statusOptions: Array<{ value: CampaignStatus | 'all'; label: string }> = [
  { value: 'all', label: 'All Statuses' },
  { value: 'active', label: 'Active' },
  { value: 'paused', label: 'Paused' },
  { value: 'closed', label: 'Closed' },
  { value: 'pending', label: 'Pending' },
]

const strategyOptions: Array<{ value: StrategyType | 'all'; label: string }> = [
  { value: 'all', label: 'All Strategies' },
  { value: 'put_spread', label: 'Put Spread' },
  { value: 'call_spread', label: 'Call Spread' },
  { value: 'iron_condor', label: 'Iron Condor' },
  { value: 'naked_put', label: 'Naked Put' },
  { value: 'covered_call', label: 'Covered Call' },
  { value: 'other', label: 'Other' },
]

export function CampaignFilters() {
  const {
    search,
    status,
    strategyType,
    underlying,
    dateFrom,
    dateTo,
    setSearch,
    setStatus,
    setStrategyType,
    setUnderlying,
    setDateFrom,
    setDateTo,
    clearFilters,
  } = useCampaignFilterStore()

  const hasActiveFilters =
    search !== '' ||
    status !== 'all' ||
    strategyType !== 'all' ||
    underlying !== '' ||
    dateFrom !== '' ||
    dateTo !== ''

  return (
    <Card>
      <CardContent className="pt-6">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-6 gap-4">
          {/* Search */}
          <div className="lg:col-span-2">
            <Input
              placeholder="Search campaigns..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          {/* Status */}
          <Select
            value={status}
            options={statusOptions}
            onChange={(value) => setStatus(value as CampaignStatus | 'all')}
          />

          {/* Strategy Type */}
          <Select
            value={strategyType}
            options={strategyOptions}
            onChange={(value) => setStrategyType(value as StrategyType | 'all')}
          />

          {/* Underlying */}
          <Input
            placeholder="Underlying (e.g., SPY)"
            value={underlying}
            onChange={(e) => setUnderlying(e.target.value)}
          />

          {/* Clear Filters */}
          <button
            onClick={clearFilters}
            disabled={!hasActiveFilters}
            className="flex items-center justify-center gap-2 px-4 py-2 rounded-lg border border-border bg-background hover:bg-muted/10 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <X className="h-4 w-4" />
            <span className="text-sm font-medium">Clear</span>
          </button>
        </div>

        {/* Date Range Row */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
          <div>
            <label className="block text-xs font-medium text-muted mb-1">Start Date From</label>
            <Input type="date" value={dateFrom} onChange={(e) => setDateFrom(e.target.value)} />
          </div>
          <div>
            <label className="block text-xs font-medium text-muted mb-1">Start Date To</label>
            <Input type="date" value={dateTo} onChange={(e) => setDateTo(e.target.value)} />
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
