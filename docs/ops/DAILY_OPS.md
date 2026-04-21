---
title: "Daily Operations Checklist"
tags: ["ops", "runbook"]
aliases: ["DAILY_OPS", "Daily Ops"]
status: current
audience: ["operator"]
phase: "phase-7"
last-reviewed: "2026-04-21"
related:
  - "[[RUNBOOK]]"
  - "[[GO_LIVE]]"
  - "[[OBSERVABILITY]]"
  - "[[SLO]]"
  - "[[PAPER_VALIDATION]]"
---

# Daily Operations Checklist

> Phase 7.7 deliverable. Operator's morning and end-of-day rounds during
> the 14-day paper validation AND once the system is live. Every item
> has: the exact command, the expected output, what a BAD result looks
> like, where to escalate.
>
> Print this page. Tape it next to the trading workstation. You will
> not remember every step the first week — that's fine, follow the list.
>
> Last updated: 2026-04-21.

---

## 1. When to run each round

| Phase              | Morning | Midday | EOD | Weekend |
|--------------------|---------|--------|-----|---------|
| Paper validation   | Yes     | Yes    | Yes | Spot    |
| Live (first month) | Yes     | Yes    | Yes | Spot    |
| Live (steady)      | Yes     | No     | Yes | No      |

- **Morning**: 08:45 local (15 min before NY open). Gives you time to
  react before orders can fire.
- **Midday**: 12:30 local. Quick sanity check.
- **EOD**: 17:30 local (90 min after NY close). All positions settled,
  audit data complete.
- **Weekend**: just verify nothing is on fire. Full round on Monday.

---

## 2. Morning round (15-20 minutes)

### 2.1 Semaphore status

**Why**: confirm the composite risk indicator is green/orange (not red)
before trading starts, and that the Worker can actually serve this
endpoint.

**Command**:

```bash
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  https://trading-bot.padosoft.workers.dev/api/risk/semaphore | jq .
```

**Expected output**:

```json
{
  "status": "green",
  "ivts": { "value": 0.92, "status": "green" },
  "vix_level": { "percentile": 42, "status": "green" },
  "account_drawdown": { "value": -0.4, "status": "green" },
  "market_regime": { "value": "normal", "status": "green" }
}
```

All four sub-indicators should be `green` or `orange`. `status: "red"`
on any → market is telling you to stop.

**BAD looks like**:

- HTTP 4xx/5xx → Worker unreachable. Go to RUNBOOK.md Playbook 1.
- `X-Data-Source: fallback-mock` header present → D1 has no real data
  (collector is broken or hasn't run yet today). Go to Playbook 2.
- All four indicators `orange` simultaneously → unusual; verify VIX
  and IVTS aren't misread before trusting.
- `"status": "red"` with no concurrent market news → data quality
  issue. Investigate before overriding.

**Action on red**: DO NOT trade today. If the signal is real, the
SemaphoreGate will refuse orders anyway. If the signal is wrong,
audit the sub-indicator(s) first — don't reach for the override.

### 2.2 Overnight alerts triaged

**Why**: any Critical alert from overnight must be acknowledged
before the market opens.

**Where**: Telegram `trading-system-critical` channel.

**Expected**: empty or all messages ACK'd (reply with thumbs-up
reaction).

**BAD looks like**:

- Unread CRITICAL alert from 03:14 AM — investigate root cause in
  the logs before trading. If the underlying issue isn't resolved,
  pause trading until it is:
  ```powershell
  [Environment]::SetEnvironmentVariable(
    "Safety__KillSwitchAllServices", "true",
    "Machine")
  Restart-Service OptionsExecutionService
  ```

**Action**: resolve → document → clear.

### 2.3 Data freshness workflow green

**Why**: catch stale market data before it silently corrupts
the trading signal.

**Command**: check GitHub Actions:

```bash
gh run list --workflow=data-freshness.yml --limit 3
# Or open: https://github.com/lopadova/trading-system/actions/workflows/data-freshness.yml
```

**Expected**: last 3 runs green. Runs every 15 min during market
hours, so "last 3" is roughly the last hour.

**BAD looks like**:

- Any run red → one of the D1 tables is stale (see SLO S3). Go to
  RUNBOOK.md Playbook 2.
- No recent runs at all → workflow disabled or GitHub Actions
  outage. Check https://www.githubstatus.com.

### 2.4 .NET CI green on main

**Why**: if last night's CI broke, don't assume the currently-deployed
services are the intended build.

**Command**:

```bash
gh run list --workflow=dotnet-build-test.yml --branch=main --limit 3
```

**Expected**: last run on `main` is green.

**BAD looks like**:

- Red run on main → a merge introduced a regression. Assess impact.
  If the regression is in the services currently running on the
  host, you may need to rollback to the previous build (see
  `docs/DEPLOYMENT_GUIDE.md` § Rollback).

### 2.5 Service status on the host

**Why**: the Windows Services could be stopped after an OS patch
reboot or a manual intervention you forgot about.

**Command** (on the service host):

```powershell
Get-Service TradingSupervisorService, OptionsExecutionService |
  Select-Object Name, Status, StartType
```

**Expected**:

```
Name                          Status  StartType
----                          ------  ---------
TradingSupervisorService      Running Automatic
OptionsExecutionService       Running Automatic
```

**BAD looks like**:

- Either service `Stopped` → start it:
  ```powershell
  Start-Service TradingSupervisorService
  Start-Service OptionsExecutionService
  ```
  Then tail the log for "Service started" and verify no exceptions.
- `StartType: Manual` → someone (you?) disabled autostart. Re-enable:
  ```powershell
  Set-Service TradingSupervisorService -StartupType Automatic
  ```

### 2.6 TradingMode assertion

**Why**: single most dangerous slippage is accidentally running
live when you meant paper (or vice versa, but usually the first).

**Command** (on the service host):

```powershell
# Read the (decrypted) value from the running service via the log.
# The service emits "Starting in TradingMode=<mode>" on every boot.
Select-String -Path "C:\trading-system\logs\supervisor-*.log" `
              -Pattern "TradingMode=" -SimpleMatch |
  Select-Object -Last 3
```

**Expected** (during paper validation):

```
supervisor-2026-04-21.log:1:TradingMode=paper
```

**Expected** (after go-live):

```
supervisor-2026-04-21.log:1:TradingMode=live
```

**BAD looks like**:

- Mismatch between expected and actual → STOP. Do not trade. Rebuild
  the config: see `docs/ops/GO_LIVE.md` § Rollback (if going live
  was accidental) or re-run go-live (if the flip didn't take).

---

## 3. Midday round (5 minutes)

Quick checks — just confirm nothing broke since morning.

### 3.1 Semaphore still not red

Repeat § 2.1. Takes 10 seconds.

### 3.2 Open positions match expectation

**Why**: a strategy might have fired unexpectedly. Know what's on
the book.

**Command**:

```bash
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  https://trading-bot.padosoft.workers.dev/api/positions/active | jq .
```

**Expected**: each open position matches something you (or a strategy)
explicitly opened. Delta/Greeks within expected ranges.

**BAD looks like**:

- A position you don't recognise → check `order_audit_log` to find
  the origin:
  ```bash
  curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
    "https://trading-bot.padosoft.workers.dev/api/audit/orders?limit=50" \
    | jq '.rows[] | select(.contract_symbol=="<symbol>")'
  ```
- A Greeks reading that's way off from morning → suspect stale
  data; check `position_greeks` freshness.

### 3.3 Outbox draining

**Why**: if the outbox is backlogged, the dashboard is going stale
and you won't notice from the Worker side alone.

**Command** (on the service host):

```powershell
sqlite3 "C:\trading-system\data\supervisor.db" `
  "SELECT status, COUNT(*) FROM sync_outbox GROUP BY status;"
```

**Expected**:

```
sent|15342
pending|0
failed|0
```

or with a tiny amount of `pending` that shrinks on next check.

**BAD looks like**:

- `pending` count climbing → Worker ingest is failing. Go to
  Playbook 2.
- Any `failed` → individual event rejected; inspect:
  ```powershell
  sqlite3 "C:\trading-system\data\supervisor.db" `
    "SELECT event_type, last_error FROM sync_outbox WHERE status='failed' LIMIT 10;"
  ```

---

## 4. End-of-day round (15 minutes)

Settles the trading day into the audit record.

### 4.1 Strategy journal review

**Why**: every order placed today should be understandable. If you
can't explain why the system placed an order, that's a red flag.

**Command**:

```bash
today=$(date +%Y-%m-%d)
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  "https://trading-bot.padosoft.workers.dev/api/audit/orders?from=${today}" | jq .
```

**Expected** (paper run, one strategy enabled — see PAPER_VALIDATION.md):

- 0-5 order rows per trading day for SPX Weekly Iron Condor.
- Each with `outcome: "placed"` or `"filled"`.
- `outcome: "rejected_*"` rows also expected as safety gates activate
  — verify the rejection reason matches the market condition.

**BAD looks like**:

- 50+ order rows → something is looping. Check the strategy code
  and the recent supervisor log for "EnqueueAsync" repetition.
- Many `outcome: "error"` → broker-side issues; see RUNBOOK.md
  Playbook 5 (CPU limit) AND the circuit breaker section.
- Orders placed at unexpected times (e.g. 03:47) → strategy has a
  timing bug. Do NOT dismiss as "IBKR did something weird".

### 4.2 Positions reconciliation

**Why**: the Worker's view of open positions MUST match IBKR's
actual portfolio. Drift here = orders placed against a stale belief
of what you own.

**Command** (two sources, manual diff):

```bash
# Source A — our Worker's view.
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  https://trading-bot.padosoft.workers.dev/api/positions/active | \
  jq -r '.rows[] | "\(.contract_symbol) \(.position) @ \(.avg_price)"' | sort
