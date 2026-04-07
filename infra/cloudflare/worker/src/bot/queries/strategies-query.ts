/**
 * Strategies Query Handler
 * Fetches active strategies from D1 database
 */

import type { D1Database } from '@cloudflare/workers-types'
import type { StrategiesData } from '../types'

/**
 * Query strategies
 * @param db - D1 database instance
 * @returns Strategies data
 */
export async function queryStrategies(db: D1Database): Promise<StrategiesData> {
  const now = new Date().toISOString()

  try {
    // Get strategy states
    const strategiesResult = await db
      .prepare(
        `SELECT
          strategy_name,
          updated_at
        FROM strategy_state
        ORDER BY strategy_name`
      )
      .all<{
        strategy_name: string
        updated_at: string
      }>()

    // Map strategies to result format
    const strategies = (strategiesResult.results || []).map((s) => ({
      name: s.strategy_name,
      status: 'active', // TODO: Parse from state_json
      lastSignal: s.updated_at
    }))

    return {
      strategies,
      timestamp: now
    }
  } catch (error) {
    console.error('[BOT] queryStrategies error:', error)

    // Return empty data on error
    return {
      strategies: [],
      timestamp: now
    }
  }
}
