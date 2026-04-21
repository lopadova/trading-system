/**
 * Unit tests for POST /api/v1/logs (Phase 7.3 centralized log ingest).
 *
 * Mirrors the in-memory D1 mock used by test/ingest.test.ts. We validate:
 *   - valid batch → 200 + accepted count matches batch size + D1 rows exist
 *   - malformed payload → 400 with Zod issues
 *   - missing auth → 401
 *   - idempotency: same batch posted twice → PK dedupe (row count unchanged)
 */

import { describe, it, expect, beforeEach } from 'vitest'
import { logs } from '../src/routes/logs'

// ============================================================================
// In-memory D1 mock — minimal, only supports service_logs INSERT OR REPLACE
// ============================================================================

type StmtLog = {
  sql: string
  params: unknown[]
}

class FakeTable {
  rows = new Map<string, Record<string, unknown>>()
}

class FakeD1 {
  tables: Record<string, FakeTable> = {
    service_logs: new FakeTable()
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

  applyStatement(sql: string, params: unknown[]) {
    const normalized = sql.replace(/\s+/g, ' ').trim()
    if (!normalized.includes('INSERT OR REPLACE INTO service_logs')) return

    const cols = [
      'service',
      'ts',
      'sequence',
      'level',
      'message',
      'properties',
      'source_context',
      'exception_type',
      'exception_message',
      'exception_stack'
    ]
    const row: Record<string, unknown> = {}
    cols.forEach((c, idx) => { row[c] = params[idx] })
    const pk = `${String(row['service'])}|${String(row['ts'])}|${String(row['sequence'])}`
    this.tables['service_logs']!.rows.set(pk, row)
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
  return logs.request('/', {
    method: 'POST',
    headers,
    body: JSON.stringify(body)
  }, env)
}

function makeEntry(overrides: Record<string, unknown> = {}) {
  return {
    ts: '2026-04-21T10:15:30.123Z',
    level: 'info',
    service: 'supervisor',
    message: 'Heartbeat ok',
    ...overrides
  }
}

// ============================================================================
// Tests
// ============================================================================

describe('POST /api/v1/logs — auth', () => {
  it('rejects missing X-Api-Key with 401', async () => {
    const res = await post(
      { batch: [makeEntry()] },
      { 'Content-Type': 'application/json' }
    )
    expect(res.status).toBe(401)
  })

  it('rejects wrong API key with 401', async () => {
    const res = await post(
      { batch: [makeEntry()] },
      { 'X-Api-Key': 'nope', 'Content-Type': 'application/json' }
    )
    expect(res.status).toBe(401)
  })
})

describe('POST /api/v1/logs — validation', () => {
  it('rejects non-JSON body with 400', async () => {
    const res = await logs.request('/', {
      method: 'POST',
      headers: AUTH_HEADERS,
      body: 'not-json'
    }, env)
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('invalid_json')
  })

  it('rejects empty batch with 400', async () => {
    const res = await post({ batch: [] })
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string }
    expect(body.error).toBe('invalid_payload')
  })

  it('rejects missing batch field with 400', async () => {
    const res = await post({ entries: [makeEntry()] })
    expect(res.status).toBe(400)
  })

  it('rejects entry with invalid level with 400', async () => {
    const res = await post({ batch: [makeEntry({ level: 'banana' })] })
    expect(res.status).toBe(400)
    const body = (await res.json()) as { error: string; issues: unknown[] }
    expect(body.error).toBe('invalid_payload')
    expect(body.issues.length).toBeGreaterThan(0)
  })

  it('rejects entry missing service with 400', async () => {
    const entry = makeEntry()
    // Remove a required field
    const { service: _, ...rest } = entry as unknown as Record<string, unknown>
    void _
    const res = await post({ batch: [rest] })
    expect(res.status).toBe(400)
  })
})

describe('POST /api/v1/logs — happy path', () => {
  it('accepts valid 3-entry batch → 200 + 3 rows inserted', async () => {
    const batch = [
      makeEntry({ message: 'first' }),
      makeEntry({ level: 'warn', message: 'second' }),
      makeEntry({
        level: 'error',
        message: 'third',
        exception: { type: 'TimeoutException', message: 'pipe dead', stackTrace: 'at ...' }
      })
    ]
    const res = await post({ batch })
    expect(res.status).toBe(200)
    const body = (await res.json()) as { accepted: number }
    expect(body.accepted).toBe(3)
    expect(db.tables['service_logs']!.rows.size).toBe(3)

    // Sequence assignment: 0, 1, 2
    const rows = Array.from(db.tables['service_logs']!.rows.values())
    const sequences = rows.map(r => r['sequence']).sort()
    expect(sequences).toEqual([0, 1, 2])

    // Exception fields captured on the third entry
    const errorRow = rows.find(r => r['level'] === 'error')
    expect(errorRow!['exception_type']).toBe('TimeoutException')
    expect(errorRow!['exception_message']).toBe('pipe dead')
    expect(errorRow!['exception_stack']).toBe('at ...')
  })

  it('stores properties as JSON string', async () => {
    const res = await post({
      batch: [makeEntry({
        properties: { correlation_id: 'abc-123', user_id: 'u42', retry: 3 }
      })]
    })
    expect(res.status).toBe(200)
    const row = Array.from(db.tables['service_logs']!.rows.values())[0]!
    expect(typeof row['properties']).toBe('string')
    expect(JSON.parse(row['properties'] as string)).toEqual({
      correlation_id: 'abc-123',
      user_id: 'u42',
      retry: 3
    })
  })

  it('handles entry with no optional fields', async () => {
    const res = await post({ batch: [makeEntry()] })
    expect(res.status).toBe(200)
    const row = Array.from(db.tables['service_logs']!.rows.values())[0]!
    expect(row['properties']).toBeNull()
    expect(row['source_context']).toBeNull()
    expect(row['exception_type']).toBeNull()
  })
})

describe('POST /api/v1/logs — idempotency', () => {
  it('replaying exact same batch does not duplicate rows (PK dedupe)', async () => {
    const batch = [
      makeEntry({ message: 'a' }),
      makeEntry({ message: 'b', ts: '2026-04-21T10:15:31.000Z' })
    ]
    const res1 = await post({ batch })
    expect(res1.status).toBe(200)
    const res2 = await post({ batch })
    expect(res2.status).toBe(200)
    // Both calls report 2 accepted, but D1 only has 2 unique PKs
    expect(db.tables['service_logs']!.rows.size).toBe(2)
  })

  it('two services logging at identical ts coexist (service in PK)', async () => {
    const ts = '2026-04-21T10:15:30.123Z'
    const res = await post({
      batch: [
        makeEntry({ service: 'supervisor', ts, message: 'A' }),
        makeEntry({ service: 'options-execution', ts, message: 'B' })
      ]
    })
    expect(res.status).toBe(200)
    expect(db.tables['service_logs']!.rows.size).toBe(2)
  })
})
