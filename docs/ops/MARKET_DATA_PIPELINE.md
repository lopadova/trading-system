---
title: "Market Data Pipeline (Phase 7.1)"
tags: ["ops", "architecture", "ibkr"]
aliases: ["Market Data Pipeline"]
status: current
audience: ["developer", "operator"]
phase: "phase-7.1"
last-reviewed: "2026-04-21"
related:
  - "[[OBSERVABILITY]]"
  - "[[Trading System Architecture|ARCHITECTURE]]"
  - "[[RUNBOOK]]"
---

# Market Data Pipeline (Phase 7.1)

> Ingestion path for real-time market + portfolio series into Cloudflare D1.
> Introduced by migration `0007_market_data.sql` and ingest route changes
> committed on branch `feat/phase7.1-market-data-ingestion`.
>
> Scope: **Worker side of the pipeline only**. The .NET-side collectors
> (`MarketDataCollector`, `BenchmarkCollector`, live-Greeks wiring for
> `GreeksMonitorWorker`) are tracked separately.

---

## 1. High-level flow

```
+----------+     +-------------------------+     +------------+     +-------+
|   IBKR   |     |  TradingSupervisor      |     | sync_outbox|     | Worker|
|   TWS    | --> |  .NET Workers           | --> | (SQLite)   | --> |  /api |
|  (Gateway)     |  MarketDataCollector    |     |            |     |  /v1/ |
|  + SDK)  |     |  BenchmarkCollector     |     |            |     |ingest |
|          |     |  GreeksMonitorWorker    |     |            |     |       |
+----------+     +-------------------------+     +------------+     +---+---+
                                                                        |
                                                                        v
                                                     +----------------------------+
                                                     |         D1 (SQLite)        |
                                                     | account_equity_daily       |
                                                     | market_quotes_daily        |
                                                     | vix_term_structure         |
                                                     | benchmark_series           |
                                                     | position_greeks            |
                                                     +----------------------------+
                                                                        |
                                                                        v
                                                     +----------------------------+
                                                     |  Dashboard aggregate       |
                                                     |  endpoints (Phase 7.2):    |
                                                     |  /performance, /drawdowns, |
                                                     |  /risk/semaphore, ...      |
                                                     +----------------------------+
```

Key invariants:

- **Outbox is at-least-once.** The Worker ingest handlers must be idempotent.
  Every table landing in Phase 7.1 uses a natural primary key so that
  `INSERT OR REPLACE` can safely re-apply the same event on a retry.
- **TradingMode=paper** until Phase 7.7 sign-off. The ingest schema is
  mode-agnostic; the supervisor tags the Outbox with the current mode in
  payload metadata if needed by downstream consumers.

---

## 2. Event types

Each event is POSTed to `/api/v1/ingest` with this envelope:

```json
{
  "event_id":   "<uuid>",
  "event_type": "<one of the types below>",
  "payload":    { /* shape depends on event_type */ }
}
```

The envelope is validated; the payload is validated by a per-type Zod schema
(see `infra/cloudflare/worker/src/routes/ingest.ts`). Any missing/malformed
field returns **400** with a flattened list of `{path, message}` issues.

### 2.1 `account_equity`

Daily NAV + margin snapshot. Primary key: `date`.

| field             | type     | required | notes                            |
|-------------------|----------|----------|----------------------------------|
| `date`            | string   | yes      | ISO date (`YYYY-MM-DD`)          |
| `account_value`   | number   | yes      | Total NAV                        |
| `cash`            | number   | yes      |                                  |
| `buying_power`    | number   | yes      |                                  |
| `margin_used`     | number   | yes      | Absolute margin consumption      |
| `margin_used_pct` | number   | yes      | `margin_used / account_value * 100` |

### 2.2 `market_quote`

OHLCV per symbol. Primary key: `(symbol, date)`.

| field    | type    | required | notes                  |
|----------|---------|----------|------------------------|
| `symbol` | string  | yes      | e.g. `SPX`, `SPY`      |
| `date`   | string  | yes      | ISO date               |
| `open`   | number  | no       | Nullable               |
| `high`   | number  | no       | Nullable               |
| `low`    | number  | no       | Nullable               |
| `close`  | number  | yes      |                        |
| `volume` | integer | no       | Nullable               |

