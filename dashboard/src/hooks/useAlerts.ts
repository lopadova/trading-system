import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
// import ky from 'ky' // Will be used when API is implemented
import type { AlertsResponse, AlertFilters, Alert, AlertType } from '../types/alert'

// Mock data generator
function generateMockAlerts(): AlertsResponse {
  const now = new Date()
  const alerts: Alert[] = [
    {
      id: 'ALT-001',
      type: 'PositionRisk',
      severity: 'critical',
      status: 'active',
      title: 'Position Risk Limit Exceeded',
      message: 'SPY Iron Condor position exceeded max loss threshold',
      details: 'Current loss: -$850 (threshold: -$500). Consider closing or adjusting position.',
      timestamp: new Date(now.getTime() - 5 * 60 * 1000).toISOString(), // 5 min ago
      source: 'TradingSupervisor',
      relatedSymbol: 'SPY',
      relatedPosition: 'POS-001',
    },
    {
      id: 'ALT-002',
      type: 'IvtsThreshold',
      severity: 'warning',
      status: 'active',
      title: 'IVTS Above Suspend Threshold',
      message: 'IVTS indicator reached 1.18, approaching suspend threshold',
      details: 'Current IVTS: 1.18. Suspend threshold: 1.15. New positions will be suspended if threshold exceeded.',
      timestamp: new Date(now.getTime() - 15 * 60 * 1000).toISOString(), // 15 min ago
      source: 'TradingSupervisor',
    },
    {
      id: 'ALT-003',
      type: 'ConnectionLost',
      severity: 'error',
      status: 'resolved',
      title: 'IBKR Connection Lost',
      message: 'Lost connection to Interactive Brokers TWS',
      details: 'Connection dropped at 14:32. Reconnected at 14:35.',
      timestamp: new Date(now.getTime() - 2 * 60 * 60 * 1000).toISOString(), // 2 hours ago
      resolvedAt: new Date(now.getTime() - 1.5 * 60 * 60 * 1000).toISOString(),
      source: 'OptionsExecutionService',
    },
    {
      id: 'ALT-004',
      type: 'HighVolatility',
      severity: 'warning',
      status: 'active',
      title: 'High Volatility Detected',
      message: 'VIX spiked to 28.5, significantly above recent average',
      details: 'VIX: 28.5 (+35% from 20-day average). Consider reducing position sizes.',
      timestamp: new Date(now.getTime() - 45 * 60 * 1000).toISOString(), // 45 min ago
      source: 'TradingSupervisor',
      relatedSymbol: 'VIX',
    },
    {
      id: 'ALT-005',
      type: 'OrderFailed',
      severity: 'error',
      status: 'active',
      title: 'Order Execution Failed',
      message: 'Failed to execute QQQ credit spread order',
      details: 'Order rejected by broker: Insufficient buying power. Required: $5,000, Available: $4,200',
      timestamp: new Date(now.getTime() - 30 * 60 * 1000).toISOString(), // 30 min ago
      source: 'OptionsExecutionService',
      relatedSymbol: 'QQQ',
    },
    {
      id: 'ALT-006',
      type: 'DataStale',
      severity: 'warning',
      status: 'resolved',
      title: 'Market Data Delayed',
      message: 'Market data feed delayed by more than 5 seconds',
      details: 'Data delay detected at 11:20. Resumed normal operation at 11:22.',
      timestamp: new Date(now.getTime() - 3 * 60 * 60 * 1000).toISOString(), // 3 hours ago
      resolvedAt: new Date(now.getTime() - 2.9 * 60 * 60 * 1000).toISOString(),
      source: 'OptionsExecutionService',
    },
    {
      id: 'ALT-007',
      type: 'SystemHealth',
      severity: 'info',
      status: 'resolved',
      title: 'High CPU Usage',
      message: 'CPU usage exceeded 80% for 5 consecutive minutes',
      details: 'CPU peaked at 85%. Returned to normal levels after scheduled task completed.',
      timestamp: new Date(now.getTime() - 4 * 60 * 60 * 1000).toISOString(), // 4 hours ago
      resolvedAt: new Date(now.getTime() - 3.8 * 60 * 60 * 1000).toISOString(),
      source: 'TradingSupervisor',
    },
    {
      id: 'ALT-008',
      type: 'MarketClosed',
      severity: 'info',
      status: 'active',
      title: 'Market Closed',
      message: 'US equity markets are currently closed',
      details: 'Markets will reopen on Monday at 9:30 AM ET.',
      timestamp: new Date(now.getTime() - 12 * 60 * 60 * 1000).toISOString(), // 12 hours ago
      source: 'TradingSupervisor',
    },
  ]

  // Calculate summary
  const summary = {
    total: alerts.length,
    active: alerts.filter((a) => a.status === 'active').length,
    resolved: alerts.filter((a) => a.status === 'resolved').length,
    bySeverity: {
      info: alerts.filter((a) => a.severity === 'info').length,
      warning: alerts.filter((a) => a.severity === 'warning').length,
      error: alerts.filter((a) => a.severity === 'error').length,
      critical: alerts.filter((a) => a.severity === 'critical').length,
    },
    byType: alerts.reduce(
      (acc, alert) => {
        acc[alert.type] = (acc[alert.type] || 0) + 1
        return acc
      },
      {} as Record<AlertType, number>
    ),
  }

  return {
    alerts,
    summary,
    timestamp: new Date().toISOString(),
  }
}

