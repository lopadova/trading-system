/**
 * Risk Query Handler
 * Fetches risk metrics from D1 database
 */

import type { D1Database } from '@cloudflare/workers-types'
import type { RiskData } from '../types'

/**
 * Query risk metrics
 * @param db - D1 database instance
 * @returns Risk metrics data
 */
export async function queryRisk(db: D1Database): Promise<RiskData> {
  const now = new Date().toISOString()

  try {
    // Get active campaigns with positions
    const campaignsResult = await db
      .prepare(
        `SELECT
          campaign_id,
          SUM(unrealized_pnl) as pnl
        FROM active_positions
        GROUP BY campaign_id`
      )
      .all<{
        campaign_id: string
        pnl: number
      }>()

    // Get latest market data for SPX
    const marketResult = await db
      .prepare(
        `SELECT spx_price
        FROM market_data_history
        ORDER BY captured_at DESC
        LIMIT 1`
      )
      .first<{
        spx_price: number
      }>()

    // Map campaigns to result format
    // Note: Greek snapshots and strategy definitions would require
    // additional tables that aren't in the current schema
    const campaigns = (campaignsResult.results || []).map((c) => ({
      name: c.campaign_id,
      pnl: c.pnl || null,
      stop: null, // TODO: Get from strategy_state.state_json
      delta: null, // TODO: Get from portfolio_greek_snapshots
      theta: null, // TODO: Get from portfolio_greek_snapshots
      spxCurrent: marketResult?.spx_price || null,
      wingLower: null, // TODO: Get from strategy definition
      wingUpper: null, // TODO: Get from strategy definition
      daysElapsed: null, // TODO: Calculate from campaign start
      maxDays: null // TODO: Get from strategy definition
    }))

    return {
      campaigns,
      timestamp: now
    }
  } catch (error) {
    console.error('[BOT] queryRisk error:', error)

    // Return empty data on error
    return {
      campaigns: [],
      timestamp: now
    }
  }
}
