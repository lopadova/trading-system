/**
 * Activity Feed API Route
 * Returns the most recent trading events for the Overview activity panel.
 *
 * NOTE: Phase 3 returns deterministic mock data. Real D1-sourced event stream
 * (from execution_log + alert_history + campaign state changes) is out of scope
 * and will be handled in a later phase.
 */

import { Hono } from 'hono'
import type { Env } from '../types/env'
import type { ActivityResponse, ActivityEvent } from '../types/api'

// ---------------------------------------------------------------------------
// Mock events — ordered most-recent first
// ---------------------------------------------------------------------------
const EVENTS: ActivityEvent[] = [
  { id: 'a1', icon: 'check-circle-2', tone: 'green',  title: 'Order filled',           subtitle: 'SPY 450C × 3 @ $12.40',          timestamp: new Date(Date.now() -   2 * 60_000).toISOString() },
  { id: 'a2', icon: 'alert-triangle', tone: 'yellow', title: 'Delta breach warning',   subtitle: 'SPY Iron Condor · short call 0.37', timestamp: new Date(Date.now() -   6 * 60_000).toISOString() },
  { id: 'a3', icon: 'play',           tone: 'blue',   title: 'Campaign resumed',       subtitle: 'IWM Volatility Harvest',         timestamp: new Date(Date.now() -  24 * 60_000).toISOString() },
  { id: 'a4', icon: 'x-circle',       tone: 'red',    title: 'Order rejected',         subtitle: 'QQQ 395C — insufficient margin', timestamp: new Date(Date.now() -  38 * 60_000).toISOString() },
  { id: 'a5', icon: 'repeat',         tone: 'purple', title: 'Position rolled',        subtitle: 'SPY 450P/445P → next week',      timestamp: new Date(Date.now() -  72 * 60_000).toISOString() },
  { id: 'a6', icon: 'trending-up',    tone: 'green',  title: 'Take-profit hit',        subtitle: 'IWM 195C closed @ +$94.50',      timestamp: new Date(Date.now() - 124 * 60_000).toISOString() },
  { id: 'a7', icon: 'refresh-cw',     tone: 'blue',   title: 'IBKR reconnected',       subtitle: 'after 4.2s drop',                timestamp: new Date(Date.now() - 201 * 60_000).toISOString() },
  { id: 'a8', icon: 'file-text',      tone: 'muted',  title: 'Daily report exported',  subtitle: 'pnl_2026-04-19.csv',             timestamp: new Date(Date.now() -  24 * 60 * 60_000).toISOString() },
]

export const activity = new Hono<{ Bindings: Env }>()

activity.get('/recent', (c) => {
  const rawLimit = Number(c.req.query('limit') ?? 8)
  // Clamp limit to [1, 50] — guard against NaN and absurd values
  const safeLimit = Number.isFinite(rawLimit) ? Math.max(1, Math.min(rawLimit, 50)) : 8

  const payload: ActivityResponse = { events: EVENTS.slice(0, safeLimit) }
  return c.json(payload)
})
