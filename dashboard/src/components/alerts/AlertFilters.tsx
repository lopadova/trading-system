import { useAlertFilterStore } from '../../stores/alertFilterStore'
import { Card, CardContent } from '../ui/Card'
import { X } from 'lucide-react'
import { cn } from '../../utils/cn'
import type { AlertSeverity, AlertType, AlertStatus } from '../../types/alert'

const severityOptions: { value: AlertSeverity; label: string }[] = [
  { value: 'critical', label: 'Critical' },
  { value: 'error', label: 'Error' },
  { value: 'warning', label: 'Warning' },
  { value: 'info', label: 'Info' },
]

const typeOptions: { value: AlertType; label: string }[] = [
  { value: 'PositionRisk', label: 'Position Risk' },
  { value: 'ConnectionLost', label: 'Connection Lost' },
  { value: 'OrderFailed', label: 'Order Failed' },
  { value: 'HighVolatility', label: 'High Volatility' },
  { value: 'MarketClosed', label: 'Market Closed' },
  { value: 'IvtsThreshold', label: 'IVTS Threshold' },
  { value: 'SystemHealth', label: 'System Health' },
  { value: 'DataStale', label: 'Data Stale' },
]

const statusOptions: { value: AlertStatus; label: string }[] = [
  { value: 'active', label: 'Active' },
  { value: 'resolved', label: 'Resolved' },
]

export function AlertFilters() {
  const {
    severity,
    type,
    status,
    search,
    setSeverity,
    setType,
    setStatus,
    setSearch,
    clearFilters,
  } = useAlertFilterStore()

  const hasActiveFilters = severity || type || status !== 'active' || search

  return (
    <Card>
      <CardContent className="p-4">
        <div className="flex items-center justify-between mb-4">
          <h3 className="font-semibold text-foreground">Filters</h3>
          {hasActiveFilters && (
            <button
              onClick={clearFilters}
              className="flex items-center gap-1 text-sm text-muted hover:text-foreground transition-colors"
            >
              <X className="h-4 w-4" />
              Clear all
            </button>
          )}
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* Search */}
          <div>
            <label htmlFor="alert-search" className="block text-sm font-medium text-foreground mb-1">
              Search
            </label>
            <input
              id="alert-search"
              type="text"
              value={search || ''}
              onChange={(e) => setSearch(e.target.value || undefined)}
              placeholder="Search alerts..."
              className={cn(
                'w-full px-3 py-2 rounded-lg border border-border',
                'bg-background text-foreground placeholder:text-muted',
                'focus:outline-none focus:ring-2 focus:ring-accent/50'
              )}
            />
          </div>

          {/* Status */}
          <div>
            <label htmlFor="alert-status" className="block text-sm font-medium text-foreground mb-1">
              Status
            </label>
            <select
              id="alert-status"
              value={status || ''}
              onChange={(e) => setStatus((e.target.value as AlertStatus) || undefined)}
              className={cn(
                'w-full px-3 py-2 rounded-lg border border-border',
                'bg-background text-foreground',
                'focus:outline-none focus:ring-2 focus:ring-accent/50'
              )}
            >
              <option value="">All statuses</option>
              {statusOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          {/* Severity */}
          <div>
            <label htmlFor="alert-severity" className="block text-sm font-medium text-foreground mb-1">
              Severity
            </label>
            <select
              id="alert-severity"
              value={severity || ''}
              onChange={(e) => setSeverity((e.target.value as AlertSeverity) || undefined)}
              className={cn(
                'w-full px-3 py-2 rounded-lg border border-border',
                'bg-background text-foreground',
                'focus:outline-none focus:ring-2 focus:ring-accent/50'
              )}
            >
              <option value="">All severities</option>
              {severityOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>

          {/* Type */}
          <div>
            <label htmlFor="alert-type" className="block text-sm font-medium text-foreground mb-1">
              Type
            </label>
            <select
              id="alert-type"
              value={type || ''}
              onChange={(e) => setType((e.target.value as AlertType) || undefined)}
              className={cn(
                'w-full px-3 py-2 rounded-lg border border-border',
                'bg-background text-foreground',
                'focus:outline-none focus:ring-2 focus:ring-accent/50'
              )}
            >
              <option value="">All types</option>
              {typeOptions.map((opt) => (
                <option key={opt.value} value={opt.value}>
                  {opt.label}
                </option>
              ))}
            </select>
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
