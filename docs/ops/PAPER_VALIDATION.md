---
title: "14-day Paper Validation"
tags: ["ops", "runbook", "safety", "testing"]
aliases: ["Paper Validation", "Paper Run"]
status: current
audience: ["operator"]
phase: "phase-7.7"
last-reviewed: "2026-04-21"
related:
  - "[[GO_LIVE]]"
  - "[[DAILY_OPS]]"
  - "[[Paper-run day log — YYYY-MM-DD|Paper-run Template]]"
---

# 14-day Paper Validation

> Phase 7.7 deliverable. The procedure Lorenzo follows to validate the
> system end-to-end in paper mode BEFORE running `GO_LIVE.md`. This doc
> does not execute the validation — it tells the operator how to execute
> it, what success looks like, and when to restart the clock.
>
> Last updated: 2026-04-21.

---

## 0. Why 14 days and not 3 or 30

- **14 ≈ 10 trading days** (accounting for weekends). That's enough to
  see two full rollovers of the weekly SPX IC strategy, at least one
  FOMC-adjacent session, and typically one VIX regime shift.
- Any shorter (e.g. 3-day) and you're cargo-culting. The regime would
  not have time to change; the SemaphoreGate and circuit breaker
  wouldn't be exercised.
- Any longer (e.g. 30-day) and operator fatigue compounds — the point
  of the validation is to stress the procedures, not the patience.

Lorenzo's discretion: extend the window if a material system change
happens mid-run. See § 5.

---

## 1. Day 0 — setup

### 1.1 TradingMode = paper confirmed

```powershell
Get-Content "C:\trading-system\logs\supervisor-*.log" -Tail 100 |
  Select-String "TradingMode="
```

Expected: `Starting in TradingMode=paper`. If anything else, STOP and
fix.

### 1.2 Enable exactly ONE strategy

Per the Phase 7 plan: the initial strategy is **SPX Weekly Iron
Condor**. Enable it in the strategy registry:

```bash
curl -X POST -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  https://trading-bot.padosoft.workers.dev/api/strategies/spx-weekly-iron-condor/enable
```

Verify:

```bash
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  https://trading-bot.padosoft.workers.dev/api/strategies/active | jq .
```

Expected: exactly one row, `id: "spx-weekly-iron-condor"`, `enabled: true`.
**Other strategies MUST be disabled.** Running multiple strategies in
parallel during validation means you can't attribute failures.

### 1.3 TWS connected as PAPER account

TWS → Account menu. Confirm the account id begins with `DU` (IBKR
paper accounts). If it starts with `U` (live), STOP and re-login.

### 1.4 Data freshness green

```bash
./scripts/verify-data-freshness.sh
```

Expected: exit 0, all D1 tables fresh. If any stale, debug BEFORE
starting the validation.

### 1.5 Create the paper-run directory

```bash
mkdir -p docs/ops/paper-run
cp docs/ops/paper-run/TEMPLATE.md docs/ops/paper-run/Day-00.md
```

Edit `Day-00.md` with the setup findings. This is the baseline.

### 1.6 Announce

Telegram `trading-system-warn`:

```
Paper validation Day 0 complete. 14-day clock starts tomorrow
at market open. Single strategy: spx-weekly-iron-condor.
```

---

## 2. Day-by-day execution

Every trading day for 14 calendar days (≈ 10 trading days):

### 2.1 Every day

Follow `docs/ops/DAILY_OPS.md` morning + midday + EOD rounds.

At EOD, create `docs/ops/paper-run/YYYY-MM-DD.md` from TEMPLATE.md
and commit it. The commit message is specifically:

```
docs(paper-run): Day N of 14 — <status>
```

Status values: `clean`, `minor-anomaly`, `CRITICAL`.

### 2.2 Weekly on Monday (days 1, 8)

Run the "Weekly" section in DAILY_OPS.md § 5. This is in addition
to the daily rounds.

### 2.3 At least one forced RED-regime test

Sometime during the 14 days (recommended: day 3 or 4, once you trust
the morning round), force the SemaphoreGate to reject orders to
verify the gate actually stops orders. Do NOT rely on waiting for a
natural RED — you need deterministic evidence the gate works.

**Note**: a dedicated `Safety__ForceSemaphoreStatus` env var does
NOT exist at the time of Phase 7.7 validation. If a helper flag is
introduced later (Phase 8+), it should be documented alongside its
implementation and logging behavior. For THIS validation run use
one of the two currently-supported paths below — pick either, and
record which one you used in the day-log.

**Option A — flip the durable `trading_paused` safety flag** (easiest,
exercises the same refusal code path `OrderPlacer` checks before
reaching the semaphore). Audit outcome will be
`rejected_pnl_pause` instead of `rejected_semaphore`, which is still
valid evidence the gate pipeline refuses orders:

