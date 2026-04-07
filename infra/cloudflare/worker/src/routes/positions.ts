/**
 * Positions API Routes
 * Endpoints for querying active positions and position history
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { ActivePositionRow, PositionHistoryRow } from '../types/database'
import { authMiddleware } from '../middleware/auth'

const positions = new Hono<{ Bindings: Env }>()

// All routes require authentication
positions.use('*', authMiddleware)

/**
 * GET /api/positions/active
 * Query active positions with optional filters
 */
positions.get('/active', async (c) => {
  const campaignId = c.req.query('campaign_id')
  const symbol = c.req.query('symbol')
  const strategyName = c.req.query('strategy_name')
  const limit = parseInt(c.req.query('limit') ?? '100', 10)

  // Build query dynamically based on filters
  let sql = 'SELECT * FROM active_positions WHERE 1=1'
  const params: string[] = []

  if (campaignId) {
    sql += ' AND campaign_id = ?'
    params.push(campaignId)
  }

  if (symbol) {
    sql += ' AND symbol = ?'
    params.push(symbol)
  }

  if (strategyName) {
    sql += ' AND strategy_name = ?'
    params.push(strategyName)
  }

  sql += ' ORDER BY opened_at DESC LIMIT ?'
  params.push(String(limit))

  try {
    const result = await c.env.DB.prepare(sql).bind(...params).all<ActivePositionRow>()

    return c.json({
      positions: result.results ?? [],
      count: result.results?.length ?? 0
    })
  } catch (error) {
    console.error('Failed to query active positions:', error)
    return c.json({ error: 'database_error', message: 'Failed to query positions' }, 500)
  }
})

/**
 * GET /api/positions/history
 * Query position history with optional filters
 */
positions.get('/history', async (c) => {
  const campaignId = c.req.query('campaign_id')
  const positionId = c.req.query('position_id')
  const status = c.req.query('status') as 'open' | 'closed' | 'rolled' | undefined
  const limit = parseInt(c.req.query('limit') ?? '100', 10)

  // Build query dynamically
  let sql = 'SELECT * FROM position_history WHERE 1=1'
  const params: string[] = []

  if (campaignId) {
    sql += ' AND campaign_id = ?'
    params.push(campaignId)
  }

  if (positionId) {
    sql += ' AND position_id = ?'
    params.push(positionId)
  }

  if (status) {
    sql += ' AND status = ?'
    params.push(status)
  }

  sql += ' ORDER BY created_at DESC LIMIT ?'
  params.push(String(limit))

  try {
    const result = await c.env.DB.prepare(sql).bind(...params).all<PositionHistoryRow>()

    return c.json({
      history: result.results ?? [],
      count: result.results?.length ?? 0
    })
  } catch (error) {
    console.error('Failed to query position history:', error)
    return c.json({ error: 'database_error', message: 'Failed to query history' }, 500)
  }
})

/**
 * GET /api/positions/:position_id
 * Get single position by ID
 */
positions.get('/:position_id', async (c) => {
  const positionId = c.req.param('position_id')

  if (!positionId) {
    return c.json({ error: 'invalid_request', message: 'position_id required' }, 400)
  }

  try {
    const row = await c.env.DB.prepare('SELECT * FROM active_positions WHERE position_id = ?')
      .bind(positionId)
      .first<ActivePositionRow>()

    if (!row) {
      return c.json({ error: 'not_found', message: 'Position not found' }, 404)
    }

    return c.json({ position: row })
  } catch (error) {
    console.error('Failed to query position:', error)
    return c.json({ error: 'database_error', message: 'Failed to query position' }, 500)
  }
})

export { positions }
