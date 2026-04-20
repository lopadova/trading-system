import { useState } from 'react'
import { useAlerts, useResolveAlert } from '../hooks/useAlerts'
import { useAlertFilterStore } from '../stores/alertFilterStore'
import { AlertsSummary } from '../components/alerts/AlertsSummary'
import { AlertFilters } from '../components/alerts/AlertFilters'
import { AlertsTable } from '../components/alerts/AlertsTable'
import { AlertCard } from '../components/alerts/AlertCard'
import { Card, CardContent } from '../components/ui/Card'
import { LayoutGrid, List, RefreshCw } from 'lucide-react'
import { cn } from '../utils/cn'

type ViewMode = 'table' | 'cards'

export function AlertsPage() {
  const [viewMode, setViewMode] = useState<ViewMode>('table')
  const getFilters = useAlertFilterStore((state) => state.getFilters)

  const { data, isLoading, isError, error, refetch, isFetching } = useAlerts(getFilters())
  const resolveAlert = useResolveAlert()

  const handleResolve = (alertId: string, resolved: boolean) => {
    resolveAlert.mutate({ alertId, resolved })
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="p-8 flex flex-col gap-5">
        <div className="flex items-center justify-center h-64">
          <div className="flex flex-col items-center gap-4">
            <RefreshCw className="h-8 w-8 animate-spin text-muted" />
            <p className="text-muted">Loading alerts...</p>
          </div>
        </div>
      </div>
    )
  }

  // Error state
  if (isError) {
    return (
      <div className="p-8 flex flex-col gap-5">
        <Card>
          <CardContent className="py-12">
            <div className="text-center">
              <p className="text-down text-lg font-semibold mb-2">Failed to load alerts</p>
              <p className="text-muted text-sm mb-4">{String(error)}</p>
              <button
                onClick={() => refetch()}
                className="px-4 py-2 bg-[var(--blue)] text-white rounded-md hover:brightness-110 transition-[filter]"
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
      <div className="p-8 flex flex-col gap-5">
        <Card>
          <CardContent className="py-12">
            <div className="text-center">
              <p className="text-muted text-lg">No alert data available</p>
            </div>
          </CardContent>
        </Card>
      </div>
    )
  }

  return (
    <div className="p-8 flex flex-col gap-5">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold mb-2">Alerts</h1>
          <p className="text-muted">
            Monitor system alerts, position risks, and connection issues in real-time
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className={cn(
              'p-2 rounded-md border border-border hover:bg-[var(--bg-3)] transition-colors',
              isFetching && 'opacity-50 cursor-not-allowed'
            )}
            title="Refresh alerts"
          >
            <RefreshCw className={cn('h-5 w-5', isFetching && 'animate-spin')} />
          </button>
          <div className="flex gap-1 border border-border rounded-md p-1">
            <button
              onClick={() => setViewMode('table')}
              className={cn(
                'p-2 rounded transition-colors',
                viewMode === 'table' ? 'bg-[var(--blue)] text-white' : 'hover:bg-[var(--bg-3)]'
              )}
              title="Table view"
            >
              <List className="h-4 w-4" />
            </button>
            <button
              onClick={() => setViewMode('cards')}
              className={cn(
                'p-2 rounded transition-colors',
                viewMode === 'cards' ? 'bg-[var(--blue)] text-white' : 'hover:bg-[var(--bg-3)]'
              )}
              title="Card view"
            >
              <LayoutGrid className="h-4 w-4" />
            </button>
          </div>
        </div>
      </div>

      {/* Summary */}
      <AlertsSummary data={data} />

      {/* Filters */}
      <AlertFilters />

      {/* Alerts Display */}
      {viewMode === 'table' ? (
        <Card>
          <CardContent className="p-0">
            <AlertsTable alerts={data.alerts} onResolve={handleResolve} />
          </CardContent>
        </Card>
      ) : (
        <div className="flex flex-col gap-4">
          {data.alerts.length === 0 ? (
            <Card>
              <CardContent className="py-12">
                <div className="text-center">
                  <p className="text-muted text-lg">No alerts found</p>
                  <p className="text-muted text-sm mt-2">
                    Adjust your filters or check back later
                  </p>
                </div>
              </CardContent>
            </Card>
          ) : (
            data.alerts.map((alert) => (
              <AlertCard key={alert.id} alert={alert} onResolve={handleResolve} />
            ))
          )}
        </div>
      )}

      {/* Footer Info */}
      <div className="text-xs text-muted text-center">
        Last updated: {new Date(data.timestamp).toLocaleString()} • Auto-refresh every 10 seconds
      </div>
    </div>
  )
}
