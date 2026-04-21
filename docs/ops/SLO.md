# Service Level Objectives (SLO)

> Phase 7.7 deliverable. Defines the measurable reliability targets that the
> trading system commits to, how each one is measured, how error budget is
> consumed, and what the operator does when a budget is burning.
>
> Audience: operator / on-call. Every objective below has a concrete query
> or endpoint an operator can hit to know, right now, whether we're inside
> or outside the budget.
>
> Last updated: 2026-04-21.

---

## 1. Why this document exists

An SLO without a measurement method is an aspiration. An aspiration is not
operable — you cannot page an operator on "feels slow today". Every SLO
below is pinned to (a) a specific time window, (b) a specific data source,
(c) a specific query/endpoint, and (d) a concrete operator action when the
objective is breached. If any of those four are fuzzy, the SLO is broken
and must be fixed before it's declared live.

Scope: **production**. Staging has the same shape but looser thresholds
(see § 5).

---

## 2. SLO summary

| #   | Objective                        | Target      | Window  | Budget / month |
|-----|----------------------------------|-------------|---------|----------------|
| S1  | Dashboard + Worker availability  | 99%         | 30 days | 7h 18m         |
| S2  | Order placement latency P95      | < 500ms     | 30 days | 5% slow        |
| S3  | D1 aggregate data freshness      | < 5 min lag | market  | 1% stale ticks |
| S4  | Ingest success rate              | 99.5%       | 30 days | 0.5% dropped   |

Targets beyond the four above (e.g. Telegram alert delivery, Sentry
ingestion) are monitored but not SLO-ized — they're single-fault
components on an external provider and the operator tolerates longer
recovery.

---

## 3. SLO definitions

### S1 — Availability (Worker + Dashboard)

**Objective**: 99.0% of all authenticated Worker API requests AND all
dashboard page loads succeed (2xx/3xx for Worker, HTTP 200 + hydrated
React tree for Dashboard) over a rolling 30-day window.

**Why 99% and not 99.9%**: single-maintainer project, free-tier
Cloudflare, unavoidable D1 / Worker maintenance windows upstream. 99%
is honest. A 99.9% claim would require multi-region failover and a
paid Workers plan — both are Phase 8+ work.

**Error budget**: `30 days × 24h × 60m × (1 - 0.99) = 432 min ≈ 7h 18m`
per rolling month of unavailability before the SLO is breached.

**Measurement**:

- **Worker**: Cloudflare Analytics `requests.status_code` per
  `trading-bot.padosoft.workers.dev`. Failure counts any 5xx. 4xx
  counts as success for SLO purposes (client-side drift, not our
  fault).
- **Dashboard**: UptimeRobot external probe every 5 min against
  `https://trading-dashboard.pages.dev/` — check both the HTTP 200
  AND the presence of the string `id="root"` in the response body
  (so a blank shell = failure). UptimeRobot computes rolling
  availability and emails when < 99% over 24h.

**Operator query** (Worker side, Cloudflare built-in HTTP request
analytics). The Worker's custom `trading_system_metrics` dataset does
NOT emit per-request `status_code` samples — `recordMetric()` only fires
for domain-specific events (auth failures, ingest outcomes). Use
Cloudflare's built-in `http_requests` dataset for availability instead:

```sql
SELECT
  SUM(CASE WHEN status_code >= 500 THEN 1 ELSE 0 END) AS failures,
  COUNT(*) AS total,
  1.0 - (SUM(CASE WHEN status_code >= 500 THEN 1 ELSE 0 END) * 1.0 / COUNT(*)) AS availability
FROM http_requests
WHERE timestamp > NOW() - INTERVAL '30' DAY
  AND host = 'trading-bot.padosoft.workers.dev';
```

If a custom per-request metric (incl. status_code as a tag) is ever
added to `trading_system_metrics`, update both sides together and
migrate the query.

Expected: `availability >= 0.99`. A result `< 0.99` means the budget
is fully consumed for the rolling window.

**Alerting thresholds**:

- **Warning (50% burn)**: budget consumption reaches 3h 39m in a
  rolling 7-day window → Telegram `warn`.
