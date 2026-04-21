/**
 * Unit tests for recordMetric helper (Phase 7.3).
 *
 * The helper must:
 *   - no-op silently when the METRICS binding is absent (local dev case)
 *   - invoke writeDataPoint with name as index, name + sorted tag values as blobs,
 *     and doubles=[1]
 *   - swallow any error from writeDataPoint (fire-and-forget semantics)
 */

import { describe, it, expect, vi } from 'vitest'
import { recordMetric } from '../src/lib/metrics'

describe('recordMetric', () => {
  it('does nothing when METRICS binding is undefined', () => {
    // No throws, no side-effects beyond the no-op.
    expect(() => recordMetric({}, 'ingest.event_type', { type: 'heartbeat' })).not.toThrow()
  })

  it('writes a data point with the expected shape', () => {
    const writeDataPoint = vi.fn()
    const METRICS = { writeDataPoint } as unknown as AnalyticsEngineDataset

    recordMetric({ METRICS }, 'ingest.event_type', {
      type: 'heartbeat',
      status: 'accepted'
    })

    expect(writeDataPoint).toHaveBeenCalledTimes(1)
    const arg = writeDataPoint.mock.calls[0]![0]
    expect(arg.indexes).toEqual(['ingest.event_type'])
    // Blobs: name first, then tag values in alphabetized-key order.
    expect(arg.blobs[0]).toBe('ingest.event_type')
    // 'status' sorts before 'type' → accepted, heartbeat
    expect(arg.blobs).toEqual(['ingest.event_type', 'accepted', 'heartbeat'])
    expect(arg.doubles).toEqual([1])
  })

  it('handles missing tags gracefully', () => {
    const writeDataPoint = vi.fn()
    const METRICS = { writeDataPoint } as unknown as AnalyticsEngineDataset

    recordMetric({ METRICS }, 'auth.failure')

    expect(writeDataPoint).toHaveBeenCalledTimes(1)
    const arg = writeDataPoint.mock.calls[0]![0]
    expect(arg.blobs).toEqual(['auth.failure'])
    expect(arg.indexes).toEqual(['auth.failure'])
    expect(arg.doubles).toEqual([1])
  })

  it('swallows errors from writeDataPoint (never propagates)', () => {
    const writeDataPoint = vi.fn(() => {
      throw new Error('analytics pipe is on fire')
    })
    const METRICS = { writeDataPoint } as unknown as AnalyticsEngineDataset

    expect(() => recordMetric({ METRICS }, 'd1.error', { route: 'ingest' })).not.toThrow()
  })
})