### 2.3 `vix_snapshot`

Denormalized VIX term-structure for a given day. Primary key: `date`.

| field   | type   | required | notes                  |
|---------|--------|----------|------------------------|
| `date`  | string | yes      | ISO date               |
| `vix`   | number | no       | Nullable               |
| `vix1d` | number | no       | Nullable               |
| `vix3m` | number | no       | Nullable               |
| `vix6m` | number | no       | Nullable               |

**Side-effect**: every non-null leg is also mirrored into
`market_quotes_daily` (symbols: `VIX`, `VIX1D`, `VIX3M`, `VIX6M`) so the chart
endpoints can treat the curve as regular symbols.

### 2.4 `benchmark_close`

Pre-normalized benchmark close (chart overlay). PK: `(symbol, date)`.

| field              | type   | required | notes                             |
|--------------------|--------|----------|-----------------------------------|
| `symbol`           | string | yes      | e.g. `SP500`, `SWDA`              |
| `date`             | string | yes      | ISO date                          |
| `close`            | number | yes      | Raw close                         |
| `close_normalized` | number | no       | Base-100 from a fixed start date  |

### 2.5 `position_greeks`

Rolling Greeks snapshot per position. PK: `(position_id, snapshot_ts)`.

| field              | type   | required | notes                         |
|--------------------|--------|----------|-------------------------------|
| `position_id`      | string | yes      | Matches `active_positions.id` |
| `snapshot_ts`      | string | yes      | ISO 8601 timestamp            |
| `delta`            | number | no       | Nullable                      |
| `gamma`            | number | no       | Nullable                      |
| `theta`            | number | no       | Nullable                      |
| `vega`             | number | no       | Nullable                      |
| `iv`               | number | no       | Nullable (implied volatility) |
| `underlying_price` | number | no       | Nullable                      |

---

## 3. D1 Schema (migration 0007)

```sql
-- Daily equity snapshot
CREATE TABLE account_equity_daily (
    date TEXT PRIMARY KEY,
    account_value REAL NOT NULL,
    cash REAL NOT NULL,
    buying_power REAL NOT NULL,
    margin_used REAL NOT NULL,
    margin_used_pct REAL NOT NULL,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- OHLCV per symbol (SPX, VIX, SPY, ...)
CREATE TABLE market_quotes_daily (
    symbol TEXT NOT NULL,
    date TEXT NOT NULL,
    open REAL, high REAL, low REAL,
    close REAL NOT NULL,
    volume INTEGER,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (symbol, date)
);

-- Denormalized VIX curve per day
CREATE TABLE vix_term_structure (
    date TEXT PRIMARY KEY,
    vix REAL, vix1d REAL, vix3m REAL, vix6m REAL,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Pre-normalized benchmark closes (chart overlay)
CREATE TABLE benchmark_series (
    symbol TEXT NOT NULL,
    date TEXT NOT NULL,
    close REAL NOT NULL,
    close_normalized REAL,
    PRIMARY KEY (symbol, date)
);

-- Rolling Greeks per position
CREATE TABLE position_greeks (
    position_id TEXT NOT NULL,
    snapshot_ts TEXT NOT NULL,
    delta REAL, gamma REAL, theta REAL, vega REAL, iv REAL,
    underlying_price REAL,
    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (position_id, snapshot_ts)
);

-- Indexes (DESC on the time axis to keep "latest-N" queries cheap)
CREATE INDEX idx_market_quotes_symbol_date ON market_quotes_daily (symbol, date DESC);
CREATE INDEX idx_vix_date                  ON vix_term_structure (date DESC);
CREATE INDEX idx_benchmark_symbol_date     ON benchmark_series (symbol, date DESC);
CREATE INDEX idx_greeks_position           ON position_greeks (position_id, snapshot_ts DESC);
CREATE INDEX idx_equity_date               ON account_equity_daily (date DESC);
```

