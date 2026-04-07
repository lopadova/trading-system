import { useState } from 'react'
import { usePositions } from '../hooks/usePositions'
import { usePositionFilterStore } from '../stores/positionFilterStore'
import { PositionsSummary } from '../components/positions/PositionsSummary'
import { PositionFilters } from '../components/positions/PositionFilters'
import { PositionsTable } from '../components/positions/PositionsTable'
import { PositionCard } from '../components/positions/PositionCard'
import { Card, CardContent } from '../components/ui/Card'
import { LayoutGrid, List, RefreshCw } from 'lucide-react'
import { cn } from '../utils/cn'

type ViewMode = 'table' | 'cards'

export function PositionsPage() {
  const [viewMode, setViewMode] = useState<ViewMode>('table')
  const getFilters = usePositionFilterStore((state) => state.getFilters)

  const { data, isLoading, isError, error, refetch, isFetching } = usePositions(getFilters())

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center justify-center h-64">
          <div className="flex flex-col items-center gap-4">
            <RefreshCw className="h-8 w-8 animate-spin text-muted" />
            <p className="text-muted">Loading positions...</p>
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
              <p className="text-danger text-lg font-semibold mb-2">Failed to load positions</p>
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

  // No data
  if (!data) {
    return (
      <div className="space-y-6">
        <Card>
          <CardContent className="py-12">
            <div className="text-center">
              <p className="text-muted text-lg">No position data available</p>
            </div>
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold mb-2">Positions</h1>
          <p className="text-muted">
            Monitor your open and closed positions with real-time P&L tracking
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className={cn(
              'p-2 rounded-lg border border-border hover:bg-muted/10 transition-colors',
              isFetching && 'opacity-50 cursor-not-allowed'
            )}
            title="Refresh positions"
          >
            <RefreshCw className={cn('h-5 w-5', isFetching && 'animate-spin')} />
          </button>
          <div className="flex gap-1 border border-border rounded-lg p-1">
            <button
              onClick={() => setViewMode('table')}
              className={cn(
                'p-2 rounded transition-colors',
                viewMode === 'table'
                  ? 'bg-accent text-white'
                  : 'hover:bg-muted/10'
              )}
              title="Table view"
            >
              <List className="h-4 w-4" />
            </button>
            <button
              onClick={() => setViewMode('cards')}
              className={cn(
                'p-2 rounded transition-colors',
                viewMode === 'cards'
                  ? 'bg-accent text-white'
                  : 'hover:bg-muted/10'
              )}
              title="Card view"
            >
              <LayoutGrid className="h-4 w-4" />
            </button>
          </div>
        </div>
      </div>

      {/* Summary */}
      <PositionsSummary data={data} />

      {/* Filters */}
      <PositionFilters />

      {/* Positions Display */}
      {viewMode === 'table' ? (
        <Card>
          <CardContent className="p-0">
            <PositionsTable positions={data.positions} />
          </CardContent>
        </Card>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
          {data.positions.length === 0 ? (
            <div className="col-span-full text-center py-12">
              <p className="text-muted text-lg">No positions found</p>
              <p className="text-muted text-sm mt-2">Adjust your filters or wait for new positions</p>
            </div>
          ) : (
            data.positions.map((position) => (
              <PositionCard key={position.id} position={position} />
            ))
          )}
        </div>
      )}

      {/* Footer Info */}
      <div className="text-xs text-muted text-center">
        Last updated: {new Date(data.timestamp).toLocaleString()} • Auto-refresh every 30 seconds
      </div>
    </div>
  )
}
