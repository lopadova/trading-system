/**
 * Unit tests for POST /api/v1/ingest (Phase 7.1 market-data event types).
 *
 * These tests run in the vitest-native environment (no cloudflare:test), so
 * they use an in-memory mock of D1Database that mimics the prepare/bind/run
 * chain. This lets us assert:
 *  - valid payload → 200 + correct SQL invocation
 *  - malformed payload → 400 with Zod issues
 *  - replay of same payload (idempotent) → second call does not duplicate
 *
 * Integration tests against a real D1 instance live in
 * test/phase7-ingest-integration.test.ts.
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { ingest } from '../src/routes/ingest'

// ============================================================================
// In-memory D1 mock
// ============================================================================

type StmtLog = {
  sql: string
  params: unknown[]
}

/**
 * Minimal in-memory table store. Key is the stringified primary key, value is
 * the row. Good enough to prove UPSERT semantics on our new tables.
 */
class FakeTable {
  rows = new Map<string, Record<string, unknown>>()
}

class FakeD1 {
  // Named tables for UPSERT assertions
  tables: Record<string, FakeTable> = {
    account_equity_daily: new FakeTable(),
    market_quotes_daily: new FakeTable(),
    vix_term_structure: new FakeTable(),
    benchmark_series: new FakeTable(),
    position_greeks: new FakeTable(),
    service_heartbeats: new FakeTable(),
    alert_history: new FakeTable(),
    positions_history: new FakeTable(),
    web_vitals: new FakeTable(),
    order_audit_log: new FakeTable()
  }
  log: StmtLog[] = []

  prepare(sql: string) {
    // eslint-disable-next-line @typescript-eslint/no-this-alias
    const db = this
    return {
      _params: [] as unknown[],
      bind(...params: unknown[]) {
        this._params = params
        return this
      },
      async run() {
        db.log.push({ sql, params: this._params })
        db.applyStatement(sql, this._params)
        return { success: true, meta: { duration: 0 } }
      },
      async first() {
        return null
      },
      async all() {
        return { results: [], success: true }
      }
    }
  }

  /**
   * Very small SQL interpreter — only understands the INSERT OR REPLACE
   * statements our new handlers issue. It's enough to prove idempotency.
   */
  applyStatement(sql: string, params: unknown[]) {
    const normalized = sql.replace(/\s+/g, ' ').trim()

    const upsert = (table: string, cols: string[], pkCols: string[]) => {
      const t = this.tables[table]
      if (!t) return
      const row: Record<string, unknown> = {}
      cols.forEach((c, idx) => { row[c] = params[idx] })
      const pk = pkCols.map(c => String(row[c])).join('|')
      t.rows.set(pk, row)
    }

    if (normalized.includes('INSERT OR REPLACE INTO account_equity_daily')) {
      upsert(
        'account_equity_daily',
        ['date', 'account_value', 'cash', 'buying_power', 'margin_used', 'margin_used_pct'],
        ['date']
      )
      return
    }
    if (normalized.includes('INSERT OR REPLACE INTO market_quotes_daily')) {
      upsert(
        'market_quotes_daily',
        ['symbol', 'date', 'open', 'high', 'low', 'close', 'volume'],
        ['symbol', 'date']
      )
      return
    }
    if (normalized.includes('INSERT OR REPLACE INTO vix_term_structure')) {
      upsert(
        'vix_term_structure',
        ['date', 'vix', 'vix1d', 'vix3m', 'vix6m'],
        ['date']
      )
      return
    }
    if (normalized.includes('INSERT OR REPLACE INTO benchmark_series')) {
      upsert(
        'benchmark_series',
        ['symbol', 'date', 'close', 'close_normalized'],
        ['symbol', 'date']
      )
      return
    }
    if (normalized.includes('INSERT OR REPLACE INTO position_greeks')) {
      upsert(
        'position_greeks',
        ['position_id', 'snapshot_ts', 'delta', 'gamma', 'theta', 'vega', 'iv', 'underlying_price'],
        ['position_id', 'snapshot_ts']
      )
      return
    }
    if (normalized.includes('INSERT OR REPLACE INTO web_vitals')) {
      upsert(
        'web_vitals',
        ['session_id', 'name', 'value', 'rating', 'navigation_type', 'metric_id', 'timestamp'],
        ['session_id', 'name', 'timestamp']
      )
      return
    }
    if (normalized.includes('INSERT OR REPLACE INTO order_audit_log')) {
      upsert(
        'order_audit_log',
        [
          'audit_id', 'order_id', 'ts', 'actor', 'strategy_id', 'contract_symbol',
          'side', 'quantity', 'price', 'semaphore_status', 'outcome', 'override_reason',
          'details_json'
        ],
        ['audit_id']
      )
      return
    }
  }
}