```powershell
# On the service host (PowerShell):
sqlite3 "C:\trading-system\data\options-execution.db" `
  "INSERT OR REPLACE INTO safety_flags (key, value) VALUES ('trading_paused', '1');"
# No service restart needed — the flag is read per-order.
```

**Option B — force a RED semaphore by seeding VIX/VIX3M values in
D1** so the IVTS indicator crosses the 1.15 threshold. Staging only —
do NOT do this on production D1:

```bash
# IVTS = VIX / VIX3M. Forcing VIX=25, VIX3M=20 => IVTS=1.25 > 1.15 => RED.
bunx wrangler d1 execute trading-db-staging --remote \
  --command="INSERT OR REPLACE INTO vix_term_structure (date, vix, vix3m) \
             VALUES (date('now'), 25.0, 20.0);"
# Restart OptionsExecutionService so the 60s SemaphoreGate cache invalidates
# on the next tick (or wait ~60s for the TTL to expire).
```

Then for either option, submit one test order:

```powershell
cd C:\trading-system
dotnet run --project src/OptionsExecutionService `
  -- order-test --symbol=SPX --strategy=spx-weekly-iron-condor
```

Expected: the order is refused, logs show `OrderPlacer blocked order`
(Option A) or `SemaphoreGate blocked order` (Option B), and a row
appears in `order_audit_log` with `outcome=rejected_pnl_pause`
(Option A) or `outcome=rejected_semaphore` (Option B).

**Undo immediately** — the whole point of the test is that the block
is temporary:

```powershell
# Option A undo:
sqlite3 "C:\trading-system\data\options-execution.db" `
  "DELETE FROM safety_flags WHERE key='trading_paused';"
```

```bash
# Option B undo — replace with a current realistic VIX pair, or just drop:
bunx wrangler d1 execute trading-db-staging --remote \
  --command="DELETE FROM vix_term_structure WHERE date = date('now');"
```

Document the blocked order + the undo in the day-log. This is
direct evidence the safety-gate pipeline refuses orders when told
to, which the Completion Report will cite.

### 2.4 Day 14

Compile the Completion Report (§ 4).

---

## 3. Success criteria (all required)

The validation PASSES only if ALL of these hold over the 14-day
window:

| # | Criterion                                                      | Measured via                                     |
|---|----------------------------------------------------------------|--------------------------------------------------|
| 1 | Zero CRITICAL alerts                                           | Telegram `trading-system-critical` channel       |
| 2 | Zero data-freshness gaps > 5 min during market hours           | `data-freshness.yml` workflow run history        |
| 3 | At least one RED-regime gate test executed and passed          | Day-log + `order_audit_log` row                  |
| 4 | Audit log shows expected outcome mix                           | see § 3.2                                        |
| 5 | Positions reconciliation (Worker vs TWS) perfect every EOD     | Daily logs — every day's reconciliation = match  |
| 6 | All 14 daily logs exist and are committed to git               | `ls docs/ops/paper-run/`                         |
| 7 | No manual restarts required (except for scheduled item 2.3)    | Day-logs                                         |
| 8 | CI green on main every day                                     | `gh run list --branch=main`                      |
| 9 | DR backup tasks ran every night (14/14)                        | Scheduled task history                           |

### 3.1 Zero-CRITICAL is strict

Warnings are OK. INFO is noise. CRITICAL = the 14-day clock resets.
This is intentional. If you let "only one" critical slip through, the
next round will have "only two". Validation discipline is a
renewable resource only until it's not.

### 3.2 Expected outcome mix in audit log

Over 14 days, for SPX Weekly Iron Condor, expect:

- `placed` rows: 10-30 (roughly 1-3 orders per trading day).
- `filled` rows: a subset of placed (simulated fills in paper mode).
- `rejected_semaphore`: at least 1 (from § 2.3 forced test).
- `rejected_broker`: 0-2 acceptable (paper brokers occasionally
  reject exotic orders).
- `error`: 0 — any `error` rows warrant investigation.

Run:

```bash
from=$(date -d '14 days ago' +%Y-%m-%d)
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  "https://trading-bot.padosoft.workers.dev/api/audit/orders?from=${from}&limit=1000" \
  | jq '[.rows[].outcome] | group_by(.) | map({outcome: .[0], count: length})'
```

Expected something like:

```json
[
  {"outcome": "placed", "count": 18},
  {"outcome": "filled", "count": 18},
  {"outcome": "rejected_semaphore", "count": 1}
]
```

If you see `error` rows or unfamiliar outcome values, investigate
before claiming pass.

---

## 4. Completion Report

### 4.1 When to write it

On day 14, after the EOD round.

### 4.2 Where

`docs/ops/paper-run/00-COMPLETION-REPORT.md`.

### 4.3 Required sections

```markdown
# Paper validation — Completion Report

