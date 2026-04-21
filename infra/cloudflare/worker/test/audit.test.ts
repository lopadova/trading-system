/**
 * Unit tests for GET /api/audit/orders (Phase 7.4).
 *
 * Uses a tiny in-memory D1 mock that understands the SELECT issued by the
 * audit router so we can assert filter + limit + ordering behavior without
 * spinning a real D1 instance.
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { audit } from '../src/routes/audit'

// ----------------------------------------------------------------------------
// In-memory D1 mock — only the /api/audit/orders SELECT path is interpreted.
// ----------------------------------------------------------------------------

interface AuditRow {
  audit_id: string
  order_id: string | null
  ts: string
  actor: string
  strategy_id: string | null
  contract_symbol: string
  side: string
  quantity: number
  price: number | null
  semaphore_status: string
  outcome: string
  override_reason: string | null
  details_json: string | null
  created_at: string
}

const SEED: AuditRow[] = [
  row({ audit_id: 'a1', ts: '2026-04-19T10:00:00.000Z', outcome: 'placed', order_id: 'o1' }),
  row({ audit_id: 'a2', ts: '2026-04-20T10:00:00.000Z', outcome: 'rejected_semaphore', order_id: null, override_reason: 'semaphore-red' }),
  row({ audit_id: 'a3', ts: '2026-04-20T11:00:00.000Z', outcome: 'placed', order_id: 'o3' }),
  row({ audit_id: 'a4', ts: '2026-04-20T12:00:00.000Z', outcome: 'rejected_breaker', order_id: null, override_reason: 'breaker-open' }),
  row({ audit_id: 'a5', ts: '2026-04-20T13:00:00.000Z', outcome: 'filled', order_id: 'o5', price: 12.5 }),
]

function row(overrides: Partial<AuditRow>): AuditRow {
  return {
    audit_id: 'a?',
    order_id: null,
    ts: '2026-04-20T00:00:00.000Z',
    actor: 'system',
    strategy_id: 'strat-x',
    contract_symbol: 'SPX',
    side: 'SELL',
    quantity: 1,
    price: null,
    semaphore_status: 'green',
    outcome: 'placed',
    override_reason: null,
    details_json: null,
    created_at: '2026-04-20T00:00:00.000Z',
    ...overrides,
  }
}

class FakeD1 {
  rows: AuditRow[] = [...SEED]

  prepare(sql: string) {
    // eslint-disable-next-line @typescript-eslint/no-this-alias
    const db = this
    return {
      _params: [] as unknown[],
      bind(...params: unknown[]) {
        this._params = params
        return this
      },
      async first() { return null },
      async run() { return { success: true, meta: { duration: 0 } } },
      async all() {
        // Parse the SELECT issued by audit.ts. The shape is stable:
        //   ... WHERE [strftime('%Y-%m-%dT%H:%M:%fZ', ts) >= strftime('%Y-%m-%dT%H:%M:%fZ', ?)]
        //         [AND outcome = ?] ORDER BY ts DESC, audit_id DESC LIMIT ?
        // The strftime wrapper normalizes .NET "O" format and JS toISOString() to the
        // same canonical millisecond-precision UTC form before comparison.
        const normalized = sql.replace(/\s+/g, ' ').trim()
        if (!normalized.includes('FROM order_audit_log')) return { results: [], success: true }

        const params = [...this._params]
        // Peek the WHERE substring to know which filters were attached.
        const hasFrom = normalized.includes("strftime('%Y-%m-%dT%H:%M:%fZ', ts)")
        const hasOutcome = normalized.includes('outcome =')

        let fromBind: string | undefined
        let outcomeBind: string | undefined
        // Binds arrive in the order filters were appended; LIMIT is always last.
        if (hasFrom) fromBind = String(params.shift())
        if (hasOutcome) outcomeBind = String(params.shift())
        const limitBind = Number(params.shift() ?? 50)

        let filtered = db.rows.slice()
        if (fromBind !== undefined) filtered = filtered.filter(r => r.ts >= fromBind!)
        if (outcomeBind !== undefined) filtered = filtered.filter(r => r.outcome === outcomeBind)

        // Sort ts DESC, audit_id DESC (same as SQL)
        filtered.sort((a, b) => {
          if (a.ts !== b.ts) return a.ts < b.ts ? 1 : -1
          return a.audit_id < b.audit_id ? 1 : -1
        })

        return { results: filtered.slice(0, limitBind), success: true }
      }
    }
  }
}

// ----------------------------------------------------------------------------
// Test harness
// ----------------------------------------------------------------------------

const API_KEY = 'test-key'
const AUTH_HEADERS = { 'X-Api-Key': API_KEY }

let db: FakeD1
let env: { DB: D1Database; API_KEY: string }

beforeEach(() => {
  db = new FakeD1()
  env = { DB: db as unknown as D1Database, API_KEY }
})

async function get(path: string, headers: Record<string, string> = AUTH_HEADERS) {
  return audit.request(path, { method: 'GET', headers }, env)
}

// ----------------------------------------------------------------------------
// Auth
// ----------------------------------------------------------------------------

describe('GET /api/audit/orders — auth', () => {
  it('rejects missing X-Api-Key with 401', async () => {
    const res = await get('/orders', {})
    expect(res.status).toBe(401)
  })

  it('rejects bad X-Api-Key with 401', async () => {
    const res = await get('/orders', { 'X-Api-Key': 'wrong-key' })
    expect(res.status).toBe(401)
  })
})

// ----------------------------------------------------------------------------
// Happy path
// ----------------------------------------------------------------------------

describe('GET /api/audit/orders — happy path', () => {
  it('returns all 5 rows sorted ts DESC', async () => {
    const res = await get('/orders')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { items: AuditRow[]; count: number; limit: number }
    expect(body.count).toBe(5)
    expect(body.items[0]!.audit_id).toBe('a5') // latest ts
    expect(body.items[4]!.audit_id).toBe('a1') // earliest ts
    expect(body.limit).toBe(50)
  })

  it('applies limit clamp', async () => {
    const res = await get('/orders?limit=2')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { items: AuditRow[]; count: number }
    expect(body.count).toBe(2)
    expect(body.items[0]!.audit_id).toBe('a5')
    expect(body.items[1]!.audit_id).toBe('a4')
  })

  it('filters by outcome', async () => {
    const res = await get('/orders?outcome=placed')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { items: AuditRow[]; count: number }
    expect(body.count).toBe(2)
    expect(body.items.every(r => r.outcome === 'placed')).toBe(true)
  })

  it('filters by outcome=rejected_semaphore', async () => {
    const res = await get('/orders?outcome=rejected_semaphore')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { items: AuditRow[]; count: number }
    expect(body.count).toBe(1)
    expect(body.items[0]!.audit_id).toBe('a2')
    expect(body.items[0]!.override_reason).toBe('semaphore-red')
  })

  it('filters by from (ISO datetime)', async () => {
    const res = await get('/orders?from=2026-04-20T11:00:00.000Z')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { items: AuditRow[]; count: number }
    // a3 (11:00), a4 (12:00), a5 (13:00)
    expect(body.count).toBe(3)
  })

  it('filters by from (bare date)', async () => {
    const res = await get('/orders?from=2026-04-20')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { items: AuditRow[]; count: number }
    // a2..a5 (all on 2026-04-20)
    expect(body.count).toBe(4)
  })

  it('combines from and outcome filters', async () => {
    const res = await get('/orders?from=2026-04-20&outcome=placed')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { items: AuditRow[]; count: number }
    // Only a3 is placed AND on 2026-04-20+
    expect(body.count).toBe(1)
    expect(body.items[0]!.audit_id).toBe('a3')
  })
})

// ----------------------------------------------------------------------------
// Validation
// ----------------------------------------------------------------------------

describe('GET /api/audit/orders — validation', () => {
  it('rejects non-numeric limit with 400', async () => {
    const res = await get('/orders?limit=abc')
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('invalid_limit')
  })

  it('clamps limit above MAX', async () => {
    const res = await get('/orders?limit=999999')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { limit: number }
    expect(body.limit).toBe(500)
  })

  it('clamps negative limit to 1', async () => {
    const res = await get('/orders?limit=-5')
    expect(res.status).toBe(200)
    const body = (await res.json()) as { limit: number }
    expect(body.limit).toBe(1)
  })

  it('rejects invalid from with 400', async () => {
    const res = await get('/orders?from=not-a-date')
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('invalid_from')
  })

  it('rejects unknown outcome with 400', async () => {
    const res = await get('/orders?outcome=something_new')
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('invalid_outcome')
  })
})
