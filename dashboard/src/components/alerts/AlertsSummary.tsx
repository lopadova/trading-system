import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card'
import { Badge } from '../ui/Badge'
import type { AlertsResponse } from '../../types/alert'
import { AlertTriangle, Info, XCircle, Activity } from 'lucide-react'

interface AlertsSummaryProps {
  data: AlertsResponse
}

export function AlertsSummary({ data }: AlertsSummaryProps) {
  const { summary } = data

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {/* Total Alerts */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-medium text-muted">Total Alerts</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-between">
            <div>
              <p className="text-3xl font-bold text-foreground">{summary.total}</p>
              <p className="text-xs text-muted mt-1">
                {summary.active} active, {summary.resolved} resolved
              </p>
            </div>
            <Activity className="h-8 w-8 text-muted" />
          </div>
        </CardContent>
      </Card>

      {/* Critical/Error */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-medium text-muted">Critical & Errors</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-between">
            <div>
              <p className="text-3xl font-bold text-danger">
                {summary.bySeverity.critical + summary.bySeverity.error}
              </p>
              <div className="flex gap-2 mt-1">
                <Badge tone="red" className="text-xs">
                  {summary.bySeverity.critical} Critical
                </Badge>
                <Badge tone="red" className="text-xs">
                  {summary.bySeverity.error} Error
                </Badge>
              </div>
            </div>
            <XCircle className="h-8 w-8 text-danger" />
          </div>
        </CardContent>
      </Card>

      {/* Warnings */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-medium text-muted">Warnings</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-between">
            <div>
              <p className="text-3xl font-bold text-warning">{summary.bySeverity.warning}</p>
              <p className="text-xs text-muted mt-1">Requires attention</p>
            </div>
            <AlertTriangle className="h-8 w-8 text-warning" />
          </div>
        </CardContent>
      </Card>

      {/* Info */}
      <Card>
        <CardHeader>
          <CardTitle className="text-sm font-medium text-muted">Informational</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-between">
            <div>
              <p className="text-3xl font-bold text-accent">{summary.bySeverity.info}</p>
              <p className="text-xs text-muted mt-1">FYI only</p>
            </div>
            <Info className="h-8 w-8 text-accent" />
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