Full DDL lives in `infra/cloudflare/worker/migrations/0007_market_data.sql`.

---

## 4. Apply the migration

### Local dev (wrangler local D1)

```bash
cd infra/cloudflare/worker
bunx wrangler d1 migrations apply trading-db --local
```

### Production (remote D1)

```bash
cd infra/cloudflare/worker
bunx wrangler d1 migrations apply trading-db --remote
```

> Tip: Phase 7.1 deployment should run the migration BEFORE the Worker deploy
> so the new ingest handlers don't race against a pre-0007 schema. The
> deployment script `scripts/deploy-worker.sh` will be updated (Phase 7.3) to
> chain these calls automatically.

---

## 5. Verify

After applying the migration you should see the 5 new tables:

```bash
bunx wrangler d1 execute trading-db --local \
  --command "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name"
```

Expected (abridged): `account_equity_daily`, `benchmark_series`,
`market_quotes_daily`, `position_greeks`, `vix_term_structure`.

Row-level check once a few events have landed:

```bash
bunx wrangler d1 execute trading-db --local \
  --command "SELECT COUNT(*) FROM vix_term_structure"
bunx wrangler d1 execute trading-db --local \
  --command "SELECT * FROM account_equity_daily ORDER BY date DESC LIMIT 5"
```

---

## 6. Testing

- **Unit** (`bun run test:unit`): 23 new tests in `test/ingest.test.ts`
  covering valid / malformed / idempotent paths for all 5 event types.
- **Integration** (`bun run test:integration`): 7 new tests in
  `test/integration/phase7-ingest-integration.integration-spec.ts` exercising
  the real Worker against in-process D1.
  > Known local issue: vitest-pool-workers fails to resolve its worker dist
  > modules on Windows paths with spaces. The suite runs correctly on CI.

---

## 7. Related

- Migration: `infra/cloudflare/worker/migrations/0007_market_data.sql`
- Ingest route: `infra/cloudflare/worker/src/routes/ingest.ts`
- Types: `infra/cloudflare/worker/src/types/api.ts` (new
  `AccountEquityPayload`, `MarketQuotePayload`, `VixSnapshotPayload`,
  `BenchmarkClosePayload`, `PositionGreeksPayload`, `MarketDataEventType`)
- Unit tests: `infra/cloudflare/worker/test/ingest.test.ts`
- Integration tests:
  `infra/cloudflare/worker/test/integration/phase7-ingest-integration.integration-spec.ts`
- Parent plan: `docs/superpowers/specs/2026-04-20-dashboard-redesign-design.md`,
  `~/.claude/plans/spicy-wondering-flamingo.md` (Phase 7.1 section)

---

## 8. .NET Collector Details

This section documents the **producer side** of the pipeline — the .NET workers
that sit inside `TradingSupervisorService` and feed `sync_outbox`.

### 8.1 Worker roster

| Worker                  | File | Emits                                          | Cadence                          |
|-------------------------|------|------------------------------------------------|----------------------------------|
| `MarketDataCollector`   | `src/TradingSupervisorService/Workers/MarketDataCollector.cs` | `market_quote`, `vix_snapshot`, `account_equity` | 15 s (quotes) / 60 s (account)   |
| `BenchmarkCollector`    | `src/TradingSupervisorService/Workers/BenchmarkCollector.cs`  | `benchmark_close`                                | Once per UTC day at 22:30 UTC    |
| `GreeksMonitorWorker`   | `src/TradingSupervisorService/Workers/GreeksMonitorWorker.cs` | `position_greeks` (+ existing `alert`)           | On every tickOptionComputation   |

Cadences are configurable in `appsettings.json` (`MarketDataCollector:*`,
`BenchmarkCollector:*`, `GreeksMonitor:*`).

### 8.2 MarketDataCollector

Responsibilities:

1. On start, wait up to 5 min for `IIbkrClient.IsConnected`.
2. Subscribe to SPX, VIX and VIX3M on CBOE (`secType=IND`, `exchange=CBOE`)
   via `IIbkrClient.RequestMarketData` (genericTickList empty → default quote
   set).
