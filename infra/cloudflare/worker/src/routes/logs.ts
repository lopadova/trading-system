/**
 * Centralized log ingest route (Phase 7.3 — observability).
 *
 * POST /api/v1/logs accepts a batched payload of structured log entries from
 * .NET Serilog sinks (TradingSupervisorService, OptionsExecutionService), the
 * Worker itself, and the Dashboard (browser → Worker path). Each entry is
 * UPSERTed into the D1 `service_logs` table keyed by (service, ts, sequence).
 *
 * Idempotency model:
 *   - The caller assigns a sequence index within each batch (0..N-1).
 *   - Primary key (service, ts, sequence) means that replaying the exact same
 *     batch (e.g. after a retryable 5xx or a client reconnect) does NOT create
 *     duplicates. Two callers producing logs at the same millisecond from
 *     different services still coexist because service is part of the key.
 *
 * Auth: reuses the same `X-Api-Key` middleware as every other /api/v1 route.
 *
 * Response shape: `{ accepted: number }` — the count of rows we successfully
 * tried to persist. Callers don't need row-level pass/fail because the PK is
 * the only durable integrity check and we short-circuit on Zod failure with
 * 400 before any D1 write.
 */

import { Hono } from 'hono'
import { z } from 'zod'
import type { Env } from '../types/env'
import { authMiddleware } from '../middleware/auth'
import { recordMetric } from '../lib/metrics'

const logs = new Hono<{ Bindings: Env }>()

// All routes require authentication
logs.use('*', authMiddleware)

// ============================================================================
// Zod schemas
// ============================================================================

const LogLevelSchema = z.enum([
  'trace',
  'debug',
  'info',
  'warn',
  'error',
  'critical'
])

const ExceptionSchema = z.object({
  type: z.string().min(1),
  message: z.string(),
  stackTrace: z.string().optional()
})

const LogEntrySchema = z.object({
  ts: z.string().min(1),                         // ISO 8601 UTC timestamp
  level: LogLevelSchema,
  service: z.string().min(1).max(64),
  message: z.string(),
  properties: z.record(z.string(), z.unknown()).optional(),
  source_context: z.string().optional(),
  exception: ExceptionSchema.optional()
})

const LogBatchSchema = z.object({
  batch: z.array(LogEntrySchema).min(1).max(500)
})

type LogEntry = z.infer<typeof LogEntrySchema>

/**
 * POST /api/v1/logs
 * Batched structured-log ingestion. Returns { accepted: number }.
 */
logs.post('/', async (c) => {
  // Parse JSON body defensively — malformed JSON becomes a clean 400.
  let rawBody: unknown
  try {
    rawBody = await c.req.json()
  } catch {
    return c.json(
      { error: 'invalid_json', message: 'Body is not valid JSON' },
      400
    )
  }

  const parsed = LogBatchSchema.safeParse(rawBody)
  if (!parsed.success) {
    const issues = parsed.error.issues.map(i => ({
      path: i.path.join('.'),
      message: i.message
    }))
    return c.json(
      {
        error: 'invalid_payload',
        message: 'Log batch failed validation',
        issues
      },
      400
    )
  }

  const entries = parsed.data.batch

  try {
    // UPSERT each entry. Sequence is the index in the batch, so retrying the
    // exact same batch reuses the same PK and INSERT OR REPLACE dedupes.
    const sql = `
      INSERT OR REPLACE INTO service_logs
        (service, ts, sequence, level, message, properties,
         source_context, exception_type, exception_message, exception_stack)
      VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    `

    let accepted = 0
    for (let i = 0; i < entries.length; i++) {
      const entry = entries[i] as LogEntry
      const properties = entry.properties
        ? JSON.stringify(entry.properties)
        : null
      const exception = entry.exception ?? null

      await c.env.DB.prepare(sql)
        .bind(
          entry.service,
          entry.ts,
          i,
          entry.level,
          entry.message,
          properties,
          entry.source_context ?? null,
          exception?.type ?? null,
          exception?.message ?? null,
          exception?.stackTrace ?? null
        )
        .run()
      accepted++
    }

    // Counter tagged by service + level. We increment once per batch (not per
    // row) to keep the cardinality bounded; downstream `accepted` is in the
    // response body if a caller wants row-level insight.
    const serviceTags = new Set<string>()
    for (const e of entries) serviceTags.add(e.service)
    for (const svc of serviceTags) {
      recordMetric(c.env, 'logs.batch', { service: svc, status: 'accepted' })
    }

    return c.json({ accepted })
  } catch (error) {
    // Don't leak SQL internals. Log once, return a generic 500.
    console.error('[LOGS] D1 write failed:', error)
    recordMetric(c.env, 'd1.error', { route: 'logs' })
    return c.json(
      {
        error: 'ingest_error',
        message: error instanceof Error ? error.message : 'Unknown error'
      },
      500
    )
  }
})

export { logs }
