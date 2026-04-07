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
      'GET /api/alerts',
      'GET /api/alerts/unresolved',
      'GET /api/alerts/:alert_id',
      'GET /api/heartbeats',
      'GET /api/heartbeats/:service_name',
      'GET /api/heartbeats/stale/:threshold_seconds'
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
