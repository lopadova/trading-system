/**
 * Status Query Handler
 * Fetches service health status from D1 database
 */

import type { D1Database } from '@cloudflare/workers-types'
import type { StatusData } from '../types'

/**
 * Query service status
 * @param db - D1 database instance
 * @returns Service status data
 */
export async function queryStatus(db: D1Database): Promise<StatusData> {
  const now = new Date().toISOString()

  try {
    // Get latest heartbeats for all services
    const heartbeatsResult = await db
      .prepare(
        `SELECT
          service_name,
          last_seen_at,
          trading_mode
        FROM service_heartbeats
        ORDER BY service_name`
      )
      .all<{
        service_name: string
        last_seen_at: string
        trading_mode: string
      }>()

    // Calculate age in minutes for each heartbeat
    const services = (heartbeatsResult.results || []).map((h) => {
      let ageMinutes: number | null = null
      let status: 'running' | 'stopped' | 'unknown' = 'unknown'

      if (h.last_seen_at) {
        const lastSeen = new Date(h.last_seen_at)
        const currentTime = new Date()
        ageMinutes = (currentTime.getTime() - lastSeen.getTime()) / 1000 / 60

        // Determine status based on age
        if (ageMinutes < 10) {
          status = 'running'
        } else {
          status = 'stopped'
        }
      }

      return {
        name: h.service_name,
        status,
        lastHeartbeat: h.last_seen_at,
        ageMinutes
      }
    })

    return {
      services,
      timestamp: now
    }
  } catch (error) {
    console.error('[BOT] queryStatus error:', error)

    // Return empty data on error
    return {
      services: [],
      timestamp: now
    }
  }
}