**Operator**: Lorenzo Padovani
**Paper-run window**: YYYY-MM-DD → YYYY-MM-DD (14 calendar days)
**Strategy**: spx-weekly-iron-condor
**Trading mode throughout**: paper
**Sign-off**: PASSED / FAILED

---

## 1. Day log index

| Day | Date       | Status | Link                                            |
|-----|------------|--------|-------------------------------------------------|
| 1   | YYYY-MM-DD | clean  | [Day-01.md](./YYYY-MM-DD.md)                    |
| ... | ...        | ...    | ...                                             |

## 2. Success criteria

| # | Criterion                                     | Result | Evidence                    |
|---|-----------------------------------------------|--------|-----------------------------|
| 1 | Zero CRITICAL alerts                          | PASS   | Telegram export             |
| 2 | Zero data-freshness gaps > 5 min              | PASS   | `data-freshness.yml` runs   |
| 3 | At least one RED-regime gate test passed      | PASS   | [Day-04.md](./...), audit #N |
| 4 | Audit log outcome mix matches expectation     | PASS   | jq output in § 4            |
| 5 | Positions reconciliation perfect every EOD    | PASS   | 14/14 daily logs            |
| 6 | All 14 daily logs committed                   | PASS   | `git log docs/ops/paper-run/` |
| 7 | No manual restarts except scheduled           | PASS   | Day logs                    |
| 8 | CI green on main every day                    | PASS   | `gh run list`               |
| 9 | DR backups ran 14/14 nights                   | PASS   | Scheduled task history      |

## 3. SLO consumption

- S1 (availability): <x.xx%>, budget consumed: <y%>
- S2 (order latency P95): <nnn ms>, slow orders: <k>
- S3 (data freshness): <n stale minutes total>
- S4 (ingest success): <xx.xx%>

## 4. Audit log summary

<jq output from § 3.2 of PAPER_VALIDATION.md, pasted>

## 5. Notable observations

<paragraphs — include things that surprised the operator, anything
 that'll inform the first-live window, any strategy-specific quirks>

## 6. Follow-up items deferred to Phase 8+

- <bullets>

## 7. Sign-off

Operator: Lorenzo Padovani, YYYY-MM-DD
Status: **PASSED** — proceed to GO_LIVE.md
```

### 4.4 Commit and tag

```bash
git add docs/ops/paper-run/
git commit -m "docs(paper-run): Completion report — PASSED"
git tag paper-validation-complete-YYYY-MM-DD
```

The tag is important — it's the reference point for the go-live
precondition in `GO_LIVE.md` § 1.3.

---

## 5. Failure handling — when to restart the clock

### 5.1 Any CRITICAL alert → +7 days

If a CRITICAL alert fires at any point in the 14-day window:

1. Investigate and fix the root cause.
2. Ship the fix (PR, merge, services updated).
3. Reset the validation clock by starting a fresh Day 1.
4. New Completion Report for the new window.

The old window's daily logs stay in the repo for the record but are
superseded by the new window.

### 5.2 Material system change (new feature, upgraded SDK) → restart

Any merge to main that touches `src/OptionsExecutionService/`,
`src/TradingSupervisorService/Strategies/`, `src/SharedKernel/Domain/`,
or `infra/cloudflare/worker/src/routes/ingest.ts` during the
validation window resets the clock. The validation is proving the
system AS DEPLOYED; a change mid-run invalidates the evidence.

Exception: documentation-only changes, test-only changes, or dashboard
CSS. These don't affect the trading path.

### 5.3 Warn-level issues → continue with a note

Warnings, retries, single data-freshness recoveries that
self-resolved — these are not fatal. Document in the day log and
continue. If warnings accumulate (e.g. 5+ on the same subject in a
week), that's effectively a CRITICAL pattern — treat as § 5.1.

### 5.4 Anything else the operator judges material

Lorenzo's discretion. If something doesn't smell right but doesn't
technically breach a criterion, err toward restarting. Cost of
restart: 14 days. Cost of a bad go-live: real money.

---

## 6. After validation PASSES

1. Tag the repo (see § 4.4).
2. Announce in Telegram:
   ```
   Paper validation PASSED on YYYY-MM-DD. Proceeding to GO_LIVE.md
   review and flip scheduling.
   ```
3. Schedule the go-live for a Tuesday or Wednesday market open
   (avoid Mondays — weekend drift risk; avoid Fridays — no time to
   react before the weekend).
4. Before the go-live day, re-read `docs/ops/GO_LIVE.md` in full.
5. Execute GO_LIVE.md on the scheduled day.

---

## 7. Related docs

- `docs/ops/DAILY_OPS.md` — the daily checklist this validation uses.
- `docs/ops/GO_LIVE.md` — the procedure unlocked by a PASSED validation.
- `docs/ops/SLO.md` — the targets measured during validation.
- `docs/ops/RUNBOOK.md` — the incident response during validation.
- `docs/ops/DR.md` — the backup chain the validation verifies is running.