3. Register three event hooks on `TwsCallbackHandler`:
   `TickPriceReceived`, `TickSizeReceived`, `AccountSummaryReceived`.
4. Track per-symbol OHLCV state in a thread-safe in-memory cache.
5. Every `QuoteIntervalSeconds` (default **15s**):
   - For each symbol that has received a fresh LAST tick, queue one
     `market_quote` Outbox event.
   - Queue one combined `vix_snapshot` event (VIX + VIX3M).
6. Every `AccountIntervalSeconds` (default **60s**):
   - Call `IIbkrClient.RequestAccountSummary(6100)`.
   - Aggregate incoming `accountSummary` tags (NetLiquidation, TotalCashValue,
     BuyingPower, MaintMarginReq/FullInitMarginReq) into an `account_equity` event.

ReqId allocation: SPX=6001, VIX=6002, VIX3M=6003, AccountSummary=6100
(distinct from IvtsMonitor 5001-5004 and GreeksMonitor 7000+).

### 8.3 BenchmarkCollector

Responsibilities:

1. Wake every `CheckIntervalMinutes` (default 30 min) — cheap clock check.
2. Fire exactly once per UTC calendar day, once current UTC time ≥ `DailyRunTimeUtc`
   (default **22:30 UTC**, chosen to sit after the US close at 21:00-21:15 UTC
   and before the European session opens the next day, so closes are fully
   posted on Stooq / Yahoo).
3. For each symbol in `Symbols` (default SPX, SWDA):
   - **Primary**: Stooq CSV (`https://stooq.com/q/d/l/?s={sym}&i=d`).
   - **Fallback**: Yahoo Finance v8 chart JSON.
   - **On double failure**: WARNING log + `ITelegramAlerter.QueueAlertAsync`.
4. Dedupe locally via `supervisor.db.benchmark_fetch_log` — only insert Outbox
   events for dates strictly newer than `last_fetched_date` (cap: newest
   `MaxBackfillRows=30` rows on cold start).

Event dedupe key: `benchmark_close:{symbol}:{date}` → idempotent replays.

### 8.4 GreeksMonitorWorker (Phase 7.1 upgrade)

Pre-Phase-7.1 behavior (preserved):

- Every `IntervalSeconds`, scan `active_positions` from `options.db` and raise
  threshold alerts on delta / gamma / theta / vega breaches.

Phase 7.1 additions (when `GreeksMonitor:LiveTicksEnabled=true` **and** the
worker is resolved with non-null IBKR / callback / db dependencies):

1. Each cycle reconciles tick subscriptions with the current open-positions set:
   - New positions → subscribe via
     `IIbkrClient.RequestMarketData(reqId, symbol, "OPT", "SMART", "USD", genericTickList="106,100")`.
   - Closed positions → `CancelMarketData`.
2. On every `TwsCallbackHandler.TickOptionComputationReceived`:
   - Update an in-memory `CachedGreeks` dict.
   - Fire-and-forget a persistence task that:
     - `INSERT OR IGNORE` into `position_greeks_cache` (supervisor.db).
     - `INSERT OR IGNORE` a `position_greeks` Outbox event (dedupe
       `position_greeks:{pid}:{yyyyMMddTHHmmssZ}`).
3. Threshold checks prefer cached values (when < 2·IntervalSeconds old) over
   the options.db mirror — so the alerts fire on *fresh* data.

### 8.5 IBKR tick IDs used

| Generic tick | Meaning                         | Used by                     |
|--------------|---------------------------------|-----------------------------|
| `""` (empty) | Default IBKR tick set (LAST, BID, ASK, OPEN, HIGH, LOW, CLOSE, VOLUME) | MarketDataCollector |
| `"100"`      | Option Greeks (delta, gamma, theta, vega, underlying) | GreeksMonitorWorker |
| `"106"`      | Option implied volatility       | GreeksMonitorWorker         |

Tick-type constants used when routing `tickPrice` fields:

