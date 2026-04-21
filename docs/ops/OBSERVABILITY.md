# Observability (Phase 7.3)

> How the trading system emits and consumes operational signals.
> Audience: operator / on-call. Written 2026-04-21.
> Covers: logs, metrics, errors, RUM (web vitals), uptime.

---

## 1. Layers at a glance

| Signal           | Producer                           | Transport           | Storage / Sink              |
|------------------|------------------------------------|---------------------|-----------------------------|
| Structured logs  | .NET Serilog, Worker, Dashboard    | HTTPS POST batched  | D1 `service_logs`           |
| Metrics          | Worker (every route)               | Analytics Engine    | CF dataset `trading_system_metrics` |
| Browser errors   | Dashboard                          | Sentry SDK          | Sentry project              |
| Web Vitals (RUM) | Dashboard `web-vitals` lib         | HTTPS → Worker      | D1 `web_vitals`             |
| Uptime           | UptimeRobot (external)             | HTTPS probe         | UptimeRobot + Telegram      |

---

## 2. Structured Logging Pipeline

### 2.1 Flow

```
+--------------------------+      +-------------------+      +----------------+
| .NET Serilog (batch)     | ---> |  Worker           | ---> |  D1            |
|   TradingSupervisor     |      |  POST /api/v1/logs|      |  service_logs  |
|   OptionsExecution       |      |  (auth: X-Api-Key)|      |                |
|   Dashboard breadcrumbs  |      +-------------------+      +----------------+
+--------------------------+
```

### 2.2 Request envelope

```json
{
  "batch": [
    {
      "ts": "2026-04-21T10:15:30.123Z",
      "level": "info",
      "service": "supervisor",
      "message": "Heartbeat ok",
      "source_context": "TradingSupervisorService.Workers.HeartbeatWorker",
      "properties": {
        "correlation_id": "abc-123",
        "trading_mode": "paper"
      },
      "exception": null
    }
  ]
}
```

Accepted `level` values: `trace | debug | info | warn | error | critical`.
`service` values in active use: `supervisor`, `options-execution`, `worker`, `dashboard`.

Response: `{ "accepted": <int> }` — the count the Worker wrote to D1.

### 2.3 Schema (D1 `service_logs`)

```sql
CREATE TABLE service_logs (
  service TEXT NOT NULL,
  ts TEXT NOT NULL,                -- ISO 8601
  sequence INTEGER NOT NULL DEFAULT 0,  -- per-batch index (dedupe key)
  level TEXT NOT NULL,
  message TEXT NOT NULL,
  properties TEXT,                 -- JSON blob
  source_context TEXT,
  exception_type TEXT,
  exception_message TEXT,
  exception_stack TEXT,
  created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (service, ts, sequence)
);

CREATE INDEX idx_service_logs_service_ts ON service_logs (service, ts DESC);
CREATE INDEX idx_service_logs_level_ts ON service_logs (level, ts DESC);
```

### 2.4 Idempotency

PK = `(service, ts, sequence)`. The `sequence` is assigned by the Worker as
the entry's index inside the batch envelope (0..N-1). Replaying the exact
same batch after a network blip does NOT create duplicates — the
`INSERT OR REPLACE` re-writes the same row.

### 2.5 Retention (TODO — Phase 7.4+)

No retention cron yet. Manual cleanup until then:

```sql
-- Keep last 30 days
DELETE FROM service_logs WHERE ts < date('now','-30 days');
```

Planned: scheduled Worker cron firing nightly at 03:00 UTC.

### 2.6 Example queries

```sql
-- Last hour, error or worse, any service
SELECT service, ts, level, message
FROM service_logs
WHERE level IN ('error','critical')
  AND ts >= datetime('now','-1 hour')
ORDER BY ts DESC LIMIT 100;

-- Error rate per service (24h)
SELECT service, COUNT(*) AS cnt
FROM service_logs
WHERE level IN ('error','critical')
  AND ts >= datetime('now','-24 hour')
GROUP BY service;

-- Correlate by request_id (structured property)
SELECT ts, level, message
FROM service_logs
WHERE json_extract(properties, '$.correlation_id') = 'abc-123'
ORDER BY ts;
```

---

## 3. Metrics (Cloudflare Analytics Engine)

### 3.1 Binding

```toml
# wrangler.toml
[[analytics_engine_datasets]]
binding = "METRICS"
dataset = "trading_system_metrics"
```

### 3.2 Emitted metric names

| Name                   | Tags (blobs, sorted)                    | Emitted from         |
|------------------------|-----------------------------------------|----------------------|
| `ingest.event_type`    | `status`, `type`                        | `routes/ingest.ts`   |
| `logs.batch`           | `service`, `status`                     | `routes/logs.ts`     |
| `auth.failure`         | `reason` ∈ {missing,invalid}, `route`  | `middleware/auth.ts` |
| `d1.error`             | `route`                                 | `routes/ingest.ts`, `routes/logs.ts` |

All metrics are counters (doubles=[1]). Fire-and-forget: the helper
(`src/lib/metrics.ts → recordMetric`) catches and logs any error from
`writeDataPoint`, never propagates it.

### 3.3 Querying

**Cloudflare dashboard**: Workers & Pages → pick `ts` worker → Analytics tab.

**SQL API**: requires an API token with `Account Analytics Read`:

