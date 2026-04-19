/**
 * Ingest API Route
 * POST endpoint for receiving events from TradingSupervisorService outbox
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import { authMiddleware } from '../middleware/auth'

const ingest = new Hono<{ Bindings: Env }>()

// All routes require authentication
ingest.use('*', authMiddleware)

/**
 * POST /api/v1/ingest
 * Receives outbox events from TradingSupervisorService and inserts into D1
 *
 * Expected payload:
 * {
 *   event_id: string,
 *   event_type: 'heartbeat' | 'alert' | 'position',
 *   payload: { ... event-specific data }
 * }
 */
ingest.post('/', async (c) => {
  try {
    const body = await c.req.json()

    // Validate required fields
    if (!body.event_id || !body.event_type || !body.payload) {
      return c.json(
        {
          error: 'invalid_request',
          message: 'Missing required fields: event_id, event_type, payload'
        },
        400
      )
    }

    const { event_id, event_type, payload } = body

    // Route to appropriate handler based on event_type
    switch (event_type) {
      case 'heartbeat':
        await handleHeartbeat(c.env.DB, event_id, payload)
        break

      case 'alert':
        await handleAlert(c.env.DB, event_id, payload)
        break

      case 'position':
        await handlePosition(c.env.DB, event_id, payload)
        break

      default:
        return c.json(
          {
            error: 'invalid_event_type',
            message: `Unknown event_type: ${event_type}`
          },
          400
        )
    }

    return c.json({
      success: true,
      event_id,
      event_type,
      message: 'Event ingested successfully'
    })

  } catch (error) {
    console.error('Failed to ingest event:', error)
    return c.json(
      {
        error: 'ingest_error',
        message: error instanceof Error ? error.message : 'Unknown error'
      },
      500
    )
  }
})

/**
 * Handle heartbeat event - upsert into service_heartbeats table
 */
async function handleHeartbeat(db: D1Database, eventId: string, payload: any) {
  const sql = `
    INSERT INTO service_heartbeats
      (service_name, hostname, last_seen_at, uptime_seconds,
       cpu_percent, ram_percent, disk_free_gb, trading_mode,
       version, created_at, updated_at)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    ON CONFLICT(service_name) DO UPDATE SET
      hostname = excluded.hostname,
      last_seen_at = excluded.last_seen_at,
      uptime_seconds = excluded.uptime_seconds,
      cpu_percent = excluded.cpu_percent,
      ram_percent = excluded.ram_percent,
      disk_free_gb = excluded.disk_free_gb,
      trading_mode = excluded.trading_mode,
      version = excluded.version,
      updated_at = CURRENT_TIMESTAMP
  `

  await db.prepare(sql)
    .bind(
      payload.serviceName,
      payload.hostname,
      payload.lastSeenAt,
      payload.uptimeSeconds,
      payload.cpuPercent,
      payload.ramPercent,
      payload.diskFreeGb,
      payload.tradingMode,
      payload.version,
      payload.createdAt,
      payload.updatedAt
    )
    .run()

  console.log(`✓ Heartbeat ingested: ${payload.serviceName} (event ${eventId})`)
}

/**
 * Handle alert event - insert into alert_history table
 */
async function handleAlert(db: D1Database, eventId: string, payload: any) {
  const sql = `
    INSERT INTO alert_history
      (alert_id, alert_type, severity, message, details_json,
       source_service, created_at, resolved_at, resolved_by)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
  `

  await db.prepare(sql)
    .bind(
      payload.alertId,
      payload.alertType,
      payload.severity,
      payload.message,
      payload.detailsJson,
      payload.sourceService,
      payload.createdAt,
      payload.resolvedAt,
      payload.resolvedBy
    )
    .run()

  console.log(`✓ Alert ingested: ${payload.alertType} (event ${eventId})`)
}

/**
 * Handle position event - insert into positions_history table
 */
async function handlePosition(db: D1Database, eventId: string, payload: any) {
  const sql = `
    INSERT INTO positions_history
      (position_id, contract_symbol, quantity, cost_basis_avg,
       current_price, unrealized_pnl, realized_pnl, opened_at,
       closed_at, status, created_at)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
  `

  await db.prepare(sql)
    .bind(
      payload.positionId,
      payload.contractSymbol,
      payload.quantity,
      payload.costBasisAvg,
      payload.currentPrice,
      payload.unrealizedPnl,
      payload.realizedPnl,
      payload.openedAt,
      payload.closedAt,
      payload.status,
      payload.createdAt
    )
    .run()

  console.log(`✓ Position ingested: ${payload.contractSymbol} (event ${eventId})`)
}

export { ingest }
