/**
 * Portfolio Query Handler
 * Fetches portfolio data from D1 database
 */

import type { D1Database } from '@cloudflare/workers-types'
import type { PortfolioData } from '../types'

/**
 * Query portfolio data
 * @param db - D1 database instance
 * @returns Portfolio data
 */
export async function queryPortfolio(db: D1Database): Promise<PortfolioData> {
  const now = new Date().toISOString()

  try {
    // Get active campaigns with PnL
    const campaignsResult = await db
      .prepare(
        `SELECT
          campaign_id,
          strategy_name,
          SUM(unrealized_pnl) as pnl,
          COUNT(*) as positions_count
        FROM active_positions
        GROUP BY campaign_id, strategy_name
        ORDER BY campaign_id`
      )
      .all<{
        campaign_id: string
        strategy_name: string
        pnl: number
        positions_count: number
      }>()

    // Get latest market data for IVTS/VIX
    const marketResult = await db
      .prepare(
        `SELECT
          spx_price,
          vix_current,
          vix3m_current,
          ivts_ratio,
          supervisor_state
        FROM market_data_history
        ORDER BY captured_at DESC
        LIMIT 1`
      )
      .first<{
        spx_price: number
        vix_current: number
        vix3m_current: number
        ivts_ratio: number
        supervisor_state: string
      }>()

    // Get closed campaigns for PnL stats
    // Note: This is a simplified query - actual implementation would need
    // time-based filtering for today/MTD/YTD
    const statsResult = await db
      .prepare(
        `SELECT
          COUNT(*) as total_campaigns,
          SUM(CASE WHEN realized_pnl > 0 THEN 1 ELSE 0 END) as winning_campaigns,
          SUM(realized_pnl) as total_pnl
        FROM position_history
        WHERE status = 'closed'
          AND closed_at >= date('now', '-1 year')`
      )
      .first<{
        total_campaigns: number
        winning_campaigns: number
        total_pnl: number
      }>()

    // Calculate win rate
    const winRate =
      statsResult && statsResult.total_campaigns > 0
        ? (statsResult.winning_campaigns / statsResult.total_campaigns) * 100
        : null

    // Map campaigns to result format
    const activeCampaigns = (campaignsResult.results || []).map((c) => ({
      name: c.campaign_id,
      daysElapsed: 0, // TODO: Calculate from campaign start date
      pnl: c.pnl || 0,
      status: 'monitoring'
    }))

    return {
      pnlToday: 0, // TODO: Calculate from today's executions
      pnlMTD: 0, // TODO: Calculate from this month's executions
      pnlYTD: statsResult?.total_pnl || 0,
      winRate,
      activeCampaigns,
      ivts: marketResult?.ivts_ratio || null,
      ivtsState: marketResult?.supervisor_state || null,
      spx: marketResult?.spx_price || null,
      vix: marketResult?.vix_current || null,
      vix3m: marketResult?.vix3m_current || null,
      timestamp: now
    }
  } catch (error) {
    console.error('[BOT] queryPortfolio error:', error)

    // Return empty data on error
    return {
      pnlToday: null,
      pnlMTD: null,
      pnlYTD: null,
      winRate: null,
      activeCampaigns: [],
      ivts: null,
      ivtsState: null,
      spx: null,
      vix: null,
      vix3m: null,
      timestamp: now
    }
  }
}