| Field | Meaning          | Consumer                    |
|-------|------------------|-----------------------------|
| 4     | LAST             | OHLC last-price tracking    |
| 6     | HIGH (session)   | OHLC high                   |
| 7     | LOW (session)    | OHLC low                    |
| 8     | VOLUME (tickSize)| OHLC volume                 |
| 9     | CLOSE (previous) | Prior-day close             |
| 14    | OPEN (session)   | OHLC open                   |

### 8.6 Local cache tables (supervisor.db)

Migration `003_MarketDataCache` (`src/TradingSupervisorService/Migrations/003_MarketDataCache.cs`):

- **`benchmark_fetch_log(symbol, last_fetched_date, last_success_ts, last_error, last_error_ts, source)`**
  Tracks the freshest date queued per benchmark symbol. Drives daily dedupe.

- **`position_greeks_cache(position_id, snapshot_ts, delta, gamma, theta, vega, iv, underlying_price)`**
  Rolling per-position Greeks snapshots captured from live IBKR ticks.
  Index `idx_pg_cache_position_ts` supports the "latest snapshot per position"
  query. Index `idx_pg_cache_ts` supports retention pruning.

### 8.7 Graceful degradation

| Failure mode                 | Behavior                                                     |
|------------------------------|--------------------------------------------------------------|
| IBKR disconnected at startup | `WaitForIbkrConnectionAsync` polls for up to 5 min, then the worker exits cleanly. It will restart on next host cycle. |
| IBKR drops mid-session       | `IbkrClient` reconnect logic runs (existing). Collectors skip emissions where `IsConnected==false` and log a WARNING. |
| No ticks yet for a symbol    | `MarketDataCollector` skips that symbol's `market_quote` event for the cycle — no stale or zero-filled payload. |
| Stooq returns HTML / empty   | BenchmarkCollector falls back to Yahoo.                       |
| Yahoo also fails             | Telegram alert queued; `benchmark_fetch_log.last_error*` updated; retry next daily window. |
| Outbox insert fails          | Exception logged, worker continues. Outbox is retried by `OutboxSyncWorker` once rows exist. |
| position_greeks_cache insert fails | Logged, worker continues; the in-memory `CachedGreeks` is still used for threshold checks. |

### 8.8 Culture safety

All numeric strings that ship into Outbox payloads use
`CultureInfo.InvariantCulture` (decimal point, 24-hour `HH:mm`, ISO 8601 dates
— see knowledge/errors-registry.md ERR-015). Dedupe keys, timestamps and CSV
parsing are invariant too, so the collectors are safe on Italian / any non-US
locale Windows hosts.

---

## 9. Consuming the data — Phase 7.2 aggregate endpoints

Phase 7.2 replaced every hardcoded-mock aggregate route in
`infra/cloudflare/worker/src/routes/` with real D1 queries. This section
documents each endpoint, the D1 query it now runs, and the fallback
behavior when the underlying tables are empty.

### 9.1 Endpoint → SQL map

