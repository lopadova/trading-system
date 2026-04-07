/**
 * Alerts Query Handler
 * Fetches recent alerts from D1 database
 */

import type { D1Database } from '@cloudflare/workers-types'
import type { AlertsData } from '../types'

/**
 * Query alerts
 * @param db - D1 database instance
 * @returns Alerts data
 */
export async function queryAlerts(db: D1Database): Promise<AlertsData> {
  const now = new Date().toISOString()

  try {
    // Get last 10 alerts
    const alertsResult = await db
      .prepare(
        `SELECT
          severity,
          message,
          created_at,
          resolved_at
        FROM alert_history
        ORDER BY created_at DESC
        LIMIT 10`
      )
      .all<{
        severity: 'info' | 'warning' | 'critical'
        message: string
        created_at: string
        resolved_at: string | null
      }>()

    // Map alerts to result format
    const alerts = (alertsResult.results || []).map((a) => ({
      severity: a.severity,
      message: a.message,
      createdAt: a.created_at,
      resolved: a.resolved_at !== null
    }))

    return {
      alerts,
      timestamp: now
    }
  } catch (error) {
    console.error('[BOT] queryAlerts error:', error)

    // Return empty data on error
    return {
      alerts: [],
      timestamp: now
    }
  }
}
