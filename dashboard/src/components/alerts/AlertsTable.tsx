import { useState } from 'react'
import { formatDistanceToNow } from 'date-fns'
import { Badge } from '../ui/Badge'
import type { Alert } from '../../types/alert'
import {
  AlertCircle,
  AlertTriangle,
  Info,
  XCircle,
  Check,
  ChevronDown,
  ChevronRight,
} from 'lucide-react'
import { cn } from '../../utils/cn'

interface AlertsTableProps {
  alerts: Alert[]
  onResolve?: (alertId: string, resolved: boolean) => void
}

// Map severity to badge variant
function getSeverityVariant(severity: Alert['severity']): 'default' | 'warning' | 'danger' | 'success' {
  switch (severity) {
    case 'critical':
      return 'danger'
    case 'error':
      return 'danger'
    case 'warning':
      return 'warning'
    case 'info':
      return 'default'
  }
}

// Map severity to icon
function getSeverityIcon(severity: Alert['severity']) {
  switch (severity) {
    case 'critical':
      return XCircle
    case 'error':
      return AlertCircle
    case 'warning':
      return AlertTriangle
    case 'info':
      return Info
  }
}

export function AlertsTable({ alerts, onResolve }: AlertsTableProps) {
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set())

  const toggleRow = (alertId: string) => {
    setExpandedRows((prev) => {
      const next = new Set(prev)
      if (next.has(alertId)) {
        next.delete(alertId)
      } else {
        next.add(alertId)
      }
      return next
    })
  }

  if (alerts.length === 0) {
    return (
      <div className="text-center py-12 text-muted">
        <p className="text-lg">No alerts found</p>
        <p className="text-sm mt-2">Adjust your filters or check back later</p>
      </div>
    )
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full">
        <thead>
          <tr className="border-b border-border text-sm text-muted">
            <th className="text-left p-3 font-medium w-8"></th>
            <th className="text-left p-3 font-medium">Severity</th>
            <th className="text-left p-3 font-medium">Type</th>
            <th className="text-left p-3 font-medium">Title</th>
            <th className="text-left p-3 font-medium">Symbol</th>
            <th className="text-left p-3 font-medium">Time</th>
            <th className="text-left p-3 font-medium">Source</th>
            <th className="text-left p-3 font-medium">Status</th>
            <th className="text-left p-3 font-medium w-24">Actions</th>
          </tr>
        </thead>
        <tbody>
          {alerts.map((alert) => {
            const isExpanded = expandedRows.has(alert.id)
            const isResolved = alert.status === 'resolved'
            const SeverityIcon = getSeverityIcon(alert.severity)

            return (
              <>
                <tr
                  key={alert.id}
                  className={cn(
                    'border-b border-border hover:bg-muted/5 transition-colors',
                    isResolved && 'opacity-60'
                  )}
                >
                  <td className="p-3">
                    <button
                      onClick={() => toggleRow(alert.id)}
                      className="text-muted hover:text-foreground transition-colors"
                      title={isExpanded ? 'Collapse' : 'Expand'}
                    >
                      {isExpanded ? (
                        <ChevronDown className="h-4 w-4" />
                      ) : (
                        <ChevronRight className="h-4 w-4" />
                      )}
                    </button>
                  </td>
                  <td className="p-3">
                    <div className="flex items-center gap-2">
                      <SeverityIcon
                        className={cn(
                          'h-4 w-4',
                          alert.severity === 'critical' && 'text-danger',
                          alert.severity === 'error' && 'text-danger',
                          alert.severity === 'warning' && 'text-warning',
                          alert.severity === 'info' && 'text-accent'
                        )}
                      />
                      <Badge variant={getSeverityVariant(alert.severity)} className="text-xs">
                        {alert.severity.toUpperCase()}
                      </Badge>
                    </div>
                  </td>
                  <td className="p-3">
                    <span className="text-sm text-foreground">{alert.type}</span>
                  </td>
                  <td className="p-3">
                    <span className="text-sm font-medium text-foreground">{alert.title}</span>
                  </td>
                  <td className="p-3">
                    {alert.relatedSymbol ? (
                      <span className="px-2 py-0.5 bg-accent/10 text-accent rounded font-mono text-xs">
                        {alert.relatedSymbol}
                      </span>
                    ) : (
                      <span className="text-muted text-sm">—</span>
                    )}
                  </td>
                  <td className="p-3">
                    <span className="text-xs text-muted whitespace-nowrap" title={alert.timestamp}>
                      {formatDistanceToNow(new Date(alert.timestamp), { addSuffix: true })}
                    </span>
                  </td>
                  <td className="p-3">
                    <span className="text-xs text-muted">{alert.source}</span>
                  </td>
                  <td className="p-3">
                    {isResolved ? (
                      <Badge variant="success" className="text-xs">
                        Resolved
                      </Badge>
                    ) : (
                      <Badge variant="warning" className="text-xs">
                        Active
                      </Badge>
                    )}
                  </td>
                  <td className="p-3">
                    {onResolve && (
                      <button
                        onClick={() => onResolve(alert.id, !isResolved)}
                        className={cn(
                          'flex items-center gap-1 px-2 py-1 rounded text-xs font-medium transition-colors',
                          isResolved
                            ? 'bg-muted/10 hover:bg-muted/20 text-muted'
                            : 'bg-success/10 hover:bg-success/20 text-success'
                        )}
                      >
                        <Check className="h-3 w-3" />
                        {isResolved ? 'Reopen' : 'Resolve'}
                      </button>
                    )}
                  </td>
                </tr>
                {isExpanded && (
                  <tr key={`${alert.id}-details`} className="border-b border-border bg-muted/5">
                    <td colSpan={9} className="p-4">
                      <div className="space-y-2 text-sm">
                        <div>
                          <span className="font-medium text-foreground">Message:</span>
                          <p className="text-muted mt-1">{alert.message}</p>
                        </div>
                        {alert.details && (
                          <div>
                            <span className="font-medium text-foreground">Details:</span>
                            <p className="text-muted mt-1 bg-background/50 p-2 rounded border border-border">
                              {alert.details}
                            </p>
                          </div>
                        )}
                        {isResolved && alert.resolvedAt && (
                          <div>
                            <span className="font-medium text-foreground">Resolved:</span>
                            <p className="text-muted mt-1">
                              {formatDistanceToNow(new Date(alert.resolvedAt), { addSuffix: true })}
                            </p>
                          </div>
                        )}
                        {alert.relatedPosition && (
                          <div>
                            <span className="font-medium text-foreground">Related Position:</span>
                            <p className="text-muted mt-1 font-mono">{alert.relatedPosition}</p>
                          </div>
                        )}
                      </div>
                    </td>
                  </tr>
                )}
              </>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
