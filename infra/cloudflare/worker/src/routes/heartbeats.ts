/**
 * Heartbeats API Routes
 * Endpoints for querying service health status
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { ServiceHeartbeatRow } from '../types/database'
import { authMiddleware } from '../middleware/auth'

const heartbeats = new Hono<{ Bindings: Env }>()

// All routes require authentication
heartbeats.use('*', authMiddleware)

/**
 * GET /api/heartbeats
 * Get all service heartbeats
 */
heartbeats.get('/', async (c) => {
  try {
    const result = await c.env.DB.prepare(
      'SELECT * FROM service_heartbeats ORDER BY last_seen_at DESC'
    ).all<ServiceHeartbeatRow>()

    return c.json({
      heartbeats: result.results ?? [],
      count: result.results?.length ?? 0
    })
  } catch (error) {
    console.error('Failed to query heartbeats:', error)
    return c.json({ error: 'database_error', message: 'Failed to query heartbeats' }, 500)
  }
})

/**
 * GET /api/heartbeats/:service_name
 * Get heartbeat for specific service
 */
heartbeats.get('/:service_name', async (c) => {
  const serviceName = c.req.param('service_name')

  if (!serviceName) {
    return c.json({ error: 'invalid_request', message: 'service_name required' }, 400)
  }

  try {
    const row = await c.env.DB.prepare('SELECT * FROM service_heartbeats WHERE service_name = ?')
      .bind(serviceName)
      .first<ServiceHeartbeatRow>()

    if (!row) {
      return c.json({ error: 'not_found', message: 'Service not found' }, 404)
    }

    return c.json({ heartbeat: row })
  } catch (error) {
    console.error('Failed to query heartbeat:', error)
    return c.json({ error: 'database_error', message: 'Failed to query heartbeat' }, 500)
  }
})

/**
 * GET /api/heartbeats/stale/:threshold_seconds
 * Get services that haven't sent heartbeat within threshold
 */
heartbeats.get('/stale/:threshold_seconds', async (c) => {
  const thresholdSeconds = parseInt(c.req.param('threshold_seconds') ?? '60', 10)

  if (isNaN(thresholdSeconds) || thresholdSeconds < 0) {
    return c.json({ error: 'invalid_request', message: 'Invalid threshold_seconds' }, 400)
  }

  try {
    // Calculate cutoff timestamp
    const cutoffDate = new Date(Date.now() - thresholdSeconds * 1000)
    const cutoffIso = cutoffDate.toISOString()

    const result = await c.env.DB.prepare(
      'SELECT * FROM service_heartbeats WHERE last_seen_at < ? ORDER BY last_seen_at ASC'
    )
      .bind(cutoffIso)
      .all<ServiceHeartbeatRow>()

    return c.json({
      stale_services: result.results ?? [],
      count: result.results?.length ?? 0,
      threshold_seconds: thresholdSeconds,
      cutoff_timestamp: cutoffIso
    })
  } catch (error) {
    console.error('Failed to query stale heartbeats:', error)
    return c.json({ error: 'database_error', message: 'Failed to query stale heartbeats' }, 500)
  }
})

export { heartbeats }