- **Critical (75% burn)**: budget consumption reaches 5h 29m in
  rolling 7-day → Telegram `critical`, operator reviews within 1h.
- **Breach (100% burn)**: 7h 18m consumed in rolling 30-day →
  operator MUST open a retrospective GitHub issue for the breach
  and include: SLO name, time window, minutes consumed, root cause,
  remediation status. A dedicated issue template under
  `.github/ISSUE_TEMPLATE/slo-breach.md` is a future enhancement —
  until then use a free-form issue with the `slo-breach` label.

**On-call action on alert**:

1. Check `https://www.cloudflarestatus.com` — if red, this is
   upstream, document and ride it out; the incident still counts
   against the budget.
2. If not upstream: follow RUNBOOK.md Playbook 1 (Worker 502 / 500).
3. Post a summary in the retro issue: root cause + minutes
   consumed + whether a fix shipped.

**Escalation**: solo-op project. If the operator is unavailable and
the breach persists > 4h, Lorenzo receives a second Telegram ping
on the `trading-system-critical` channel.

---

### S2 — Order placement latency (P95)

**Objective**: P95 of `OrderPlacer.PlaceOrderAsync` wall time, measured
from method entry to IBKR `orderStatus` ACK (status transitions to
`Submitted`, `PreSubmitted`, or `Filled`), is below 500ms over a
rolling 30-day window of **live market hours only**.

**Why 500ms and not 100ms**: IBKR API is rate-limited at 50 messages/sec
and the smart-routing round-trip for a new option order typically
lands at 150-350ms against TWS on localhost, with occasional spikes
to 800ms+ during volatile open/close auctions. 500ms is a realistic
ceiling that catches genuine pathology (SDK stall, GC pressure, network
blip) without flagging normal fluctuation.

**Error budget**: 5% of in-hours orders may exceed 500ms. If we send
200 orders/month in live (typical paper-run target: ~10-20/day during
the 14-day validation), budget is ~10 slow orders.

**Measurement**: the Options Execution Service already writes a row
to `order_audit_log` (local SQLite) and `order_audit_log` on D1 for
every order attempt. The `latency_ms` column captures end-to-end wall
time from `PlaceOrderAsync` entry to ACK (see
`src/OptionsExecutionService/Brokers/OrderPlacer.cs` — look for the
Stopwatch around the IBKR call).

**Operator query** (D1, via Worker admin endpoint):

```bash
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  "https://trading-bot.padosoft.workers.dev/api/audit/orders?from=$(date -d '30 days ago' +%Y-%m-%d)&outcome=placed&limit=1000" \
  | jq '[.rows[].latency_ms] | sort | .[((length * 0.95) | floor)]'
```

Expected: output `<= 500`. If > 500, the P95 target is breached.

**Alerting thresholds**:

- **Warning**: single day's P95 > 600ms → Telegram `warn` at EOD.
- **Critical**: rolling 7-day P95 > 500ms → Telegram `critical`,
  operator halts live trading until investigated (set
  `Safety:KillSwitchAllServices=true`, restart).
- **Breach**: 30-day P95 > 500ms → SLO breach issue.

**On-call action on alert**:

1. Identify pathology source from audit log: which
   `strategy_id` / `contract_symbol` shows latency tail?
2. Check IBKR TWS latency: `ibkrLatency` counter in
   `OptionsExecutionService` logs. If > 300ms on the IBKR side,
   the problem is upstream of us — record, ride out.
3. If IBKR is fine, suspect our side: .NET GC pause
   (`dotnet-counters monitor System.Runtime`), DB lock
   contention (`PRAGMA wal_checkpoint(PASSIVE);`), or recent
   code change in the hot path.
4. If unclear, pause live trading per above and open a priority
   issue.

**Escalation**: same as S1 (solo-op).

---

### S3 — D1 aggregate data freshness

**Objective**: During market hours (NY 09:30-16:00 ET, weekdays),
every D1 aggregate table used by the dashboard widgets has a
`max(ts)` or equivalent most-recent row within 5 minutes of wall
clock.

Covered tables (per `infra/cloudflare/worker/migrations/`):

