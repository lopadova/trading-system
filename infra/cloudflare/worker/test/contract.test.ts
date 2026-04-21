/**
 * Phase 7.6 — Consumer-side contract tests.
 *
 * For every outbox event type that has a Zod schema in ingest.ts, this file
 * imports the schema and asserts that the corresponding fixture under
 * ../../../tests/Contract/fixtures/outbox-events/<type>.json is a valid
 * payload via schema.safeParse().
 *
 * If a test fails here, EITHER the schema drifted from the contract OR the
 * fixture is stale — update both sides together. See
 * `tests/Contract/README.md` for the full procedure.
 *
 * Events without Zod schemas (heartbeat, alert) use the loose "existing
 * handler" path in ingest.ts — we still snapshot their shape on the .NET
 * side but skip a Zod assertion here.
 */

import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, it, expect } from 'vitest'
import {
  AccountEquityPayloadSchema,
  MarketQuotePayloadSchema,
  VixSnapshotPayloadSchema,
  BenchmarkClosePayloadSchema,
  PositionGreeksPayloadSchema,
  OrderAuditPayloadSchema,
  WebVitalsPayloadSchema,
} from '../src/routes/ingest'

// Resolve fixture paths relative to this file. infra/cloudflare/worker/test/
// is 3 levels below the repo root; ../../../tests/Contract/fixtures/ anchors
// us at the repo-root fixture directory.
const FIXTURES_ROOT = resolve(__dirname, '../../../../tests/Contract/fixtures')

function readOutboxFixture(eventType: string): unknown {
  const path = resolve(FIXTURES_ROOT, 'outbox-events', `${eventType}.json`)
  return JSON.parse(readFileSync(path, 'utf-8'))
}

function readWorkerResponseFixture(name: string): unknown {
  const path = resolve(FIXTURES_ROOT, 'worker-responses', `${name}.json`)
  return JSON.parse(readFileSync(path, 'utf-8'))
}

describe('contract: outbox event payloads parse against the Worker Zod schemas', () => {
  it('account_equity fixture passes AccountEquityPayloadSchema', () => {
    const fixture = readOutboxFixture('account_equity')
    const result = AccountEquityPayloadSchema.safeParse(fixture)
    expect(result.success, renderError('account_equity', result)).toBe(true)
  })

  it('market_quote fixture passes MarketQuotePayloadSchema', () => {
    const fixture = readOutboxFixture('market_quote')
    const result = MarketQuotePayloadSchema.safeParse(fixture)
    expect(result.success, renderError('market_quote', result)).toBe(true)
  })

  it('vix_snapshot fixture passes VixSnapshotPayloadSchema', () => {
    const fixture = readOutboxFixture('vix_snapshot')
    const result = VixSnapshotPayloadSchema.safeParse(fixture)
    expect(result.success, renderError('vix_snapshot', result)).toBe(true)
  })

  it('benchmark_close fixture passes BenchmarkClosePayloadSchema', () => {
    const fixture = readOutboxFixture('benchmark_close')
    const result = BenchmarkClosePayloadSchema.safeParse(fixture)
    expect(result.success, renderError('benchmark_close', result)).toBe(true)
  })

  it('position_greeks fixture passes PositionGreeksPayloadSchema', () => {
    const fixture = readOutboxFixture('position_greeks')
    const result = PositionGreeksPayloadSchema.safeParse(fixture)
    expect(result.success, renderError('position_greeks', result)).toBe(true)
  })

  it('order_audit fixture passes OrderAuditPayloadSchema', () => {
    const fixture = readOutboxFixture('order_audit')
    const result = OrderAuditPayloadSchema.safeParse(fixture)
    expect(result.success, renderError('order_audit', result)).toBe(true)
  })

  // heartbeat + alert are handled by the "legacy" path in ingest.ts
  // (no Zod schema) — see the `handleHeartbeat` / `handleAlert` branches.
  // We still snapshot their shape on the .NET producer side; there's
  // nothing to assert against on the consumer until those handlers gain
  // Zod schemas too. When they do, add an `it(...)` here.

  it('negative: a payload missing a required field is REJECTED', () => {
    // Guardrail: if safeParse were somehow lenient enough to accept any
    // shape, our positive tests would silently pass even when the contract
    // broke. This test fails loudly if the Zod schema becomes pass-through.
    const broken = { date: '2026-04-20', cash: 100 }  // Missing account_value, etc.
    const result = AccountEquityPayloadSchema.safeParse(broken)
    expect(result.success).toBe(false)
  })
})

describe('contract: worker response shapes', () => {
  // Lightweight structural checks on each response fixture. We don't have
  // a Zod schema for the envelopes themselves (they're hand-written in
  // ingest.ts), so we assert on the field set — if a field is renamed or
  // removed, this test fails and the .NET side (which parses these
  // envelopes in OutboxSyncWorker.cs) gets a warning in its tests too.
  it('ingest.success has the documented envelope', () => {
    const fixture = readWorkerResponseFixture('ingest.success') as Record<string, unknown>
    expect(Object.keys(fixture).sort()).toEqual(
      ['event_id', 'event_type', 'message', 'success'].sort()
    )
    expect(typeof fixture.success).toBe('boolean')
    expect(typeof fixture.event_id).toBe('string')
  })

  it('ingest.invalid-payload surfaces Zod issues under "issues"', () => {
    const fixture = readWorkerResponseFixture('ingest.invalid-payload') as {
      error: string
      message: string
      issues: Array<{ path: string; message: string }>
    }
    expect(fixture.error).toBe('invalid_payload')
    expect(Array.isArray(fixture.issues)).toBe(true)
    if (fixture.issues.length > 0) {
      const first = fixture.issues[0]!
      expect(typeof first.path).toBe('string')
      expect(typeof first.message).toBe('string')
    }
  })

  it('ingest.invalid-event-type has the documented envelope', () => {
    const fixture = readWorkerResponseFixture('ingest.invalid-event-type') as Record<string, unknown>
    expect(Object.keys(fixture).sort()).toEqual(['error', 'message'].sort())
    expect(fixture.error).toBe('invalid_event_type')
  })

  it('ingest.server-error has the documented envelope', () => {
    const fixture = readWorkerResponseFixture('ingest.server-error') as Record<string, unknown>
    expect(Object.keys(fixture).sort()).toEqual(['error', 'message'].sort())
    expect(fixture.error).toBe('ingest_error')
  })
})

// Use the web_vitals schema as a sanity check — not part of the Contract
// fixture set (it's a dashboard → Worker contract, not a .NET → Worker one)
// but importing it here keeps the import list matched with what ingest.ts
// exports, so a schema rename or removal surfaces as a TS error here too.
describe('contract: web_vitals schema is still importable (TS link only)', () => {
  it('web_vitals schema parses a minimal valid payload', () => {
    const result = WebVitalsPayloadSchema.safeParse({
      session_id: 'sess-1',
      name: 'CLS',
      value: 0.05,
    })
    expect(result.success).toBe(true)
  })
})

function renderError(label: string, result: { success: boolean; error?: { issues?: unknown[] } }): string {
  if (result.success) return ''
  return `Fixture for ${label} failed schema validation: ${JSON.stringify(result.error?.issues ?? [], null, 2)}`
}