// API client function (currently using mock data)
async function fetchAlerts(filters?: AlertFilters): Promise<AlertsResponse> {
  // TODO: Replace with real API call when Cloudflare Worker is ready
  // const apiKey = import.meta.env.VITE_API_KEY
  // const response = await ky.get('/api/v1/alerts', {
  //   headers: { 'X-Api-Key': apiKey },
  //   searchParams: filters as Record<string, string>,
  // }).json<AlertsResponse>()

  // Simulate network delay
  await new Promise((resolve) => setTimeout(resolve, 200))

  const data = generateMockAlerts()

  // Apply filters to mock data
  if (filters) {
    data.alerts = data.alerts.filter((alert) => {
      // Severity filter
      if (filters.severity && alert.severity !== filters.severity) {
        return false
      }

      // Type filter
      if (filters.type && alert.type !== filters.type) {
        return false
      }

      // Status filter
      if (filters.status && alert.status !== filters.status) {
        return false
      }

      // Search filter (title, message, details)
      if (filters.search) {
        const searchLower = filters.search.toLowerCase()
        const matchesSearch =
          alert.title.toLowerCase().includes(searchLower) ||
          alert.message.toLowerCase().includes(searchLower) ||
          (alert.details && alert.details.toLowerCase().includes(searchLower)) ||
          false

        if (!matchesSearch) {
          return false
        }
      }

      // Date range filters
      if (filters.dateFrom) {
        const alertDate = new Date(alert.timestamp)
        const fromDate = new Date(filters.dateFrom)
        if (alertDate < fromDate) {
          return false
        }
      }

      if (filters.dateTo) {
        const alertDate = new Date(alert.timestamp)
        const toDate = new Date(filters.dateTo)
        // Set to end of day
        toDate.setHours(23, 59, 59, 999)
        if (alertDate > toDate) {
          return false
        }
      }

      return true
    })

    // Recalculate summary after filtering
    data.summary = {
      total: data.alerts.length,
      active: data.alerts.filter((a) => a.status === 'active').length,
      resolved: data.alerts.filter((a) => a.status === 'resolved').length,
      bySeverity: {
        info: data.alerts.filter((a) => a.severity === 'info').length,
        warning: data.alerts.filter((a) => a.severity === 'warning').length,
        error: data.alerts.filter((a) => a.severity === 'error').length,
        critical: data.alerts.filter((a) => a.severity === 'critical').length,
      },
      byType: data.alerts.reduce(
        (acc, alert) => {
          acc[alert.type] = (acc[alert.type] || 0) + 1
          return acc
        },
        {} as Record<AlertType, number>
      ),
    }
  }

  return data
}

// React Query hook for alerts
export function useAlerts(filters?: AlertFilters) {
  return useQuery({
    queryKey: ['alerts', filters],
    queryFn: () => fetchAlerts(filters),
    staleTime: 5_000, // 5 seconds
    refetchInterval: 10_000, // Auto-refresh every 10 seconds
  })
}

// Mutation hook to resolve/unresolve alerts
export function useResolveAlert() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async ({ alertId, resolved }: { alertId: string; resolved: boolean }) => {
      // TODO: Replace with real API call
      // await ky.patch(`/api/v1/alerts/${alertId}`, {
      //   headers: { 'X-Api-Key': import.meta.env.VITE_API_KEY },
      //   json: { status: resolved ? 'resolved' : 'active' },
      // })

      // Simulate API call
      await new Promise((resolve) => setTimeout(resolve, 300))
      return { alertId, resolved }
    },
    onSuccess: () => {
      // Invalidate and refetch alerts
      queryClient.invalidateQueries({ queryKey: ['alerts'] })
    },
  })
}
