/**
 * Alerts API Routes
 * Endpoints for querying alert history and unresolved alerts
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { AlertHistoryRow } from '../types/database'
import { authMiddleware } from '../middleware/auth'

const alerts = new Hono<{ Bindings: Env }>()

// All routes require authentication
alerts.use('*', authMiddleware)

/**
 * GET /api/alerts
 * Query alerts with optional filters
 */
alerts.get('/', async (c) => {
  const severity = c.req.query('severity') as 'info' | 'warning' | 'critical' | undefined
  const alertType = c.req.query('alert_type')
  const sourceService = c.req.query('source_service')
  const unresolvedOnly = c.req.query('unresolved_only') === 'true'
  const limit = parseInt(c.req.query('limit') ?? '100', 10)

  // Build query dynamically
  let sql = 'SELECT * FROM alert_history WHERE 1=1'
  const params: string[] = []

  if (severity) {
    sql += ' AND severity = ?'
    params.push(severity)
  }

  if (alertType) {
    sql += ' AND alert_type = ?'
    params.push(alertType)
  }

  if (sourceService) {
    sql += ' AND source_service = ?'
    params.push(sourceService)
  }

  if (unresolvedOnly) {
    sql += ' AND resolved_at IS NULL'
  }

  sql += ' ORDER BY created_at DESC LIMIT ?'
  params.push(String(limit))

  try {
    const result = await c.env.DB.prepare(sql).bind(...params).all<AlertHistoryRow>()

    return c.json({
      alerts: result.results ?? [],
      count: result.results?.length ?? 0
    })
  } catch (error) {
    console.error('Failed to query alerts:', error)
    return c.json({ error: 'database_error', message: 'Failed to query alerts' }, 500)
  }
})

/**
 * GET /api/alerts/unresolved
 * Get all unresolved alerts
 */
alerts.get('/unresolved', async (c) => {
  const limit = parseInt(c.req.query('limit') ?? '100', 10)

  try {
    const result = await c.env.DB.prepare(
      'SELECT * FROM alert_history WHERE resolved_at IS NULL ORDER BY created_at DESC LIMIT ?'
    )
      .bind(limit)
      .all<AlertHistoryRow>()

    return c.json({
      alerts: result.results ?? [],
      count: result.results?.length ?? 0
    })
  } catch (error) {
    console.error('Failed to query unresolved alerts:', error)
    return c.json({ error: 'database_error', message: 'Failed to query alerts' }, 500)
  }
})

/**
 * GET /api/alerts/:alert_id
 * Get single alert by ID
 */
alerts.get('/:alert_id', async (c) => {
  const alertId = c.req.param('alert_id')

  if (!alertId) {
    return c.json({ error: 'invalid_request', message: 'alert_id required' }, 400)
  }

  try {
    const row = await c.env.DB.prepare('SELECT * FROM alert_history WHERE alert_id = ?')
      .bind(alertId)
      .first<AlertHistoryRow>()

    if (!row) {
      return c.json({ error: 'not_found', message: 'Alert not found' }, 404)
    }

    return c.json({ alert: row })
  } catch (error) {
    console.error('Failed to query alert:', error)
    return c.json({ error: 'database_error', message: 'Failed to query alert' }, 500)
  }
})

export { alerts }
