/**
 * Integration tests for Cloudflare Worker endpoints
 * Uses Cloudflare vitest-pool-workers for D1 testing
 */

import { describe, it, expect, beforeAll } from 'vitest'
import { env, SELF } from 'cloudflare:test'

describe('Trading System Worker', () => {
  beforeAll(async () => {
    // Run basic schema creation for tests
    await env.DB.exec(`
      CREATE TABLE IF NOT EXISTS service_heartbeats (
        service_name TEXT PRIMARY KEY,
        hostname TEXT NOT NULL,
        last_seen_at TEXT NOT NULL,
        uptime_seconds INTEGER NOT NULL,
        cpu_percent REAL NOT NULL,
        ram_percent REAL NOT NULL,
        disk_free_gb REAL NOT NULL,
        trading_mode TEXT NOT NULL,
        version TEXT NOT NULL,
        created_at TEXT NOT NULL,
        updated_at TEXT NOT NULL
      );

      CREATE TABLE IF NOT EXISTS active_positions (
        position_id TEXT PRIMARY KEY,
        campaign_id TEXT NOT NULL,
        symbol TEXT NOT NULL,
        contract_symbol TEXT NOT NULL,
        strategy_name TEXT NOT NULL,
        quantity INTEGER NOT NULL,
        entry_price REAL NOT NULL,
        current_price REAL,
        unrealized_pnl REAL,
        stop_loss REAL,
        take_profit REAL,
        opened_at TEXT NOT NULL,
        updated_at TEXT NOT NULL,
        metadata_json TEXT
      );

      CREATE TABLE IF NOT EXISTS campaigns (
        id TEXT PRIMARY KEY,
        name TEXT NOT NULL,
        state TEXT NOT NULL,
        created_at TEXT NOT NULL,
        updated_at TEXT NOT NULL
      );

      CREATE TABLE IF NOT EXISTS alert_history (
        alert_id TEXT PRIMARY KEY,
        alert_type TEXT NOT NULL,
        severity TEXT NOT NULL CHECK(severity IN ('info', 'warning', 'critical')),
        message TEXT NOT NULL,
        details_json TEXT,
        source_service TEXT NOT NULL,
        created_at TEXT NOT NULL,
        resolved_at TEXT,
        resolved_by TEXT
      );
    `)

    // Insert test data
    const now = new Date().toISOString()

    await env.DB.prepare(
      `INSERT INTO service_heartbeats
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
    )
      .bind('test-service', 'localhost', now, 3600, 25.5, 45.2, 100.5, 'paper', '1.0.0', now, now)
      .run()

    // Seed a campaign so the LEFT JOIN on active_positions produces a labelled row
    await env.DB.prepare(
      `INSERT INTO campaigns (id, name, state, created_at, updated_at)
       VALUES (?, ?, ?, ?, ?)`
    )
      .bind('campaign-001', 'Test Iron Condor Campaign', 'active', now, now)
      .run()

    await env.DB.prepare(
      `INSERT INTO active_positions
       VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)`
    )
      .bind(
        'pos-001',
        'campaign-001',
        'SPY',
        'SPY240119C00450000',
        'iron-condor',
        10,
        5.25,
        5.50,
        25.0,
        4.75,
        6.00,
        now,
        now,
        null
      )
      .run()

    await env.DB.prepare(`INSERT INTO alert_history VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)`)
      .bind('alert-001', 'HeartbeatMissing', 'critical', 'Service heartbeat missing', null, 'supervisor', now, null, null)
      .run()
  })

  describe('Health Check', () => {
    it('should return 200 OK for health endpoint', async () => {
      const response = await SELF.fetch('https://example.com/api/health')
      expect(response.status).toBe(200)

      const data = await response.json()
      expect(data).toHaveProperty('ok', true)
      expect(data).toHaveProperty('timestamp')
    })
  })

  describe('Authentication', () => {
    it('should reject requests without API key', async () => {
      const response = await SELF.fetch('https://example.com/api/positions/active')
      expect(response.status).toBe(401)

      const data = await response.json()
      expect(data).toHaveProperty('error', 'missing_api_key')
    })

    it('should accept requests with valid API key', async () => {
      const response = await SELF.fetch('https://example.com/api/positions/active', {
        headers: { 'X-Api-Key': env.API_KEY }
      })
      expect(response.status).toBe(200)
    })
  })

  describe('Positions API', () => {
    it('should return active positions with campaign join', async () => {
      const response = await SELF.fetch('https://example.com/api/positions/active', {
        headers: { 'X-Api-Key': env.API_KEY }
      })
      expect(response.status).toBe(200)

      const data = (await response.json()) as {
        positions: { campaign: string | null }[]
        count: number
      }
      expect(data).toHaveProperty('positions')
      expect(data).toHaveProperty('count')
      // Campaign column must be present on every row (joined from campaigns table)
      for (const pos of data.positions) {
        expect(pos).toHaveProperty('campaign')
        // Seeded fixture has a matching campaign — verify the label came through
        if ((pos as unknown as { position_id: string }).position_id === 'pos-001') {
          expect(pos.campaign).toBe('Test Iron Condor Campaign')
        }
      }
    })

    it('should get single position by ID with campaign field', async () => {
      const response = await SELF.fetch('https://example.com/api/positions/pos-001', {
        headers: { 'X-Api-Key': env.API_KEY }
      })
      expect(response.status).toBe(200)

      const data = (await response.json()) as { position: { position_id: string; campaign: string | null } }
      expect(data.position).toHaveProperty('position_id', 'pos-001')
      expect(data.position).toHaveProperty('campaign')
      expect(typeof data.position.campaign === 'string' || data.position.campaign === null).toBe(true)
    })
  })

  describe('Alerts API', () => {
    it('should return alerts', async () => {
      const response = await SELF.fetch('https://example.com/api/alerts', {
        headers: { 'X-Api-Key': env.API_KEY }
      })
      expect(response.status).toBe(200)

      const data = await response.json()
      expect(data).toHaveProperty('alerts')
    })
  })

  describe('Heartbeats API', () => {
    it('should return heartbeats', async () => {
      const response = await SELF.fetch('https://example.com/api/heartbeats', {
        headers: { 'X-Api-Key': env.API_KEY }
      })
      expect(response.status).toBe(200)

      const data = await response.json()
      expect(data).toHaveProperty('heartbeats')
    })
  })
})