| Table                 | Freshness check                               |
|-----------------------|-----------------------------------------------|
| `market_quotes_daily` | `MAX(ts)` within 5 min of now                 |
| `vix_term_structure`  | `MAX(captured_at)` within 5 min of now        |
| `position_greeks`     | `MAX(snapshot_ts)` within 5 min of now        |
| `account_equity_daily`| `MAX(as_of_date) = today` during market hours |
| `benchmark_series`    | `MAX(ts)` within 5 min of now                 |

**Why 5 min and not 30s**: the supervisor polling cadences
(MarketDataCollector = 30s, BenchmarkCollector = 60s,
GreeksMonitorWorker = 60s) plus outbox drain latency (~5-15s)
plus ingest latency (~100ms) put the real lag in the 1-3 min range
on a good day. 5 min is a generous ceiling that still catches real
stall conditions.

**Error budget**: 1% of 5-min sampling windows during market
hours may be stale. Market day = 390 minutes; 1% = ~4 minutes
of stale data per day before the SLO is consumed.

**Measurement**: the existing `scripts/verify-data-freshness.sh`
(and its PowerShell twin `scripts/Verify-DataFreshness.ps1`) is the
reference implementation. The `.github/workflows/data-freshness.yml`
runs it every 15 minutes during market hours and emits Telegram
`warn` on any stale table.

**Operator query** (run locally against the live Worker):

```bash
./scripts/verify-data-freshness.sh --env production
# PowerShell equivalent:
.\scripts\Verify-DataFreshness.ps1 -Environment production
```

Expected exit code: `0`. The script prints a per-table summary table
with age and a fresh/stale verdict for each. A non-zero exit code
means at least one table is stale and the script lists which.

Note: a direct HTTP endpoint (e.g. `/api/admin/freshness`) returning
a JSON blob equivalent to the script's output is a Phase 8+ wish —
until then, the script is the canonical measurement path, and the
scheduled workflow is the canonical alerting path.

**Alerting thresholds**:

- **Warning**: one table `age_seconds > 300` for two consecutive
  15-min checks → Telegram `warn`.
- **Critical**: two or more tables stale simultaneously, OR one
  table stale for ≥ 4 consecutive checks (= ~1 hour of budget
  consumed in a single incident) → Telegram `critical`.
- **Breach**: > 4 minutes stale per market day on average over
  the rolling 30 days → SLO breach issue.

**On-call action on alert**:

1. Which table? There is no `/api/admin/freshness` endpoint yet
   (Phase 8+ wish — see the Measurement section above). Consult the
   **actual signal sources**: (a) the last run output of
   `scripts/verify-data-freshness.sh --env production`, or (b) the
   `Data Freshness Check` workflow run in GitHub Actions — the
   `freshness-report.txt` artifact names the stale table(s).
2. Map table → producer:
   - `market_quotes_daily`, `vix_term_structure` →
     `TradingSupervisorService.MarketDataCollector`.
   - `position_greeks` → `GreeksMonitorWorker`.
   - `account_equity_daily` → nightly snapshot, hour offset OK.
   - `benchmark_series` → `BenchmarkCollector`.
3. Run RUNBOOK.md Playbook 2 (stale data) against the named
   producer.

**Escalation**: same as S1.

---

### S4 — Ingest success rate

**Objective**: 99.5% of `POST /api/v1/ingest` requests from the
supervisor return 2xx over a rolling 30-day window.

**Why 99.5% and not 100%**: we explicitly want an error budget for
(a) transient Cloudflare edge failures that the supervisor retries
successfully on the next poll, (b) occasional malformed payloads
during schema evolution that are caught by the Zod contract layer
(which is doing its job). 0.5% of a typical 100k events/day =
500 events of slack.

**Error budget**: 0.5% of batches = the supervisor writes to the
Outbox and retries. A budget breach means the retry loop itself
is unhealthy, not that individual events fail.