// ============================================================================
// Test harness
// ============================================================================

const API_KEY = 'test-key'
const AUTH_HEADERS = { 'X-Api-Key': API_KEY, 'Content-Type': 'application/json' }

let db: FakeD1
let env: { DB: D1Database; API_KEY: string }

beforeEach(() => {
  db = new FakeD1()
  env = { DB: db as unknown as D1Database, API_KEY }
})

async function post(body: unknown, headers: Record<string, string> = AUTH_HEADERS) {
  return ingest.request('/', {
    method: 'POST',
    headers,
    body: JSON.stringify(body)
  }, env)
}

function envelope(event_type: string, payload: unknown, event_id = crypto.randomUUID()) {
  return { event_id, event_type, payload }
}

// ============================================================================
// Auth + envelope smoke
// ============================================================================

describe('POST /api/v1/ingest — envelope guards', () => {
  it('rejects missing X-Api-Key with 401', async () => {
    const res = await post(envelope('account_equity', { date: '2026-04-20' }), {
      'Content-Type': 'application/json'
    })
    expect(res.status).toBe(401)
  })

  it('rejects missing envelope fields with 400', async () => {
    const res = await post({ event_id: 'x', event_type: 'account_equity' })
    expect(res.status).toBe(400)
  })

  it('rejects unknown event_type with 400', async () => {
    const res = await post(envelope('something_weird', {}))
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('invalid_event_type')
  })
})

// ============================================================================
// account_equity
// ============================================================================

describe('POST /api/v1/ingest — account_equity', () => {
  const validPayload = {
    date: '2026-04-20',
    account_value: 120000.5,
    cash: 50000,
    buying_power: 200000,
    margin_used: 15000,
    margin_used_pct: 12.5
  }

  it('accepts valid payload → 200 + row inserted', async () => {
    const res = await post(envelope('account_equity', validPayload))
    expect(res.status).toBe(200)
    expect(db.tables.account_equity_daily.rows.size).toBe(1)
    const row = db.tables.account_equity_daily.rows.get('2026-04-20')!
    expect(row.account_value).toBe(120000.5)
    expect(row.margin_used_pct).toBe(12.5)
  })

  it('rejects malformed payload (missing required field) with 400', async () => {
    const bad = { ...validPayload } as Partial<typeof validPayload>
    delete bad.account_value
    const res = await post(envelope('account_equity', bad))
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string; issues: unknown[] }
    expect(body.error).toBe('invalid_payload')
    expect(body.issues.length).toBeGreaterThan(0)
  })

  it('is idempotent: replaying same date does not create a duplicate row', async () => {
    // Use the same event_id on purpose to simulate a retry
    const env1 = envelope('account_equity', validPayload, 'evt-1')
    await post(env1)
    await post(env1)
    expect(db.tables.account_equity_daily.rows.size).toBe(1)
  })
})

// ============================================================================
// market_quote
// ============================================================================

