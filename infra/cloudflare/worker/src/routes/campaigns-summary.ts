/**
 * Campaigns Summary API Route
 * Returns aggregate counts of campaigns by state for the Overview card.
 *
 * Phase 7.2 — Replaces the hardcoded counts with a real D1 aggregation:
 *   SELECT state, COUNT(*) FROM campaigns GROUP BY state
 *
 * The `detail` string is built from the non-active counts (paused/draft) to
 * preserve the pre-Phase-7.2 display format. When the `campaigns` table is
 * empty we return zeros with the friendly "no campaigns" detail — this is a
 * legitimate "empty state" and does NOT set fallback-mock.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { CampaignsSummary } from '../types/api'
import { authMiddleware } from '../middleware/auth'

export const campaignsSummary = new Hono<{ Bindings: Env }>()

// All campaigns-summary routes require authentication
campaignsSummary.use('*', authMiddleware)

interface StateCountRow {
  state: string
  count: number
}

campaignsSummary.get('/summary', async (c) => {
  try {
    const res = await c.env.DB
      .prepare('SELECT state, COUNT(*) AS count FROM campaigns GROUP BY state')
      .all<StateCountRow>()
    const rows = res.results ?? []

    const counts: Record<string, number> = { active: 0, paused: 0, draft: 0, closed: 0 }
    for (const row of rows) {
      counts[row.state] = (counts[row.state] ?? 0) + row.count
    }

    const total = (counts.active ?? 0) + (counts.paused ?? 0) + (counts.draft ?? 0) + (counts.closed ?? 0)

    // Empty state: no campaigns of any kind. Not a fallback-mock condition —
    // zero campaigns is a valid production state on a fresh install.
    if (total === 0) {
      return c.json<CampaignsSummary>({
        active: 0,
        paused: 0,
        draft: 0,
        detail: 'no campaigns',
      })
    }

    const detailParts: string[] = []
    if ((counts.paused ?? 0) > 0) detailParts.push(`${counts.paused} paused`)
    if ((counts.draft ?? 0) > 0) detailParts.push(`${counts.draft} draft`)
    const detail = detailParts.length > 0 ? detailParts.join(' · ') : 'all active'

    const payload: CampaignsSummary = {
      active: counts.active ?? 0,
      paused: counts.paused ?? 0,
      draft: counts.draft ?? 0,
      detail,
    }
    return c.json(payload)
  } catch (error) {
    console.error('campaigns/summary query failed:', error)
    // On DB error, surface a literal zero payload with the fallback-mock
    // header so the dashboard can show "demo data" / "unavailable". DO NOT
    // return plausible mock counts here — that would silently mislead
    // operators about campaign state when the DB is actually broken.
    c.header('X-Data-Source', 'fallback-mock')
    return c.json<CampaignsSummary>({
      active: 0,
      paused: 0,
      draft: 0,
      detail: 'data unavailable',
    })
  }
})
