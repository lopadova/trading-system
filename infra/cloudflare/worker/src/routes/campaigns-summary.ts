/**
 * Campaigns Summary API Route
 * Returns aggregate counts of campaigns by state for the Overview card.
 *
 * NOTE: Phase 3 returns deterministic mock data. Real D1 aggregation of the
 * `campaigns` table is out of scope here and will be handled in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { CampaignsSummary } from '../types/api'
import { authMiddleware } from '../middleware/auth'

export const campaignsSummary = new Hono<{ Bindings: Env }>()

// All campaigns-summary routes require authentication
campaignsSummary.use('*', authMiddleware)

campaignsSummary.get('/summary', (c) => {
  const payload: CampaignsSummary = {
    active: 2,
    paused: 1,
    draft: 1,
    detail: '1 paused · 1 draft',
  }
  return c.json(payload)
})
