/**
 * Campaigns Query Handler
 * Fetches active campaigns from D1 database
 */

import type { D1Database } from '@cloudflare/workers-types'
import type { CampaignsData } from '../types'

/**
 * Query campaigns
 * @param db - D1 database instance
 * @returns Campaigns data
 */
export async function queryCampaigns(db: D1Database): Promise<CampaignsData> {
  const now = new Date().toISOString()

  try {
    // Get active campaigns with position counts
    const campaignsResult = await db
      .prepare(
        `SELECT
          campaign_id,
          strategy_name,
          COUNT(*) as positions_count,
          SUM(unrealized_pnl) as pnl
        FROM active_positions
        GROUP BY campaign_id, strategy_name
        ORDER BY campaign_id`
      )
      .all<{
        campaign_id: string
        strategy_name: string
        positions_count: number
        pnl: number
      }>()

    // Map campaigns to result format
    const campaigns = (campaignsResult.results || []).map((c) => ({
      id: c.campaign_id,
      name: c.campaign_id,
      status: 'monitoring', // TODO: Get actual status from campaign_states table
      positionsCount: c.positions_count,
      pnl: c.pnl || null,
      daysElapsed: 0 // TODO: Calculate from campaign start date
    }))

    return {
      campaigns,
      timestamp: now
    }
  } catch (error) {
    console.error('[BOT] queryCampaigns error:', error)

    // Return empty data on error
    return {
      campaigns: [],
      timestamp: now
    }
  }
}
