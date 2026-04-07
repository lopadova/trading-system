/**
 * Simple Rate Limiting Middleware
 * Uses in-memory counter (per worker instance)
 * For production, consider using Durable Objects or KV for distributed rate limiting
 */

import { createMiddleware } from 'hono/factory'
import type { Env } from '../types/env'

// In-memory rate limit tracking (per worker instance)
const rateLimitMap = new Map<string, { count: number; resetAt: number }>()

interface RateLimitConfig {
  maxRequests: number
  windowMs: number
}

/**
 * Create rate limiting middleware
 * @param config.maxRequests - Maximum requests per window
 * @param config.windowMs - Time window in milliseconds
 */
export function rateLimitMiddleware(config: RateLimitConfig) {
  return createMiddleware<{ Bindings: Env }>(async (c, next) => {
    const clientId = c.req.header('X-Api-Key') ?? c.req.header('CF-Connecting-IP') ?? 'anonymous'
    const now = Date.now()

    // Get or initialize rate limit entry
    let entry = rateLimitMap.get(clientId)

    if (!entry || now > entry.resetAt) {
      // Initialize or reset window
      entry = { count: 0, resetAt: now + config.windowMs }
      rateLimitMap.set(clientId, entry)
    }

    // Check if limit exceeded
    if (entry.count >= config.maxRequests) {
      const retryAfter = Math.ceil((entry.resetAt - now) / 1000)
      return c.json(
        {
          error: 'rate_limit_exceeded',
          message: 'Too many requests',
          retry_after: retryAfter
        },
        429,
        { 'Retry-After': String(retryAfter) }
      )
    }

    // Increment counter
    entry.count++

    // Clean up old entries (simple cleanup every 1000 requests)
    if (Math.random() < 0.001) {
      for (const [key, value] of rateLimitMap.entries()) {
        if (now > value.resetAt + config.windowMs) {
          rateLimitMap.delete(key)
        }
      }
    }

    await next()
  })
}