describe('POST /api/v1/ingest — market_quote', () => {
  const validPayload = {
    symbol: 'SPX',
    date: '2026-04-20',
    open: 5100.1,
    high: 5150.5,
    low: 5095.0,
    close: 5142.3,
    volume: 2_400_000
  }

  it('accepts valid payload → 200 + row inserted', async () => {
    const res = await post(envelope('market_quote', validPayload))
    expect(res.status).toBe(200)
    expect(db.tables.market_quotes_daily.rows.size).toBe(1)
    expect(db.tables.market_quotes_daily.rows.get('SPX|2026-04-20')!.close).toBe(5142.3)
  })

  it('accepts minimal payload (only symbol/date/close)', async () => {
    const res = await post(envelope('market_quote', {
      symbol: 'SPY',
      date: '2026-04-20',
      close: 515.1
    }))
    expect(res.status).toBe(200)
    expect(db.tables.market_quotes_daily.rows.get('SPY|2026-04-20')!.close).toBe(515.1)
  })

  it('rejects malformed payload (close not a number) with 400', async () => {
    const res = await post(envelope('market_quote', {
      ...validPayload,
      close: 'not-a-number'
    }))
    expect(res.status).toBe(400)
  })

  it('is idempotent on (symbol, date) primary key', async () => {
    await post(envelope('market_quote', validPayload, 'evt-mq-1'))
    await post(envelope('market_quote', validPayload, 'evt-mq-1'))
    expect(db.tables.market_quotes_daily.rows.size).toBe(1)
  })
})

// ============================================================================
// vix_snapshot
// ============================================================================

describe('POST /api/v1/ingest — vix_snapshot', () => {
  const validPayload = {
    date: '2026-04-20',
    vix: 15.3,
    vix1d: 14.1,
    vix3m: 17.8,
    vix6m: 18.5
  }

  it('accepts valid payload → 200 + vix_term_structure row + mirrors into market_quotes_daily', async () => {
    const res = await post(envelope('vix_snapshot', validPayload))
    expect(res.status).toBe(200)
    // Curve row
    expect(db.tables.vix_term_structure.rows.size).toBe(1)
    expect(db.tables.vix_term_structure.rows.get('2026-04-20')!.vix3m).toBe(17.8)
    // Mirrored quotes — 4 legs
    expect(db.tables.market_quotes_daily.rows.size).toBe(4)
    expect(db.tables.market_quotes_daily.rows.get('VIX|2026-04-20')!.close).toBe(15.3)
    expect(db.tables.market_quotes_daily.rows.get('VIX3M|2026-04-20')!.close).toBe(17.8)
  })

  it('skips mirror for null/undefined legs', async () => {
    const res = await post(envelope('vix_snapshot', {
      date: '2026-04-20',
      vix: 15.3,
      vix3m: 17.8
      // vix1d and vix6m absent
    }))
    expect(res.status).toBe(200)
    expect(db.tables.market_quotes_daily.rows.size).toBe(2)
    expect(db.tables.market_quotes_daily.rows.has('VIX1D|2026-04-20')).toBe(false)
  })

  it('rejects malformed payload (bad date format) with 400', async () => {
    const res = await post(envelope('vix_snapshot', {
      date: '20/04/2026',
      vix: 15.3
    }))
    expect(res.status).toBe(400)
  })

  it('is idempotent on date PK', async () => {
    await post(envelope('vix_snapshot', validPayload, 'evt-vix-1'))
    await post(envelope('vix_snapshot', validPayload, 'evt-vix-1'))
    expect(db.tables.vix_term_structure.rows.size).toBe(1)
  })
})

// ============================================================================
// benchmark_close
// ============================================================================

