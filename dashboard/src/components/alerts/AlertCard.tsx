/**
 * File-level eslint suppression: the severity/type icon helpers (defined at
 * module scope) return stable lucide components, but react-hooks/static-
 * components flags both the `const ... = helper(prop)` declarations and the
 * JSX usages inside render. Refactoring to a component-per-icon wrapper would
 * add churn without changing behaviour. Disable the rule here and keep the
 * helpers pure.
 */
/* eslint-disable react-hooks/static-components */
import { formatDistanceToNow } from 'date-fns'
import { Card, CardContent, CardHeader } from '../ui/Card'
import { Badge, type BadgeTone } from '../ui/Badge'
import type { Alert } from '../../types/alert'
import {
  AlertCircle,
  AlertTriangle,
  Info,
  XCircle,
  Check,
  Activity,
  TrendingUp,
  Wifi,
  BarChart3,
  Clock,
  Database,
} from 'lucide-react'
import { cn } from '../../utils/cn'

interface AlertCardProps {
  alert: Alert
  onResolve?: (alertId: string, resolved: boolean) => void
}

function getSeverityTone(severity: Alert['severity']): BadgeTone {
  switch (severity) {
    case 'critical':
    case 'error':
      return 'red'
    case 'warning':
      return 'yellow'
    case 'info':
      return 'muted'
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

// Map alert type to icon
function getTypeIcon(type: Alert['type']) {
  switch (type) {
    case 'PositionRisk':
      return TrendingUp
    case 'ConnectionLost':
      return Wifi
    case 'OrderFailed':
      return XCircle
    case 'HighVolatility':
      return BarChart3
    case 'MarketClosed':
      return Clock
    case 'IvtsThreshold':
      return Activity
    case 'SystemHealth':
      return Activity
    case 'DataStale':
      return Database
  }
}

export function AlertCard({ alert, onResolve }: AlertCardProps) {
  const SeverityIcon = getSeverityIcon(alert.severity)
  const TypeIcon = getTypeIcon(alert.type)

  const isResolved = alert.status === 'resolved'

  return (
    <Card className={cn(isResolved && 'opacity-60')}>
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div className="flex items-start gap-3 flex-1">
            <div
              className={cn(
                'p-2 rounded-lg',
                alert.severity === 'critical' && 'bg-danger/10',
                alert.severity === 'error' && 'bg-danger/10',
                alert.severity === 'warning' && 'bg-warning/10',
                alert.severity === 'info' && 'bg-accent/10'
              )}
            >
              <SeverityIcon
                className={cn(
                  'h-5 w-5',
                  alert.severity === 'critical' && 'text-danger',
                  alert.severity === 'error' && 'text-danger',
                  alert.severity === 'warning' && 'text-warning',
                  alert.severity === 'info' && 'text-accent'
                )}
              />
            </div>
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1 flex-wrap">
                <Badge tone={getSeverityTone(alert.severity)}>
                  {alert.severity.toUpperCase()}
                </Badge>
                <Badge tone="muted">
                  <TypeIcon className="h-3 w-3 mr-1 inline" />
                  {alert.type}
                </Badge>
                {isResolved && <Badge tone="green">Resolved</Badge>}
              </div>
              <h3 className="font-semibold text-foreground mb-1">{alert.title}</h3>
              <p className="text-sm text-muted">{alert.message}</p>
              {alert.details && (
                <p className="text-xs text-muted mt-2 bg-muted/5 p-2 rounded border border-border">
                  {alert.details}
                </p>
              )}
            </div>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className="flex items-center justify-between text-xs text-muted">
          <div className="flex items-center gap-4">
            <span title="Alert timestamp">
              {formatDistanceToNow(new Date(alert.timestamp), { addSuffix: true })}
            </span>
            {alert.relatedSymbol && (
              <span className="px-2 py-0.5 bg-accent/10 text-accent rounded font-mono">
                {alert.relatedSymbol}
              </span>
            )}
            <span className="text-muted/70">{alert.source}</span>
          </div>
          {onResolve && (
            <button
              onClick={() => onResolve(alert.id, !isResolved)}
              className={cn(
                'flex items-center gap-1 px-3 py-1.5 rounded-lg transition-colors font-medium',
                isResolved
                  ? 'bg-muted/10 hover:bg-muted/20 text-muted'
                  : 'bg-success/10 hover:bg-success/20 text-success'
              )}
            >
              <Check className="h-3.5 w-3.5" />
              {isResolved ? 'Reopen' : 'Resolve'}
            </button>
          )}
        </div>
        {isResolved && alert.resolvedAt && (
          <div className="mt-2 text-xs text-muted">
            Resolved {formatDistanceToNow(new Date(alert.resolvedAt), { addSuffix: true })}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
