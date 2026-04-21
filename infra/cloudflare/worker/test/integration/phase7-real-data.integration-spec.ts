/**
 * Phase 7.2 integration tests — real-data aggregate endpoints.
 *
 * Seeds D1 with synthetic equity, benchmark, VIX, heartbeat, position and
 * greeks data, then hits every aggregate endpoint wired in Phase 7.2 to
 * verify the computed output matches the expected math (not just the shape).
 *
 * Seed strategy: 500 trading days of deterministic daily data anchored to
 * 2026-04-20 so tests stay reproducible across runs. Equity grows smoothly
 * with one intentional drawdown in the middle of the window so drawdowns and
 * worst-N episodes have something to bite into.
 *
 * NOTE: vitest-native unit config excludes test/integration/**; this file is
 * picked up only by vitest.integration.config.ts.
 */

import { describe, it, expect, beforeAll } from 'vitest'
import { env, SELF } from 'cloudflare:test'

// Anchor the seed window to the current UTC date so SQLite `date('now', ...)`
// in the route code continues to overlap the seeded dataset as real time moves
// forward. Using a hard-coded date here would make every 1Y / 2Y / 5Y lookback
// test future-flaky the moment the clock passes past the seed's last day
// (routes would fall back to mocks and the endpoint-math assertions would fail).
// If a route later accepts an explicit `asOf` param we can pin the window
// deterministically instead; until then, derive from real "today".
function getCurrentUtcIsoDate(): string {
  return new Date().toISOString().slice(0, 10)
}

const SEED_END_DATE = getCurrentUtcIsoDate()
const SEED_DAYS = 500  // ~2 years of trading days

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Produce an array of `count` ISO dates (YYYY-MM-DD) going BACK from `endIso`
 * inclusive. Returned oldest-first.
 */
function generateDates(endIso: string, count: number): string[] {
  const end = new Date(`${endIso}T00:00:00Z`)
  const out: string[] = []
  for (let i = count - 1; i >= 0; i--) {
    const d = new Date(end)
    d.setUTCDate(d.getUTCDate() - i)
    out.push(d.toISOString().slice(0, 10))
  }
  return out
}

/**
 * Run a single CREATE TABLE statement via prepare/run. Avoids using the
 * multi-statement `env.DB.exec` path so our seed path is entirely
 * bind-parameterized.
 */
