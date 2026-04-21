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

  // Build query dynamically based on filters.
  // LEFT JOIN to campaigns lets the dashboard show the human-readable campaign
  // name without requiring a separate round-trip. campaigns is optional — if
  // the row has no matching campaign, `campaign` will be NULL.
  //
  // Phase 7.2: also LEFT JOIN the latest row of `position_greeks` per position
  // so the Active Positions table can render fresh delta/theta without a
  // separate round-trip. The CTE `latest_greeks` is SCOPED to active positions
  // (inner-joined to `active_positions`) so the cost scales with open-book
  // size rather than total greek history — without this scope it would
  // degenerate into a full-table scan of position_greeks per request as the
  // snapshot history grows.
  let sql =
    'WITH latest_greeks AS (' +
    '  SELECT pg.position_id, MAX(pg.snapshot_ts) AS max_ts ' +
    '  FROM position_greeks pg ' +
    '  INNER JOIN active_positions ap ON ap.position_id = pg.position_id ' +
    '  GROUP BY pg.position_id' +
    ') ' +
    'SELECT p.*, c.name AS campaign, ' +
    '       g.delta AS delta, g.gamma AS gamma, g.theta AS theta, ' +
    '       g.vega AS vega, g.iv AS iv ' +
    'FROM active_positions p ' +
    'LEFT JOIN campaigns c ON c.id = p.campaign_id ' +
    'LEFT JOIN latest_greeks lg ON lg.position_id = p.position_id ' +
    'LEFT JOIN position_greeks g ON g.position_id = p.position_id ' +
    '  AND g.snapshot_ts = lg.max_ts ' +
    'WHERE 1=1'
  const params: string[] = []

  if (campaignId) {
    sql += ' AND p.campaign_id = ?'
    params.push(campaignId)
  }

  if (symbol) {
    sql += ' AND p.symbol = ?'
    params.push(symbol)
  }

  if (strategyName) {
    sql += ' AND p.strategy_name = ?'
    params.push(strategyName)
  }

  sql += ' ORDER BY p.opened_at DESC LIMIT ?'
  params.push(String(limit))

  try {
    // Each row carries the joined `campaign` string (or null when no FK match)
    // plus the 5 greek fields (delta/gamma/theta/vega/iv) — all nullable when
    // the position has no snapshot yet in `position_greeks`.
    const result = await c.env.DB.prepare(sql).bind(...params).all<
      ActivePositionRow & {
        campaign: string | null
        delta: number | null
        gamma: number | null
        theta: number | null
        vega: number | null
        iv: number | null
      }
    >()

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
    // Phase 7.2: include latest greeks via the same latest_greeks CTE used by
    // /active (scoped to the specific positionId we're fetching, so the CTE
    // work stays O(1) per detail request regardless of total greek history).
    const row = await c.env.DB
      .prepare(
        'WITH latest_greeks AS (' +
        '  SELECT position_id, MAX(snapshot_ts) AS max_ts ' +
        '  FROM position_greeks WHERE position_id = ? GROUP BY position_id' +
        ') ' +
        'SELECT p.*, c.name AS campaign, ' +
        '       g.delta AS delta, g.gamma AS gamma, g.theta AS theta, ' +
        '       g.vega AS vega, g.iv AS iv ' +
        'FROM active_positions p ' +
        'LEFT JOIN campaigns c ON c.id = p.campaign_id ' +
        'LEFT JOIN latest_greeks lg ON lg.position_id = p.position_id ' +
        'LEFT JOIN position_greeks g ON g.position_id = p.position_id ' +
        '  AND g.snapshot_ts = lg.max_ts ' +
        'WHERE p.position_id = ?'
      )
      .bind(positionId, positionId)
      .first<
        ActivePositionRow & {
          campaign: string | null
          delta: number | null
          gamma: number | null
          theta: number | null
          vega: number | null
          iv: number | null
        }
      >()

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
