/**
 * Phase 7.1 end-to-end integration tests for the 5 new ingest event types.
 *
 * Uses @cloudflare/vitest-pool-workers to exercise the real Worker against a
 * real in-process D1 instance. For each event type:
 *   1. POST a valid envelope with the correct API key
 *   2. Expect 200 + success envelope
 *   3. Query D1 directly to confirm the row landed (proves the handler
 *      actually wrote through to SQLite and not just a mock)
 *
 * NOTE: vitest-native unit config excludes test/integration/**; this file is
 * picked up only by vitest.integration.config.ts (bun run test:integration).
 * The unit-only mock-backed counterpart lives in test/ingest.test.ts.
 */

import { describe, it, expect, beforeAll } from 'vitest'
import { env, SELF } from 'cloudflare:test'

const authHeaders = (): Record<string, string> => ({
  'Content-Type': 'application/json',
  'X-Api-Key': env.API_KEY
})

// Helper to wrap a payload in the envelope the ingest route expects.
function body(event_type: string, payload: unknown, event_id?: string) {
  return JSON.stringify({
    event_id: event_id ?? crypto.randomUUID(),
    event_type,
    payload
  })
}

describe('Phase 7.1 ingest integration', () => {
  beforeAll(async () => {
    // Create the 5 new tables in the test D1. In production this is handled
    // by wrangler d1 migrations apply; in tests we inline the DDL so the
    // suite is self-contained.
    await env.DB.exec(
      'CREATE TABLE IF NOT EXISTS account_equity_daily (' +
        'date TEXT PRIMARY KEY, ' +
        'account_value REAL NOT NULL, ' +
        'cash REAL NOT NULL, ' +
        'buying_power REAL NOT NULL, ' +
        'margin_used REAL NOT NULL, ' +
        'margin_used_pct REAL NOT NULL, ' +
        'created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP' +
      ')'
    )
    await env.DB.exec(
      'CREATE TABLE IF NOT EXISTS market_quotes_daily (' +
        'symbol TEXT NOT NULL, ' +
        'date TEXT NOT NULL, ' +
        'open REAL, high REAL, low REAL, ' +
        'close REAL NOT NULL, ' +
        'volume INTEGER, ' +
        'created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, ' +
        'PRIMARY KEY (symbol, date)' +
      ')'
    )
    await env.DB.exec(
      'CREATE TABLE IF NOT EXISTS vix_term_structure (' +
        'date TEXT PRIMARY KEY, ' +
        'vix REAL, vix1d REAL, vix3m REAL, vix6m REAL, ' +
        'created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP' +
      ')'
    )
    await env.DB.exec(
      'CREATE TABLE IF NOT EXISTS benchmark_series (' +
        'symbol TEXT NOT NULL, ' +
        'date TEXT NOT NULL, ' +
        'close REAL NOT NULL, ' +
        'close_normalized REAL, ' +
        'PRIMARY KEY (symbol, date)' +
      ')'
    )
    await env.DB.exec(
      'CREATE TABLE IF NOT EXISTS position_greeks (' +
        'position_id TEXT NOT NULL, ' +
        'snapshot_ts TEXT NOT NULL, ' +
        'delta REAL, gamma REAL, theta REAL, vega REAL, iv REAL, ' +
        'underlying_price REAL, ' +
        'created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, ' +
        'PRIMARY KEY (position_id, snapshot_ts)' +
      ')'
    )
  })

  it('account_equity: POST inserts row into account_equity_daily', async () => {
    const res = await SELF.fetch('https://example.com/api/v1/ingest', {
      method: 'POST',
      headers: authHeaders(),
      body: body('account_equity', {
        date: '2026-04-19',
        account_value: 150000,
        cash: 60000,
        buying_power: 250000,
        margin_used: 20000,
        margin_used_pct: 13.33
      })
    })
    expect(res.status).toBe(200)

    const row = await env.DB
      .prepare('SELECT account_value, margin_used_pct FROM account_equity_daily WHERE date = ?')
      .bind('2026-04-19')
      .first<{ account_value: number; margin_used_pct: number }>()

    expect(row).toBeTruthy()
    expect(row?.account_value).toBe(150000)
    expect(row?.margin_used_pct).toBeCloseTo(13.33)
  })

  it('market_quote: POST inserts row into market_quotes_daily', async () => {
    const res = await SELF.fetch('https://example.com/api/v1/ingest', {
      method: 'POST',
      headers: authHeaders(),
      body: body('market_quote', {
        symbol: 'SPX',
        date: '2026-04-19',
        open: 5100.0,
        high: 5150.5,
        low: 5090.0,
        close: 5140.2,
        volume: 2500000
      })
    })
    expect(res.status).toBe(200)

    const row = await env.DB
      .prepare('SELECT close, volume FROM market_quotes_daily WHERE symbol = ? AND date = ?')
      .bind('SPX', '2026-04-19')
      .first<{ close: number; volume: number }>()

    expect(row?.close).toBeCloseTo(5140.2)
    expect(row?.volume).toBe(2500000)
  })

  it('vix_snapshot: POST inserts row in vix_term_structure + mirrors into market_quotes_daily', async () => {
    const res = await SELF.fetch('https://example.com/api/v1/ingest', {
      method: 'POST',
      headers: authHeaders(),
      body: body('vix_snapshot', {
        date: '2026-04-19',
        vix: 15.2,
        vix1d: 14.1,
        vix3m: 17.9,
        vix6m: 18.6
      })
    })
    expect(res.status).toBe(200)

    const curve = await env.DB
      .prepare('SELECT vix, vix3m FROM vix_term_structure WHERE date = ?')
      .bind('2026-04-19')
      .first<{ vix: number; vix3m: number }>()

    expect(curve?.vix).toBeCloseTo(15.2)
    expect(curve?.vix3m).toBeCloseTo(17.9)

    const mirrored = await env.DB
      .prepare(
        "SELECT symbol, close FROM market_quotes_daily " +
        "WHERE date = ? AND symbol IN ('VIX','VIX1D','VIX3M','VIX6M') " +
        "ORDER BY symbol"
      )
      .bind('2026-04-19')
      .all<{ symbol: string; close: number }>()

    expect(mirrored.results.length).toBe(4)
    const bySymbol = Object.fromEntries(mirrored.results.map(r => [r.symbol, r.close]))
    expect(bySymbol.VIX).toBeCloseTo(15.2)
    expect(bySymbol.VIX3M).toBeCloseTo(17.9)
  })

  it('benchmark_close: POST inserts row into benchmark_series', async () => {
    const res = await SELF.fetch('https://example.com/api/v1/ingest', {
      method: 'POST',
      headers: authHeaders(),
      body: body('benchmark_close', {
        symbol: 'SP500',
        date: '2026-04-19',
        close: 5140.2,
        close_normalized: 103.8
      })
    })
    expect(res.status).toBe(200)

    const row = await env.DB
      .prepare('SELECT close, close_normalized FROM benchmark_series WHERE symbol = ? AND date = ?')
      .bind('SP500', '2026-04-19')
      .first<{ close: number; close_normalized: number }>()

    expect(row?.close).toBeCloseTo(5140.2)
    expect(row?.close_normalized).toBeCloseTo(103.8)
  })

  it('position_greeks: POST inserts row into position_greeks', async () => {
    const res = await SELF.fetch('https://example.com/api/v1/ingest', {
      method: 'POST',
      headers: authHeaders(),
      body: body('position_greeks', {
        position_id: 'pos-integ-001',
        snapshot_ts: '2026-04-19T14:30:00Z',
        delta: -0.31,
        gamma: 0.012,
        theta: -11.5,
        vega: 44.1,
        iv: 0.172,
        underlying_price: 5140.2
      })
    })
    expect(res.status).toBe(200)

    const row = await env.DB
      .prepare('SELECT delta, underlying_price FROM position_greeks WHERE position_id = ? AND snapshot_ts = ?')
      .bind('pos-integ-001', '2026-04-19T14:30:00Z')
      .first<{ delta: number; underlying_price: number }>()

    expect(row?.delta).toBeCloseTo(-0.31)
    expect(row?.underlying_price).toBeCloseTo(5140.2)
  })

  it('idempotency: same account_equity event replayed does not duplicate', async () => {
    const eventId = 'evt-replay-1'
    const payload = {
      date: '2026-04-18',
      account_value: 200000,
      cash: 80000,
      buying_power: 300000,
      margin_used: 30000,
      margin_used_pct: 15.0
    }
    const r1 = await SELF.fetch('https://example.com/api/v1/ingest', {
      method: 'POST', headers: authHeaders(), body: body('account_equity', payload, eventId)
    })
    const r2 = await SELF.fetch('https://example.com/api/v1/ingest', {
      method: 'POST', headers: authHeaders(), body: body('account_equity', payload, eventId)
    })
    expect(r1.status).toBe(200)
    expect(r2.status).toBe(200)

    const count = await env.DB
      .prepare('SELECT COUNT(*) as c FROM account_equity_daily WHERE date = ?')
      .bind('2026-04-18')
      .first<{ c: number }>()
    expect(count?.c).toBe(1)
  })
})