describe('POST /api/v1/ingest — benchmark_close', () => {
  const validPayload = {
    symbol: 'SP500',
    date: '2026-04-20',
    close: 5142.3,
    close_normalized: 103.5
  }

  it('accepts valid payload → 200 + row inserted', async () => {
    const res = await post(envelope('benchmark_close', validPayload))
    expect(res.status).toBe(200)
    expect(db.tables.benchmark_series.rows.size).toBe(1)
    const row = db.tables.benchmark_series.rows.get('SP500|2026-04-20')!
    expect(row.close).toBe(5142.3)
    expect(row.close_normalized).toBe(103.5)
  })

  it('accepts payload without close_normalized', async () => {
    const res = await post(envelope('benchmark_close', {
      symbol: 'SWDA',
      date: '2026-04-20',
      close: 82.1
    }))
    expect(res.status).toBe(200)
    expect(db.tables.benchmark_series.rows.get('SWDA|2026-04-20')!.close_normalized).toBeNull()
  })

  it('rejects malformed payload (missing close) with 400', async () => {
    const res = await post(envelope('benchmark_close', {
      symbol: 'SP500',
      date: '2026-04-20'
    }))
    expect(res.status).toBe(400)
  })

  it('is idempotent on (symbol, date)', async () => {
    await post(envelope('benchmark_close', validPayload, 'evt-b-1'))
    await post(envelope('benchmark_close', validPayload, 'evt-b-1'))
    expect(db.tables.benchmark_series.rows.size).toBe(1)
  })
})

// ============================================================================
// position_greeks
// ============================================================================

describe('POST /api/v1/ingest — position_greeks', () => {
  const validPayload = {
    position_id: 'pos-001',
    snapshot_ts: '2026-04-20T14:30:00Z',
    delta: -0.32,
    gamma: 0.015,
    theta: -12.5,
    vega: 45.2,
    iv: 0.18,
    underlying_price: 5142.3
  }

  it('accepts valid payload → 200 + row inserted', async () => {
    const res = await post(envelope('position_greeks', validPayload))
    expect(res.status).toBe(200)
    const row = db.tables.position_greeks.rows.get('pos-001|2026-04-20T14:30:00Z')!
    expect(row.delta).toBe(-0.32)
    expect(row.underlying_price).toBe(5142.3)
  })

  it('accepts payload with only required fields', async () => {
    const res = await post(envelope('position_greeks', {
      position_id: 'pos-002',
      snapshot_ts: '2026-04-20T15:00:00Z'
    }))
    expect(res.status).toBe(200)
    const row = db.tables.position_greeks.rows.get('pos-002|2026-04-20T15:00:00Z')!
    expect(row.delta).toBeNull()
  })

  it('rejects malformed payload (missing position_id) with 400', async () => {
    const res = await post(envelope('position_greeks', {
      snapshot_ts: '2026-04-20T14:30:00Z',
      delta: 0.5
    }))
    expect(res.status).toBe(400)
  })

  it('preserves distinct snapshots for the same position', async () => {
    await post(envelope('position_greeks', validPayload))
    await post(envelope('position_greeks', {
      ...validPayload,
      snapshot_ts: '2026-04-20T14:31:00Z'
    }))
    expect(db.tables.position_greeks.rows.size).toBe(2)
  })

  it('is idempotent on (position_id, snapshot_ts)', async () => {
    await post(envelope('position_greeks', validPayload, 'evt-g-1'))
    await post(envelope('position_greeks', validPayload, 'evt-g-1'))
    expect(db.tables.position_greeks.rows.size).toBe(1)
  })
})

// ============================================================================
// web_vitals (Phase 7.3 dashboard telemetry)
// ============================================================================