| Endpoint | Table(s) | One-line SQL |
|---|---|---|
| `GET /api/performance/summary` | `account_equity_daily` | `SELECT date, account_value FROM account_equity_daily ORDER BY date ASC` (then compute M/YTD/2Y/5Y/10Y + annualized in JS) |
| `GET /api/performance/series` | `account_equity_daily`, `benchmark_series` | Same equity query cropped per range + `SELECT symbol, date, close, close_normalized FROM benchmark_series WHERE symbol IN ('SPX','SWDA') AND date BETWEEN ? AND ?` |
| `GET /api/drawdowns` | `account_equity_daily`, `market_quotes_daily` | Window function: `MAX(account_value) OVER (ORDER BY date ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW)` → `(account_value - peak) / peak * 100`. Same shape against `market_quotes_daily` for SPX. |
| `GET /api/monthly-returns` | `account_equity_daily` | CTE with `ROW_NUMBER() OVER (PARTITION BY strftime('%Y-%m', date) ORDER BY date ASC/DESC)` to pair first and last monthly values, then monthly return = `(last - first) / first * 100` |
| `GET /api/risk/metrics` | `vix_term_structure`, `position_greeks`, `account_equity_daily`, `market_quotes_daily` | Latest VIX curve row + `SUM(delta/theta/vega)` over latest `position_greeks` snapshot per position + latest account_equity row + SPX 1Y percentile |
| `GET /api/risk/semaphore` | `market_quotes_daily` (SPX), `vix_term_structure` | SPX close vs `AVG(close)` over last 200; VIX current percentile in 1Y series; rolling-yield mean over 30d and its percentile; IVTS = VIX / VIX3M |
| `GET /api/system/metrics` | `service_heartbeats` | `SELECT last_seen_at, cpu_percent, ram_percent, disk_free_gb FROM service_heartbeats ORDER BY last_seen_at DESC LIMIT 20` (reversed to chronological) |
| `GET /api/breakdown` | `active_positions` | `SELECT strategy_name, SUM(ABS(quantity * COALESCE(current_price, entry_price))) AS value FROM active_positions GROUP BY strategy_name ORDER BY value DESC` |
| `GET /api/activity/recent` | `alert_history`, `execution_log` | `UNION ALL` of the two sources with unified columns, ordered by `ts DESC LIMIT ?limit=` |
| `GET /api/campaigns/summary` | `campaigns` | `SELECT state, COUNT(*) FROM campaigns GROUP BY state` |
| `GET /api/positions/active` | `active_positions`, `campaigns`, `position_greeks` | Base query + LEFT JOIN to `campaigns.name` and to `position_greeks` on `(position_id, MAX(snapshot_ts))` CTE for latest greeks |

### 9.2 Fallback behavior

When a source table is empty (fresh install, no collectors running yet) or a
query throws, the route returns the pre-Phase-7.2 deterministic mock payload
AND sets the response header `X-Data-Source: fallback-mock`. The dashboard
can inspect this header to surface a "using demo data" indicator in a later
milestone.

Exception: `GET /api/campaigns/summary` treats an empty `campaigns` table as
a legitimate production state (zero campaigns on a fresh install) and
returns `{active: 0, paused: 0, draft: 0, detail: 'no campaigns'}` WITHOUT
the fallback-mock header. Only DB errors trigger fallback on that route.

### 9.3 Testing endpoints against local D1

```bash
# 1. Apply migrations (creates the 5 Phase 7.1 tables + anything from 0001-0006)
cd infra/cloudflare/worker
bunx wrangler d1 migrations apply trading-db --local

# 2. Seed a few rows
bunx wrangler d1 execute trading-db --local --command \
  "INSERT OR REPLACE INTO account_equity_daily (date, account_value, cash, buying_power, margin_used, margin_used_pct) VALUES ('2026-04-20', 150000, 60000, 250000, 20000, 13.33)"

bunx wrangler d1 execute trading-db --local --command \
  "INSERT OR REPLACE INTO vix_term_structure (date, vix, vix1d, vix3m) VALUES ('2026-04-20', 15.2, 14.1, 17.9)"

# 3. Start the dev server
bun run dev

# 4. Hit the aggregate endpoints with your API_KEY
curl -H "X-Api-Key: $API_KEY" http://127.0.0.1:8787/api/performance/summary?asset=all
curl -H "X-Api-Key: $API_KEY" http://127.0.0.1:8787/api/risk/metrics
curl -H "X-Api-Key: $API_KEY" http://127.0.0.1:8787/api/risk/semaphore
curl -H "X-Api-Key: $API_KEY" http://127.0.0.1:8787/api/drawdowns?asset=all&range=1Y
```

When the tables are empty the same curl calls return the mock payload plus
an `X-Data-Source: fallback-mock` response header — a handy way to detect
"no collectors running" without reading the D1 tables directly.

### 9.4 Asset-bucket decomposition (TODO Phase 7.x)

Performance and drawdowns currently scale the "all" bucket values by a
deterministic per-asset factor to keep visible differentiation between the
systematic/options/other buckets. Real per-asset decomposition requires an
equity-by-strategy time series which is not yet emitted by any collector.
See the `TODO(Phase 7.x)` markers in `performance.ts`, `drawdowns.ts`,
`monthly-returns.ts` and the `STRATEGY_TO_ASSET` heuristic in `breakdown.ts`.
