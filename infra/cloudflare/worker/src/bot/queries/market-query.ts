/**
 * Market Data Query Handler
 * Fetches market data from D1 database
 */

import type { D1Database } from '@cloudflare/workers-types'
import type { MarketData } from '../types'

/**
 * Query market data
 * @param db - D1 database instance
 * @returns Market data
 */
export async function queryMarket(db: D1Database): Promise<MarketData> {
  const now = new Date().toISOString()

  try {
    // Get latest market data
    const latestResult = await db
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

    // Get previous day SPX for change calculation
    const previousResult = await db
      .prepare(
        `SELECT spx_price
        FROM market_data_history
        ORDER BY captured_at DESC
        LIMIT 1 OFFSET 1`
      )
      .first<{
        spx_price: number
      }>()

    // Get last 30 days of IVTS for sparkline
    const sparklineResult = await db
      .prepare(
        `SELECT ivts_ratio
        FROM market_data_history
        ORDER BY captured_at DESC
        LIMIT 30`
      )
      .all<{
        ivts_ratio: number
      }>()

    // Calculate SPX change
    const spxChange =
      latestResult && previousResult
        ? latestResult.spx_price - previousResult.spx_price
        : null

    // Extract sparkline data
    const ivtsSparkline = (sparklineResult.results || [])
      .map((r) => r.ivts_ratio)
      .filter((v) => v !== null)
      .reverse() // Oldest to newest

    return {
      spx: latestResult?.spx_price || null,
      spxChange,
      vix: latestResult?.vix_current || null,
      vix3m: latestResult?.vix3m_current || null,
      ivts: latestResult?.ivts_ratio || null,
      ivtsState: latestResult?.supervisor_state || null,
      ivtsSparkline,
      timestamp: now
    }
  } catch (error) {
    console.error('[BOT] queryMarket error:', error)

    // Return empty data on error
    return {
      spx: null,
      spxChange: null,
      vix: null,
      vix3m: null,
      ivts: null,
      ivtsState: null,
      ivtsSparkline: [],
      timestamp: now
    }
  }
}
