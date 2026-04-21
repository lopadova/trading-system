/**
 * Trading System Cloudflare Worker
 * Hono-based API for querying D1 database
 */

import { Hono } from 'hono'
import { cors } from 'hono/cors'
import type { Env } from './types/env'
import { positions } from './routes/positions'
import { alerts } from './routes/alerts'
import { heartbeats } from './routes/heartbeats'
import { ingest } from './routes/ingest'
import { logs } from './routes/logs'
import { strategiesConvert } from './routes/strategies-convert'
import { botTelegram } from './routes/bot-telegram'
import { botDiscord } from './routes/bot-discord'
// Dashboard-facing aggregate endpoints (Phase 3)
import { performance } from './routes/performance'
import { drawdowns } from './routes/drawdowns'
import { monthlyReturns } from './routes/monthly-returns'
import { risk } from './routes/risk'
import { systemMetrics } from './routes/system-metrics'
import { breakdown } from './routes/breakdown'
import { activity } from './routes/activity'
import { campaignsSummary } from './routes/campaigns-summary'
import { rateLimitMiddleware } from './middleware/rate-limit'

const app = new Hono<{ Bindings: Env }>()

// CORS middleware - allow dashboard origin
app.use(
  '*',
  cors({
    origin: (origin, c) => {
      // Allow configured dashboard origin
      const allowedOrigin = c.env.DASHBOARD_ORIGIN
      if (origin === allowedOrigin) {
        return allowedOrigin
      }
      // Fallback to origin for development (if localhost)
      if (origin?.includes('localhost') || origin?.includes('127.0.0.1')) {
        return origin
      }
      return allowedOrigin
    },
    allowMethods: ['GET', 'POST', 'PATCH', 'DELETE', 'OPTIONS'],
    allowHeaders: ['Content-Type', 'X-Api-Key'],
    exposeHeaders: ['Content-Length', 'X-Request-Id'],
    maxAge: 600,
    credentials: true
  })
)

// Global rate limiting (100 requests per minute per client)
app.use('*', rateLimitMiddleware({ maxRequests: 100, windowMs: 60000 }))

// Mount API routes
app.route('/api/positions', positions)
app.route('/api/alerts', alerts)
app.route('/api/heartbeats', heartbeats)
app.route('/api/v1/ingest', ingest)
app.route('/api/v1/logs', logs)
app.route('/api/v1/strategies', strategiesConvert)

// Dashboard-facing aggregate routes (Phase 3 — deterministic mocks, D1 wiring later)
app.route('/api/performance', performance)
app.route('/api/drawdowns', drawdowns)
app.route('/api/monthly-returns', monthlyReturns)
app.route('/api/risk', risk)
app.route('/api/system', systemMetrics)
// NOTE: mounted at /api/breakdown (NOT /api/positions/breakdown) to avoid the
// /api/positions prefix match which triggers positions.ts's authMiddleware
// before this router is reached. See fix(worker): move breakdown out of
// /api/positions namespace.
app.route('/api/breakdown', breakdown)
app.route('/api/activity', activity)
app.route('/api/campaigns', campaignsSummary)

// Mount bot routes (no rate limiting on webhooks)
app.route('/api/bot', botTelegram)
app.route('/api/bot', botDiscord)

/**
 * GET /api/health
 * Health check endpoint (no auth required)
 */
app.get('/api/health', (c) => {
  return c.json({
    ok: true,
    timestamp: new Date().toISOString(),
    service: 'trading-system-worker',
    version: '1.0.0'
  })
})

/**
 * GET /
 * Root endpoint
 */
app.get('/', (c) => {
  return c.json({
    service: 'trading-system-worker',
    version: '1.0.0',
    endpoints: [
      'GET /api/health',
      'GET /api/positions/active',
      'GET /api/positions/history',
      'GET /api/positions/:position_id',
      'GET /api/breakdown',
      'GET /api/alerts',
      'GET /api/alerts/unresolved',
      'GET /api/alerts/:alert_id',
      'GET /api/alerts/summary-24h',
      'GET /api/heartbeats',
      'GET /api/heartbeats/:service_name',
      'GET /api/heartbeats/stale/:threshold_seconds',
      'GET /api/performance/summary',
      'GET /api/performance/series',
      'GET /api/drawdowns',
      'GET /api/monthly-returns',
      'GET /api/risk/metrics',
      'GET /api/risk/semaphore',
      'GET /api/system/metrics',
      'GET /api/activity/recent',
      'GET /api/campaigns/summary',
      'POST /api/v1/ingest',
      'POST /api/v1/logs',
      'POST /api/v1/strategies/convert-el',
      'POST /api/bot/webhook/telegram',
      'POST /api/bot/webhook/discord'
    ]
  })
})

/**
 * 404 handler
 */
app.notFound((c) => {
  return c.json({ error: 'not_found', message: 'Endpoint not found' }, 404)
})

/**
 * Global error handler
 */
app.onError((err, c) => {
  console.error('Unhandled error:', err)
  return c.json(
    {
      error: 'internal_error',
      message: 'An internal error occurred',
      ...(c.env.DASHBOARD_ORIGIN?.includes('localhost') && { stack: err.stack })
    },
    500
  )
})

export default app