describe('POST /api/v1/ingest — web_vitals', () => {
  const validPayload = {
    session_id: 'sess-abc-123',
    name: 'LCP',
    value: 2350.5,
    rating: 'good',
    navigationType: 'navigate',
    id: 'v3-lcp-1',
    timestamp: '2026-04-21T12:00:00.000Z'
  }

  it('accepts valid payload → 200 + row inserted', async () => {
    const res = await post(envelope('web_vitals', validPayload))
    expect(res.status).toBe(200)
    expect(db.tables.web_vitals.rows.size).toBe(1)
    const row = db.tables.web_vitals.rows.get('sess-abc-123|LCP|2026-04-21T12:00:00.000Z')!
    expect(row.value).toBe(2350.5)
    expect(row.rating).toBe('good')
    expect(row.metric_id).toBe('v3-lcp-1')
  })

  it('accepts minimal payload (only session_id/name/value) and auto-fills timestamp', async () => {
    const res = await post(envelope('web_vitals', {
      session_id: 'sess-x',
      name: 'CLS',
      value: 0.05
    }))
    expect(res.status).toBe(200)
    expect(db.tables.web_vitals.rows.size).toBe(1)
  })

  it('rejects invalid metric name with 400', async () => {
    const res = await post(envelope('web_vitals', {
      ...validPayload,
      name: 'NOT_A_METRIC'
    }))
    expect(res.status).toBe(400)
  })

  it('is idempotent on (session_id, name, timestamp)', async () => {
    await post(envelope('web_vitals', validPayload))
    await post(envelope('web_vitals', validPayload))
    expect(db.tables.web_vitals.rows.size).toBe(1)
  })
})

// ============================================================================
// order_audit (Phase 7.4 — order placement audit trail)
// ============================================================================

describe('POST /api/v1/ingest — order_audit', () => {
  const validPayload = {
    audit_id: 'aud-00000000-0000-0000-0000-000000000001',
    order_id: 'ord-abc-123',
    ts: '2026-04-20T13:45:00.000Z',
    actor: 'system',
    strategy_id: 'iron-condor-v1',
    contract_symbol: 'SPX   250321P05000000',
    side: 'SELL',
    quantity: 2,
    price: 12.5,
    semaphore_status: 'green',
    outcome: 'placed',
    override_reason: null,
    details_json: null
  }

  it('accepts valid payload → 200 + row inserted', async () => {
    const res = await post(envelope('order_audit', validPayload))
    expect(res.status).toBe(200)
    expect(db.tables.order_audit_log.rows.size).toBe(1)
    const row = db.tables.order_audit_log.rows.get(validPayload.audit_id)!
    expect(row.outcome).toBe('placed')
    expect(row.contract_symbol).toBe(validPayload.contract_symbol)
    expect(row.quantity).toBe(2)
  })

  it('accepts rejection payload with null order_id and override_reason', async () => {
    const res = await post(envelope('order_audit', {
      ...validPayload,
      audit_id: 'aud-2',
      order_id: null,
      outcome: 'rejected_semaphore',
      override_reason: 'semaphore-red'
    }))
    expect(res.status).toBe(200)
    const row = db.tables.order_audit_log.rows.get('aud-2')!
    expect(row.order_id).toBeNull()
    expect(row.outcome).toBe('rejected_semaphore')
    expect(row.override_reason).toBe('semaphore-red')
  })

  it('rejects unknown outcome with 400', async () => {
    const res = await post(envelope('order_audit', {
      ...validPayload,
      audit_id: 'aud-3',
      outcome: 'rejected_nonsense'
    }))
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string; issues: unknown[] }
    expect(body.error).toBe('invalid_payload')
    expect(body.issues.length).toBeGreaterThan(0)
  })

  it('rejects invalid side with 400', async () => {
    const res = await post(envelope('order_audit', {
      ...validPayload,
      audit_id: 'aud-4',
      side: 'HOLD'
    }))
    expect(res.status).toBe(400)
  })

  it('rejects negative quantity with 400', async () => {
    const res = await post(envelope('order_audit', {
      ...validPayload,
      audit_id: 'aud-5',
      quantity: -1
    }))
    expect(res.status).toBe(400)
  })

  it('is idempotent on audit_id PK', async () => {
    await post(envelope('order_audit', validPayload, 'evt-audit-1'))
    await post(envelope('order_audit', validPayload, 'evt-audit-1'))
    expect(db.tables.order_audit_log.rows.size).toBe(1)
  })
})