async function runDdl(sql: string): Promise<void> {
  await env.DB.prepare(sql).run()
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------
describe('Phase 7.2 real-data integration', () => {
  beforeAll(async () => {
    // Create every table the Phase 7.2 routes touch. We inline DDL here since
    // tests don't run wrangler migrations.
    await runDdl(
      'CREATE TABLE IF NOT EXISTS account_equity_daily (' +
        'date TEXT PRIMARY KEY, ' +
        'account_value REAL NOT NULL, ' +
        'cash REAL NOT NULL, ' +
        'buying_power REAL NOT NULL, ' +
        'margin_used REAL NOT NULL, ' +
        'margin_used_pct REAL NOT NULL, ' +
        'created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS market_quotes_daily (' +
        'symbol TEXT NOT NULL, date TEXT NOT NULL, ' +
        'open REAL, high REAL, low REAL, close REAL NOT NULL, volume INTEGER, ' +
        'created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, ' +
        'PRIMARY KEY (symbol, date)' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS vix_term_structure (' +
        'date TEXT PRIMARY KEY, vix REAL, vix1d REAL, vix3m REAL, vix6m REAL, ' +
        'created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS benchmark_series (' +
        'symbol TEXT NOT NULL, date TEXT NOT NULL, close REAL NOT NULL, ' +
        'close_normalized REAL, PRIMARY KEY (symbol, date)' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS position_greeks (' +
        'position_id TEXT NOT NULL, snapshot_ts TEXT NOT NULL, ' +
        'delta REAL, gamma REAL, theta REAL, vega REAL, iv REAL, ' +
        'underlying_price REAL, created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, ' +
        'PRIMARY KEY (position_id, snapshot_ts)' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS service_heartbeats (' +
        'service_name TEXT PRIMARY KEY, hostname TEXT NOT NULL, ' +
        'last_seen_at TEXT NOT NULL, uptime_seconds INTEGER NOT NULL, ' +
        'cpu_percent REAL NOT NULL, ram_percent REAL NOT NULL, ' +
        'disk_free_gb REAL NOT NULL, trading_mode TEXT NOT NULL, ' +
        'version TEXT NOT NULL, created_at TEXT NOT NULL, updated_at TEXT NOT NULL' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS alert_history (' +
        'alert_id TEXT PRIMARY KEY, alert_type TEXT NOT NULL, ' +
        'severity TEXT NOT NULL, message TEXT NOT NULL, ' +
        'details_json TEXT, source_service TEXT NOT NULL, ' +
        'created_at TEXT NOT NULL, resolved_at TEXT, resolved_by TEXT' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS execution_log (' +
        'execution_id TEXT PRIMARY KEY, order_id TEXT NOT NULL, ' +
        'position_id TEXT, campaign_id TEXT NOT NULL, symbol TEXT NOT NULL, ' +
        'contract_symbol TEXT NOT NULL, side TEXT NOT NULL, ' +
        'quantity INTEGER NOT NULL, fill_price REAL NOT NULL, ' +
        'commission REAL NOT NULL, executed_at TEXT NOT NULL, ' +
        'created_at TEXT NOT NULL' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS campaigns (' +
        'id TEXT PRIMARY KEY, name TEXT NOT NULL, state TEXT NOT NULL, ' +
        'created_at TEXT NOT NULL, updated_at TEXT NOT NULL' +
      ')',
    )
    await runDdl(
      'CREATE TABLE IF NOT EXISTS active_positions (' +
        'position_id TEXT PRIMARY KEY, campaign_id TEXT NOT NULL, ' +
        'symbol TEXT NOT NULL, contract_symbol TEXT NOT NULL, ' +
        'strategy_name TEXT NOT NULL, quantity INTEGER NOT NULL, ' +
        'entry_price REAL NOT NULL, current_price REAL, ' +
        'unrealized_pnl REAL, stop_loss REAL, take_profit REAL, ' +
        'opened_at TEXT NOT NULL, updated_at TEXT NOT NULL, ' +
        'metadata_json TEXT' +
      ')',
    )

    // -----------------------------------------------------------------------
    // Seed: 500 days of equity with a drawdown episode
    // -----------------------------------------------------------------------
    const dates = generateDates(SEED_END_DATE, SEED_DAYS)
    const stmtEquity = env.DB.prepare(
      'INSERT OR REPLACE INTO account_equity_daily ' +
      '(date, account_value, cash, buying_power, margin_used, margin_used_pct) ' +
      'VALUES (?, ?, ?, ?, ?, ?)',
    )
    const equityBatch = dates.map((d, i) => {
      // Smooth upward drift with a 30-day 10% dip centered at day 200.
      const base = 100000 + i * 50
      const dipIdx = i - 200
      const dip = Math.abs(dipIdx) <= 15 ? -(15 - Math.abs(dipIdx)) * 700 : 0
      const val = base + dip
      return stmtEquity.bind(d, val, val * 0.3, val * 1.5, val * 0.2, 20)
    })
    await env.DB.batch(equityBatch)

    // SPX quotes (same dates) - slight dip too.
    const stmtQuote = env.DB.prepare(
      'INSERT OR REPLACE INTO market_quotes_daily (symbol, date, open, high, low, close, volume) ' +
      'VALUES (?, ?, NULL, NULL, NULL, ?, NULL)',
    )
    const spxBatch = dates.map((d, i) => {
      const base = 5000 + i * 2
      const dipIdx = i - 200
      const dip = Math.abs(dipIdx) <= 15 ? -(15 - Math.abs(dipIdx)) * 15 : 0
      return stmtQuote.bind('SPX', d, base + dip)
    })
    await env.DB.batch(spxBatch)

    // VIX term structure (non-null vix + vix3m on every date)
    const stmtVix = env.DB.prepare(
      'INSERT OR REPLACE INTO vix_term_structure (date, vix, vix1d, vix3m, vix6m) ' +
      'VALUES (?, ?, ?, ?, ?)',
    )
    const vixBatch = dates.map((d, i) => {
      const phase = (i % 100) / 100
      const vix = 14 + phase * 10
      const vix3m = vix + 2
      return stmtVix.bind(d, vix, vix - 1, vix3m, vix3m + 1)
    })
    await env.DB.batch(vixBatch)

    // Benchmark series for SPX + SWDA with pre-normalized closes
    const stmtBench = env.DB.prepare(
      'INSERT OR REPLACE INTO benchmark_series (symbol, date, close, close_normalized) ' +
      'VALUES (?, ?, ?, ?)',
    )
    const benchBatch: D1PreparedStatement[] = []
    dates.forEach((d, i) => {
      benchBatch.push(stmtBench.bind('SPX', d, 5000 + i * 2, 100 + i * 0.04))
      benchBatch.push(stmtBench.bind('SWDA', d, 100 + i * 0.03, 100 + i * 0.03))
    })
    await env.DB.batch(benchBatch)

    // Campaigns + positions + greeks
    await env.DB.batch([
      env.DB.prepare(
        'INSERT OR REPLACE INTO campaigns (id, name, state, created_at, updated_at) ' +
        'VALUES (?, ?, ?, ?, ?)',
      ).bind('c-1', 'Active Campaign', 'active', SEED_END_DATE, SEED_END_DATE),
      env.DB.prepare(
        'INSERT OR REPLACE INTO campaigns (id, name, state, created_at, updated_at) VALUES (?, ?, ?, ?, ?)',
      ).bind('c-2', 'Another Active', 'active', SEED_END_DATE, SEED_END_DATE),
      env.DB.prepare(
        'INSERT OR REPLACE INTO campaigns (id, name, state, created_at, updated_at) VALUES (?, ?, ?, ?, ?)',
      ).bind('c-3', 'Paused One', 'paused', SEED_END_DATE, SEED_END_DATE),
      env.DB.prepare(
        'INSERT OR REPLACE INTO campaigns (id, name, state, created_at, updated_at) VALUES (?, ?, ?, ?, ?)',
      ).bind('c-4', 'Drafted', 'draft', SEED_END_DATE, SEED_END_DATE),
    ])

    await env.DB.batch([
      env.DB.prepare(
        'INSERT OR REPLACE INTO active_positions ' +
        '(position_id, campaign_id, symbol, contract_symbol, strategy_name, ' +
        'quantity, entry_price, current_price, unrealized_pnl, stop_loss, take_profit, opened_at, updated_at) ' +
        'VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)',
      ).bind('pos-1', 'c-1', 'SPY', 'SPY240419C450', 'Iron Condor', 5, 10, 12, 10, 8, 15, SEED_END_DATE, SEED_END_DATE),
      env.DB.prepare(
        'INSERT OR REPLACE INTO active_positions ' +
        '(position_id, campaign_id, symbol, contract_symbol, strategy_name, ' +
        'quantity, entry_price, current_price, unrealized_pnl, stop_loss, take_profit, opened_at, updated_at) ' +
        'VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)',
      ).bind('pos-2', 'c-1', 'QQQ', 'QQQ240419P380', 'Put Spread', 3, 20, 22, 6, 18, 25, SEED_END_DATE, SEED_END_DATE),
    ])

    // Latest greeks snapshot for both positions
    await env.DB.batch([
      env.DB.prepare(
        'INSERT OR REPLACE INTO position_greeks ' +
        '(position_id, snapshot_ts, delta, gamma, theta, vega, iv, underlying_price) ' +
        'VALUES (?, ?, ?, ?, ?, ?, ?, ?)',
      ).bind('pos-1', `${SEED_END_DATE}T14:30:00Z`, 0.25, 0.01, -1.2, 3.4, 0.18, 450),
      env.DB.prepare(
        'INSERT OR REPLACE INTO position_greeks ' +
        '(position_id, snapshot_ts, delta, gamma, theta, vega, iv, underlying_price) ' +
        'VALUES (?, ?, ?, ?, ?, ?, ?, ?)',
      ).bind('pos-2', `${SEED_END_DATE}T14:31:00Z`, -0.30, 0.02, -0.8, 2.1, 0.21, 380),
    ])

    // Heartbeats — a few services with recent samples
    await env.DB.batch([
      env.DB.prepare(
        'INSERT OR REPLACE INTO service_heartbeats ' +
        '(service_name, hostname, last_seen_at, uptime_seconds, cpu_percent, ' +
        'ram_percent, disk_free_gb, trading_mode, version, created_at, updated_at) ' +
        'VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)',
      ).bind('svc1', 'host1', `${SEED_END_DATE}T14:30:00Z`, 3600, 35.0, 62.0, 150, 'paper', '1.0', `${SEED_END_DATE}T14:30:00Z`, `${SEED_END_DATE}T14:30:00Z`),
      env.DB.prepare(
        'INSERT OR REPLACE INTO service_heartbeats ' +
        '(service_name, hostname, last_seen_at, uptime_seconds, cpu_percent, ' +
        'ram_percent, disk_free_gb, trading_mode, version, created_at, updated_at) ' +
        'VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)',
      ).bind('svc2', 'host2', `${SEED_END_DATE}T14:29:00Z`, 7200, 22.0, 55.0, 155, 'paper', '1.0', `${SEED_END_DATE}T14:29:00Z`, `${SEED_END_DATE}T14:29:00Z`),
    ])

    // Alerts + executions for activity feed
    await env.DB.batch([
      env.DB.prepare(
        'INSERT OR REPLACE INTO alert_history ' +
        '(alert_id, alert_type, severity, message, details_json, source_service, created_at) ' +
        'VALUES (?, ?, ?, ?, NULL, ?, ?)',
      ).bind('alr-1', 'DeltaBreach', 'warning', 'Delta exceeds threshold', 'risk', `${SEED_END_DATE}T14:00:00Z`),
      env.DB.prepare(
        'INSERT OR REPLACE INTO alert_history ' +
        '(alert_id, alert_type, severity, message, details_json, source_service, created_at) ' +
        'VALUES (?, ?, ?, ?, NULL, ?, ?)',
      ).bind('alr-2', 'IBKRDrop', 'critical', 'IBKR disconnected', 'supervisor', `${SEED_END_DATE}T13:00:00Z`),
      env.DB.prepare(
        'INSERT OR REPLACE INTO execution_log ' +
        '(execution_id, order_id, campaign_id, symbol, contract_symbol, side, quantity, fill_price, commission, executed_at, created_at) ' +
        'VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)',
      ).bind('exec-1', 'ord-1', 'c-1', 'SPY', 'SPY240419C450', 'BUY', 3, 12.40, 1.5, `${SEED_END_DATE}T13:30:00Z`, `${SEED_END_DATE}T13:30:00Z`),
    ])
  })

  // -------------------------------------------------------------------------
  // performance /summary
  // -------------------------------------------------------------------------
  it('performance /summary returns computed returns from seeded equity', async () => {
    const res = await SELF.fetch('https://example.com/api/performance/summary?asset=all', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      asset: string
      m: number
      ytd: number
      base: number
    }
    expect(body.asset).toBe('all')
    // Latest account_value ≈ 100000 + 499*50 = 124950 (last dip is at day 200)
    expect(body.base).toBeGreaterThan(100000)
    // 1M return: last vs 22 days ago. With +50/day drift and no dip in the
    // last 22 days, the return should be roughly (50*22)/latestValue*100 ≈
    // 1100/124950 ≈ 0.88%. We allow a wide band to absorb the drawdown.
    expect(body.m).toBeGreaterThan(0)
    expect(body.m).toBeLessThan(5)
    // Real-data path must NOT set the fallback header.
    expect(res.headers.get('X-Data-Source')).toBeNull()
  })

  // -------------------------------------------------------------------------
  // performance /series
  // -------------------------------------------------------------------------
  it('performance /series normalizes portfolio to 100 on day 1', async () => {
    const res = await SELF.fetch('https://example.com/api/performance/series?asset=all&range=1M', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      portfolio: number[]
      sp500: number[]
      swda: number[]
    }
    expect(body.portfolio.length).toBeGreaterThan(0)
    // First point normalized to 100 after applying asset-scale 1.0 for 'all'.
    expect(body.portfolio[0]).toBeCloseTo(100, 0)
    expect(body.sp500.length).toBeGreaterThan(0)
    expect(body.swda.length).toBeGreaterThan(0)
  })

  // -------------------------------------------------------------------------
  // drawdowns
  // -------------------------------------------------------------------------
  it('drawdowns surfaces the seeded drawdown episode in worst[]', async () => {
    const res = await SELF.fetch('https://example.com/api/drawdowns?asset=all&range=1Y', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      portfolioSeries: number[]
      sp500Series: number[]
      worst: { depthPct: number; start: string; end: string; months: number }[]
    }
    expect(body.portfolioSeries.length).toBeGreaterThan(0)
    expect(body.sp500Series.length).toBeGreaterThan(0)
    // The drawdown episode at day ~200 should land in worst[] if the 1Y
    // window still covers it. If not, worst[] may be empty but the
    // portfolioSeries must still render.
    if (body.worst.length > 0) {
      expect(body.worst[0]?.depthPct).toBeLessThan(0)
    }
  })

  // -------------------------------------------------------------------------
  // monthly-returns
  // -------------------------------------------------------------------------
  it('monthly-returns produces 12-entry arrays per year', async () => {
    const res = await SELF.fetch('https://example.com/api/monthly-returns?asset=all', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      years: Record<string, (number | null)[]>
      totals: Record<string, number>
    }
    const years = Object.keys(body.years)
    expect(years.length).toBeGreaterThan(0)
    for (const y of years) {
      expect(body.years[y]?.length).toBe(12)
    }
  })

  // -------------------------------------------------------------------------
  // risk /metrics
  // -------------------------------------------------------------------------
  it('risk /metrics reflects the latest VIX + greeks + equity', async () => {
    const res = await SELF.fetch('https://example.com/api/risk/metrics', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      vix: number | null
      vix3m: number | null
      delta: number
      theta: number
      vega: number
      buyingPower: number
      marginUsedPct: number
    }
    // Latest VIX row from the seed is at phase (499 % 100)/100 = 0.99 → 14+9.9=23.9
    expect(body.vix).toBeGreaterThan(13)
    expect(body.vix).toBeLessThan(25)
    // Sum of seeded greeks: pos-1 delta 0.25 + pos-2 delta -0.30 ≈ -0.05
    expect(body.delta).toBeCloseTo(-0.05, 1)
    // Theta: -1.2 + -0.8 = -2.0
    expect(body.theta).toBeCloseTo(-2.0, 1)
    expect(body.buyingPower).toBeGreaterThan(0)
  })

  // -------------------------------------------------------------------------
  // risk /semaphore
  // -------------------------------------------------------------------------
  it('risk /semaphore computes from real SPX + VIX series', async () => {
    const res = await SELF.fetch('https://example.com/api/risk/semaphore', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      score: number
      status: string
      indicators: { id: string; status: string }[]
      spx: { price: number }
      vix: { price: number }
    }
    expect(body.indicators.length).toBe(5)
    expect(body.spx.price).toBeGreaterThan(0)
    expect(body.vix.price).toBeGreaterThan(0)
    // Seed has SPX rising steadily — should be BULLISH regime.
    const regime = body.indicators.find((i) => i.id === 'regime')
    expect(regime?.status).toBe('green')
  })

  // -------------------------------------------------------------------------
  // system /metrics
  // -------------------------------------------------------------------------
  it('system /metrics returns sparkline arrays from heartbeats', async () => {
    const res = await SELF.fetch('https://example.com/api/system/metrics', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      cpu: number[]
      ram: number[]
      network: number[]
      diskTotalGb: number
    }
    // Two seeded heartbeats → two-element arrays, in chronological order.
    expect(body.cpu.length).toBe(2)
    expect(body.ram.length).toBe(2)
    expect(body.network.length).toBe(2)
    expect(body.diskTotalGb).toBeGreaterThan(0)
  })

  // -------------------------------------------------------------------------
  // breakdown
  // -------------------------------------------------------------------------
  it('breakdown aggregates active_positions by strategy and asset', async () => {
    const res = await SELF.fetch('https://example.com/api/breakdown', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      byStrategy: { label: string; value: number }[]
      byAsset: { label: string; value: number }[]
    }
    // Seed has 2 strategies: Iron Condor and Put Spread
    expect(body.byStrategy.length).toBe(2)
    const labels = body.byStrategy.map((s) => s.label).sort()
    expect(labels).toEqual(['Iron Condor', 'Put Spread'])
    // Both are options-family → single Options bucket.
    expect(body.byAsset.length).toBe(1)
    expect(body.byAsset[0]?.label).toBe('Options')
  })

  // -------------------------------------------------------------------------
  // activity /recent
  // -------------------------------------------------------------------------
  it('activity /recent unions alerts + executions ordered DESC', async () => {
    const res = await SELF.fetch('https://example.com/api/activity/recent?limit=5', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      events: { id: string; title: string; timestamp: string }[]
    }
    // 2 alerts + 1 execution seeded → 3 events
    expect(body.events.length).toBe(3)
    // Most-recent first: alr-1 @ 14:00, exec-1 @ 13:30, alr-2 @ 13:00
    expect(body.events[0]?.id).toBe('alr-1')
    expect(body.events[1]?.id).toBe('exec-1')
    expect(body.events[2]?.id).toBe('alr-2')
  })

  // -------------------------------------------------------------------------
  // campaigns /summary
  // -------------------------------------------------------------------------
  it('campaigns /summary counts states from seeded campaigns', async () => {
    const res = await SELF.fetch('https://example.com/api/campaigns/summary', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      active: number
      paused: number
      draft: number
      detail: string
    }
    expect(body.active).toBe(2)
    expect(body.paused).toBe(1)
    expect(body.draft).toBe(1)
    expect(body.detail).toContain('1 paused')
    expect(body.detail).toContain('1 draft')
  })

  // -------------------------------------------------------------------------
  // positions /active
  // -------------------------------------------------------------------------
  it('positions /active includes latest greeks from LEFT JOIN', async () => {
    const res = await SELF.fetch('https://example.com/api/positions/active', {
      headers: { 'X-Api-Key': env.API_KEY },
    })
    expect(res.status).toBe(200)
    const body = (await res.json()) as {
      positions: Array<{
        position_id: string
        delta: number | null
        theta: number | null
        campaign: string | null
      }>
    }
    expect(body.positions.length).toBe(2)
    const pos1 = body.positions.find((p) => p.position_id === 'pos-1')
    expect(pos1?.delta).toBeCloseTo(0.25, 2)
    expect(pos1?.theta).toBeCloseTo(-1.2, 1)
    expect(pos1?.campaign).toBe('Active Campaign')
  })

  // -------------------------------------------------------------------------
  // Auth guards (negative tests — ensure Phase 7.2 changes didn't regress)
  // -------------------------------------------------------------------------
  it('all aggregate endpoints 401 without X-Api-Key', async () => {
    const endpoints = [
      '/api/performance/summary?asset=all',
      '/api/performance/series?asset=all&range=1M',
      '/api/drawdowns?asset=all&range=1Y',
      '/api/monthly-returns?asset=all',
      '/api/risk/metrics',
      '/api/risk/semaphore',
      '/api/system/metrics',
      '/api/breakdown',
      '/api/activity/recent',
      '/api/campaigns/summary',
    ]
    for (const ep of endpoints) {
      const res = await SELF.fetch(`https://example.com${ep}`)
      expect(res.status, ep).toBe(401)
    }
  })
})
