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
