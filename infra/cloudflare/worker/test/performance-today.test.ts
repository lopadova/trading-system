/**
 * Unit tests for GET /api/performance/today (Phase 7.4).
 *
 * Covers the math + null/empty handling — DailyPnLWatcher relies on this
 * endpoint to decide whether to pause trading, so bugs here directly cause
 * either a missed drawdown trigger (false negative, dangerous) or an
 * accidental pause (false positive, annoying).
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { performance as perfRoute } from '../src/routes/performance'

interface EquityRow {
  date: string
  account_value: number
  cash: number
}

class FakeD1 {
  rows: EquityRow[] = []

  prepare(sql: string) {
    // eslint-disable-next-line @typescript-eslint/no-this-alias
    const db = this
    return {
      _params: [] as unknown[],
      bind(...params: unknown[]) { this._params = params; return this },
      async first() { return null },
      async run() { return { success: true, meta: { duration: 0 } } },
      async all() {
        // Both /today and /summary use the same table. /today asks for DESC LIMIT 2.
        const normalized = sql.replace(/\s+/g, ' ').trim()
        if (normalized.includes('ORDER BY date DESC LIMIT 2')) {
          const sorted = [...db.rows].sort((a, b) => a.date < b.date ? 1 : -1)
          return { results: sorted.slice(0, 2), success: true }
        }
        // /summary + /series expect ASC ordering — keep the mock minimal.
        const ascSorted = [...db.rows].sort((a, b) => a.date < b.date ? -1 : 1)
        return { results: ascSorted, success: true }
      }
    }
  }
}

const AUTH_HEADERS = { 'X-Api-Key': 'test-key' }
let db: FakeD1
let env: { DB: D1Database; API_KEY: string }

beforeEach(() => {
  db = new FakeD1()
  env = { DB: db as unknown as D1Database, API_KEY: 'test-key' }
})

async function get(path: string) {
  return perfRoute.request(path, { method: 'GET', headers: AUTH_HEADERS }, env)
}

describe('GET /api/performance/today', () => {
  it('returns zeros when equity history is empty', async () => {
    const res = await get('/today')
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      accountValue: number; cash: number; pnl: number; pnlPct: number; yesterdayClose: number | null
    }
    expect(body.accountValue).toBe(0)
    expect(body.pnl).toBe(0)
    expect(body.pnlPct).toBe(0)
    expect(body.yesterdayClose).toBeNull()
  })

  it('returns accountValue only with no yesterdayClose for single-row history', async () => {
    db.rows = [{ date: '2026-04-20', account_value: 100000, cash: 25000 }]
    const res = await get('/today')
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      accountValue: number; cash: number; pnl: number; pnlPct: number; yesterdayClose: number | null
    }
    expect(body.accountValue).toBe(100000)
    expect(body.cash).toBe(25000)
    expect(body.yesterdayClose).toBeNull()
    expect(body.pnl).toBe(0)
    expect(body.pnlPct).toBe(0)
  })

  it('computes positive pnlPct correctly', async () => {
    db.rows = [
      { date: '2026-04-20', account_value: 102000, cash: 25000 },
      { date: '2026-04-19', account_value: 100000, cash: 25000 },
    ]
    const res = await get('/today')
    const body = (await res.json()) as {
      accountValue: number; pnl: number; pnlPct: number; yesterdayClose: number
    }
    expect(body.accountValue).toBe(102000)
    expect(body.yesterdayClose).toBe(100000)
    expect(body.pnl).toBe(2000)
    expect(body.pnlPct).toBe(2)
  })

  it('computes negative pnlPct correctly (drawdown case)', async () => {
    db.rows = [
      { date: '2026-04-20', account_value: 97500, cash: 25000 },
      { date: '2026-04-19', account_value: 100000, cash: 25000 },
    ]
    const res = await get('/today')
    const body = (await res.json()) as {
      accountValue: number; pnl: number; pnlPct: number; yesterdayClose: number
    }
    expect(body.accountValue).toBe(97500)
    expect(body.yesterdayClose).toBe(100000)
    expect(body.pnl).toBe(-2500)
    expect(body.pnlPct).toBe(-2.5)
  })

  it('handles yesterday=0 without divide-by-zero', async () => {
    db.rows = [
      { date: '2026-04-20', account_value: 100, cash: 0 },
      { date: '2026-04-19', account_value: 0, cash: 0 },
    ]
    const res = await get('/today')
    const body = (await res.json()) as { pnl: number; pnlPct: number; yesterdayClose: number }
    // pnl is still a valid subtraction; pnlPct must short-circuit to 0 not NaN.
    expect(body.pnl).toBe(100)
    expect(body.pnlPct).toBe(0)
    expect(body.yesterdayClose).toBe(0)
  })

  it('rounds pnlPct to 4 decimals', async () => {
    db.rows = [
      { date: '2026-04-20', account_value: 100123.45, cash: 25000 },
      { date: '2026-04-19', account_value: 100000, cash: 25000 },
    ]
    const res = await get('/today')
    const body = (await res.json()) as { pnlPct: number }
    expect(body.pnlPct).toBeCloseTo(0.1234, 4)
  })

  it('rejects missing X-Api-Key with 401', async () => {
    const res = await perfRoute.request('/today', { method: 'GET' }, env)
    expect(res.status).toBe(401)
  })
})