```bash
curl -X POST 'https://api.cloudflare.com/client/v4/accounts/<acct>/analytics_engine/sql' \
  -H 'Authorization: Bearer <token>' \
  -H 'Content-Type: application/json' \
  --data '{"query": "SELECT blob2 as status, SUM(_sample_interval) FROM trading_system_metrics WHERE blob1 = ''ingest.event_type'' AND timestamp > NOW() - INTERVAL ''1'' HOUR GROUP BY blob2"}'
```

### 3.4 When a metric is absent

The `METRICS` binding is optional in `Env`. Local `wrangler dev` without the
binding runs cleanly: `recordMetric` checks for presence and silently no-ops.
Test suites exploit the same behavior (no real dataset needed).

---

## 4. Sentry (Dashboard errors)

### 4.1 Setup

1. Create project at https://sentry.io → new Project → React → note the DSN.
2. Set it as a Cloudflare Pages env var `VITE_SENTRY_DSN` (Pages → Settings →
   Environment Variables → Production).
3. Local dev: copy `dashboard/.env.local.example` → `.env.local` and paste DSN.
4. Empty DSN means disabled — the init block short-circuits.

### 4.2 Configuration (see `dashboard/src/main.tsx`)

```ts
Sentry.init({
  dsn,
  integrations: [Sentry.browserTracingIntegration()],
  tracesSampleRate: 0.1,           // 10% transactions sampled
  replaysSessionSampleRate: 0,     // session replays OFF (MVP)
  replaysOnErrorSampleRate: 0.5,   // replays ON only when error fires
  environment: import.meta.env.MODE
})
```

Why session replay is off: it can add 50-100 KB/min of bandwidth per active
session. For a solo-dev dashboard the value is low; we turn it on only for
error occurrences (50% sample) which is enough for triage.

### 4.3 ErrorBoundary

The root tree is wrapped in `<Sentry.ErrorBoundary fallback={<ErrorFallback />}>`.
Render errors surface in Sentry + show a clean "Something went wrong" card
with a Reload button (see `components/ui/ErrorFallback.tsx`). Unhandled
promise rejections flow to Sentry via its global handler.

### 4.4 Triage flow

1. New error lands in Sentry → GitHub issue auto-created via Sentry's GitHub
   integration (configure once per project).
2. Operator opens the issue, checks stack trace + breadcrumbs.
3. Fix lands on a branch; PR auto-links via "fixes SENTRY-ID" in commit msg.
4. Mark resolved in Sentry → auto-closes the GitHub issue.

---

## 5. Web Vitals (Real User Monitoring)

### 5.1 What's measured

| Metric | Description                              | Good    | Needs work | Poor    |
|--------|------------------------------------------|---------|------------|---------|
| CLS    | Cumulative Layout Shift                  | ≤ 0.1   | ≤ 0.25     | > 0.25  |
| INP    | Interaction to Next Paint                | ≤ 200ms | ≤ 500ms    | > 500ms |
| LCP    | Largest Contentful Paint                 | ≤ 2.5s  | ≤ 4.0s     | > 4.0s  |
| FCP    | First Contentful Paint                   | ≤ 1.8s  | ≤ 3.0s     | > 3.0s  |
| TTFB   | Time to First Byte                       | ≤ 800ms | ≤ 1.8s     | > 1.8s  |

Thresholds from web.dev (2026 vintage). The `web-vitals` library emits the
`rating` bucket directly so the Worker stores it without recomputation.

### 5.2 Flow

```
Browser → web-vitals lib → reportMetric() → api.post('v1/ingest', {event_type:'web_vitals',payload})
  → Worker validates Zod → D1 web_vitals table
```

Session id is stashed in `sessionStorage` per-tab; all metrics from the same
page visit share the same `session_id`.

### 5.3 Example queries

```sql
-- p75 LCP over the last 24h
SELECT name, value
FROM web_vitals
WHERE name = 'LCP' AND timestamp >= datetime('now','-24 hour')
ORDER BY value;
-- then client-side pick the 75th percentile

-- % of sessions with at least one "poor" rating
SELECT COUNT(DISTINCT session_id) AS poor_sessions
FROM web_vitals
WHERE rating = 'poor' AND timestamp >= datetime('now','-7 days');
```

---

## 6. Uptime monitoring

Both .NET services expose a future local HTTP `/health` (Phase 7.3 .NET
side — implemented by the parallel agent). The Worker `/api/health`
already exists (no auth).

### 6.1 UptimeRobot (recommended, free tier)

1. Sign up at https://uptimerobot.com → New Monitor → HTTP(s).
2. URL: `https://trading-bot.padosoft.workers.dev/api/health`
3. Interval: 5 minutes
4. Alert contacts: add webhook pointing at the existing .NET
   `TelegramAlerter` HTTP endpoint (Phase 7.3 .NET side will expose it), or
   use Telegram's native UptimeRobot integration.

### 6.2 Alert routing

The `TelegramAlerter` already handles severity-routed alerts (CRITICAL →
immediate, WARNING → 15-min digest). Feed UptimeRobot downtime notifications
into the same channel by posting to its webhook with severity=CRITICAL.

---

## 7. TODO

- [ ] Log retention cron (nightly delete > 30 days) — Phase 7.4+
- [ ] Web Vitals retention cron (weekly delete > 90 days) — Phase 7.4+
- [ ] Tail Workers for real-time critical log streaming to Telegram
- [ ] Grafana Cloud integration (if we outgrow CF Analytics native UI)

---

*Related docs: `docs/ops/RUNBOOK.md` (failure playbooks),
`docs/ops/MARKET_DATA_PIPELINE.md` (Phase 7.1 ingest).*