```

**Source B** — open TWS, Account → Portfolio tab. Manually compare
line by line.

**Expected**: identical set of (symbol, quantity, avg_price)
triples.

**BAD looks like**:

- Worker shows a position that TWS doesn't → our state is stale or
  a close-order wasn't captured. Do not trade tomorrow until
  reconciled. Run:
  ```bash
  # Force a full positions re-sync from IBKR into D1
  curl -X POST -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
    https://trading-bot.padosoft.workers.dev/api/positions/resync
  ```
  (Endpoint is to be added in Phase 7.7.A or later — until then,
  restart the supervisor and let it repopulate on reconnect.)
- TWS shows a position that Worker doesn't → supervisor wasn't
  running when the fill happened. Manually reconcile:
  restart the supervisor, wait 60s, re-check.

### 4.3 Daily P&L sanity

**Command**:

```bash
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  https://trading-bot.padosoft.workers.dev/api/performance/today | jq .
```

**Expected**: `pnlPct` in a plausible range for the strategy
(SPX Weekly IC: typically ±1-2% over a week). `accountValue` matches
TWS within rounding.

**BAD looks like**:

- `pnlPct` > 50% in absolute value → almost certainly a data bug.
  Check the `account_equity_daily` row for today; if it's wrong,
  delete it and let the nightly snapshot re-populate.

### 4.4 Paper-run daily log entry (paper phase only)

**Why**: the 14-day validation requires a daily log so the
Completion Report has evidence.

**Action**: create or update `docs/ops/paper-run/YYYY-MM-DD.md` from
the template in `docs/ops/paper-run/TEMPLATE.md`. One file per day,
committed to git.

Minimal checklist to fill:

- Morning checks: all green / anomalies
- Orders today (count + brief description)
- Alerts received (any)
- SLO impact (any budget consumed)
- Action items for tomorrow

---

## 5. Weekly (Monday morning — paper validation + first month live)

On top of the daily round:

- [ ] Review last 7 days of `order_audit_log` for patterns (same
  `strategy_id` rejecting repeatedly? Latency trending up?).
- [ ] Run `./scripts/verify-data-freshness.sh` locally to sanity-check
  the CI workflow's assumptions against your own expectations.
- [ ] Confirm the backup scheduled tasks (§ 3.1 + § 3.2 in `DR.md`)
  ran each of the last 7 nights:
  ```powershell
  Get-ScheduledTaskInfo -TaskName "TradingSystem-DailyD1Backup"
  Get-ScheduledTaskInfo -TaskName "TradingSystem-NightlySqliteBackup"
  ```
- [ ] Review Sentry issues inbox — anything new and uninvestigated?

---

## 6. Monthly

On top of weekly:

- [ ] Compute rolling 30-day SLO consumption per § 4 of `SLO.md` and
  record the numbers in `docs/ops/slo-reports/YYYY-MM.md`.
- [ ] If the quarterly DR drill is due this month, schedule it
  (`DR.md` § 6).
- [ ] If the quarterly secret rotation is due, instantiate the
  rotation issue template and work through it (`SECRETS.md` § 3).

---

## 7. When to escalate mid-round

Stop the round and go to RUNBOOK.md immediately if:

- Any service is crashing (repeated `Stopped` in § 2.5 after restart).
- Semaphore stuck red for > 30 min with no market reason.
- Outbox `pending` count climbing continuously for > 30 min.
- Any Critical alert fires during a round.
- Positions reconciliation shows drift you cannot explain.

Do NOT continue to subsequent checklist items when you're in an
incident — an unplanned CRITICAL is the priority.

---

## 8. Related docs

- `docs/ops/RUNBOOK.md` — the incident playbooks referenced above.
- `docs/ops/SLO.md` — the thresholds that decide "stale", "slow",
  "broken".
- `docs/ops/PAPER_VALIDATION.md` — the 14-day procedure this
  checklist supports during paper.
- `docs/ops/GO_LIVE.md` — the post-validation flip to live; this
  checklist applies to live too.
- `docs/ops/DR.md` — backup + recovery; referenced for weekly +
  monthly items.
