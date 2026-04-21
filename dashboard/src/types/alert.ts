// Alert types for the Trading System Dashboard

export type AlertSeverity = 'info' | 'warning' | 'error' | 'critical'

export type AlertType =
  | 'PositionRisk'
  | 'ConnectionLost'
  | 'OrderFailed'
  | 'HighVolatility'
  | 'MarketClosed'
  | 'IvtsThreshold'
  | 'SystemHealth'
  | 'DataStale'

export type AlertStatus = 'active' | 'resolved'

export interface Alert {
  id: string
  type: AlertType
  severity: AlertSeverity
  status: AlertStatus
  title: string
  message: string
  details?: string
  timestamp: string
  resolvedAt?: string | undefined
  source: string // Which service generated the alert
  relatedSymbol?: string | undefined
  relatedPosition?: string | undefined
  metadata?: Record<string, unknown> | undefined
}

export interface AlertsResponse {
  alerts: Alert[]
  summary: {
    total: number
    active: number
    resolved: number
    bySeverity: {
      info: number
      warning: number
      error: number
      critical: number
    }
    byType: Record<AlertType, number>
  }
  timestamp: string
}

export interface AlertFilters {
  severity?: AlertSeverity | undefined
  type?: AlertType | undefined
  status?: AlertStatus | undefined
  search?: string | undefined
  dateFrom?: string | undefined
  dateTo?: string | undefined
}

// Worker API: aggregated counts for the 24h summary card
export interface AlertsSummary {
  total: number
  critical: number
  warning: number
  info: number
}
