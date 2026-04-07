/**
 * API Key Authentication Middleware
 * Validates X-Api-Key header against configured secret
 */

import { createMiddleware } from 'hono/factory'
import type { Env } from '../types/env'

export const authMiddleware = createMiddleware<{ Bindings: Env }>(async (c, next) => {
  const apiKey = c.req.header('X-Api-Key')

  // Check if API key is provided
  if (!apiKey) {
    return c.json({ error: 'missing_api_key', message: 'X-Api-Key header required' }, 401)
  }

  // Validate API key against environment secret
  if (apiKey !== c.env.API_KEY) {
    return c.json({ error: 'invalid_api_key', message: 'Invalid API key' }, 401)
  }

  // API key is valid, proceed to route handler
  await next()
})