**Measurement**: Cloudflare Analytics dataset `trading_system_metrics`.
Every ingest POST emits exactly one `ingest.event_type` sample with a
`status` tag (`accepted`, `rejected_validation`, or
`rejected_unknown_type`) and a `type` tag (the event type). The
`recordMetric()` helper (`infra/cloudflare/worker/src/lib/metrics.ts`)
sorts tag keys alphabetically before emission, so within the
`ingest.event_type` metric: `blob1 = 'ingest.event_type'` (name),
`blob2 = status value`, `blob3 = type value`.

**Operator query**:

```sql
SELECT
  SUM(CASE WHEN blob2 = 'accepted' THEN 1 ELSE 0 END) AS accepted,
  SUM(CASE WHEN blob2 LIKE 'rejected%' THEN 1 ELSE 0 END) AS rejected,
  1.0 - (SUM(CASE WHEN blob2 LIKE 'rejected%' THEN 1 ELSE 0 END) * 1.0
         / COUNT(*)) AS success_rate
FROM trading_system_metrics
WHERE timestamp > NOW() - INTERVAL '30' DAY
  AND blob1 = 'ingest.event_type'
  AND (blob2 = 'accepted' OR blob2 LIKE 'rejected%');
```

Expected: `success_rate >= 0.995`.

**Alerting thresholds**:

- **Warning**: 1-day success rate < 99.5% → Telegram `warn`.
- **Critical**: 1-hour success rate < 95% → Telegram `critical`
  (contract drift, API key drift, or D1 write failures).
- **Breach**: 30-day success rate < 99.5% → breach issue.

**On-call action on alert**:

1. RUNBOOK.md Playbook 2 (stale data) — same investigation
   surface (ingest + outbox).
2. If rejection rate dominates → check recent schema / fixture
   changes (Phase 7.6 contract tests should catch this before
   merge; if a rejection storm happens anyway, something slipped
   past CI).

**Escalation**: same as S1.

---

## 4. Error budget methodology

**Budget accounting is rolling, not calendar**. We compute against
the last 30 days, not "this month". Two reasons: (a) a budget that
resets on the 1st of the month creates perverse incentives to push
risky changes on the 30th; (b) rolling windows better reflect the
operator's perception of reliability.

**Budget consumption formula (any SLO)**:

```
consumed = (actual_failures / allowed_failures) * 100%
```

Example S1 over 30 days:
- Allowed unavailability: 432 min.
- Actual unavailability: 95 min.
- Consumed: 22%.
- Remaining: 337 min (5h 37m).

**Budget policies**:

| Consumed | Posture                                           |
|----------|---------------------------------------------------|
| 0-50%    | Normal — change velocity unrestricted.            |
| 50-75%   | Caution — no risky deploys Friday afternoon.      |
| 75-100%  | Conservative — deploy only bugfixes + rollbacks.  |
| > 100%   | Freeze — no non-critical deploys until 7-day window drops below 100%, AND a breach retro is opened. |

The freeze is an honor system for a solo-op, but the rule lives
here so a future team-of-two or auditing exercise has a policy to
point at.

---

## 5. Staging SLOs (looser — reference only)

Staging targets are half-strength of production:

| SLO | Staging target                  |
|-----|---------------------------------|
| S1  | 95% availability (vs 99%)       |
| S2  | P95 < 1000ms (vs 500ms)         |
| S3  | < 15 min lag (vs 5 min)         |
| S4  | 98% ingest success (vs 99.5%)   |

Staging does not alert Telegram — issues surface through CI and
the `data-freshness.yml` workflow only. Staging SLO breaches are
informational; we don't freeze deploys for them.

---

## 6. Who updates this file

- **The operator** (Lorenzo): whenever a threshold is tuned based
  on live observation. Every threshold change MUST include a
  one-paragraph rationale comment in the section, dated.
- **Never**: any automated tool. This document is deliberately
  human-curated so every number in it is traceable to a decision,
  not drift.

---

## 7. Related docs

- `docs/ops/RUNBOOK.md` — the procedures an alert points to.
- `docs/ops/OBSERVABILITY.md` — where the signals the SLO
  measures actually come from.
- `docs/ops/DR.md` — what happens when availability budget
  is consumed by a disaster-recovery event.
- `docs/ops/GO_LIVE.md` — the precondition "SLO widgets present
  on dashboard" is measured against the objectives above.
