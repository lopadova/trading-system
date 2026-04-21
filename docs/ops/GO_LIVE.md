---
title: "Go-live Runbook"
tags: ["ops", "runbook", "deployment", "safety"]
aliases: ["Go Live", "Paper to Live"]
status: current
audience: ["operator"]
phase: "phase-7.7"
last-reviewed: "2026-04-21"
related:
  - "[[RUNBOOK]]"
  - "[[DAILY_OPS]]"
  - "[[DR]]"
  - "[[RELEASE]]"
  - "[[PAPER_VALIDATION]]"
  - "[[SECRETS]]"
---

# Go-live Runbook

> Phase 7.7 deliverable. The exact procedure to flip the trading system
> from paper to live, with verifiable preconditions, a rollback that
> takes under 60 seconds, and a 24h / 72h / 2-week post-live schedule.
>
> **This is the most important document in Phase 7.** Read the whole
> thing BEFORE starting the flip. Do not improvise. If any step is
> unclear, stop and clarify.
>
> Last updated: 2026-04-21.

---

## 0. The 60-second summary

Going live means changing one enum value in an encrypted config file.
Mechanically it's trivial. Operationally it's the point at which a bug
costs money instead of a log line. This document exists so the
mechanical part is boring and the operational part is deliberate.

The rollback is the inverse of the flip: another edit, another restart.
It MUST complete in under 60 seconds. If you can't meet that bar, do
NOT go live — fix the rollback first.

---

## 1. Preconditions (all required)

Do NOT proceed until every box is green. Each item has a verifiable
check — no "I'm pretty sure".

### 1.1 All Phase 7 sub-phases merged

```bash
git log --oneline --grep="Phase 7" origin/main | head -20
```

Must show entries for Phase 7.1 → Phase 7.7, all on `main`. If any
sub-phase is unmerged or reverted, STOP.

### 1.2 CI green on main (all four workflows)

```bash
gh run list --branch=main --limit 8 --json status,conclusion,name \
  --jq '.[] | "\(.status)/\(.conclusion) \(.name)"'
```

Must show `completed/success` for:

- .NET Build & Test
- CI/CD (main pipeline)
- Cloudflare Worker & Dashboard Deploy (test jobs)
- Data Freshness Check

If any is red → fix the regression and re-run. Never go live against
a known-broken build.

### 1.3 14-day paper validation completed and signed off

```bash
ls docs/ops/paper-run/
# Must show: 14 daily log files + 00-COMPLETION-REPORT.md
```

The completion report lives at
`docs/ops/paper-run/00-COMPLETION-REPORT.md` and must state
`Sign-off: PASSED` with the operator's signature and date. If the
14-day clock was reset during validation, 14 consecutive clean days
from the reset.

### 1.4 DR drill executed within last 90 days

Check `docs/ops/dr-drills/` — most recent drill file must be dated
within 90 days of today, with `Row-count diff: zero`. If older than
90 days or the last drill failed, execute a fresh drill NOW
(`docs/ops/DR.md` § 6) before proceeding.

### 1.5 Backup schedule active

```powershell
Get-ScheduledTaskInfo -TaskName "TradingSystem-DailyD1Backup" |
  Select-Object LastRunTime, NextRunTime, LastTaskResult
Get-ScheduledTaskInfo -TaskName "TradingSystem-NightlySqliteBackup" |
  Select-Object LastRunTime, NextRunTime, LastTaskResult
```

Both tasks must show `LastRunTime` within 36h and `LastTaskResult: 0`.

### 1.6 Secrets current (no stale rotations)

Password manager entries for every secret in `docs/ops/SECRETS.md` § 1
have been reviewed in the last 90 days. If the last rotation is > 90
days old, rotate first. Rotating ON go-live day is forbidden — too
many variables changing at once.

### 1.7 On-call acknowledged

Solo-op project. "Acknowledged" means:

- Operator (Lorenzo) is physically at the workstation for the
  entirety of the first live market session.
- Phone with Telegram is on and unmuted.
- Nothing else scheduled that could pull attention (no meetings, no
  errands) for the 4h window after the flip.

### 1.8 Dashboard SLO widgets present

Open the dashboard:

```
https://trading-dashboard.pages.dev/
```

Verify the following widgets render real data (not "fallback-mock"):

- Overview → Performance summary.
- Risk semaphore → 4 sub-indicator tiles.
- Positions table → at least empty-state "no open positions" rendered,
  not a spinner or error.
- Audit log recent entries.

If any widget is broken, you will be blind during the first live
session. Fix it first.

### 1.9 Account has live capital and is subscribed to real-time data

In TWS: Account → verify:
- NOT a paper account (account numbers do NOT start with `DU`).
- Live market data subscriptions active for every symbol the enabled
  strategy will touch (SPX, VIX, etc.).
- Margin requirements and buying power sufficient for the strategy's
  max position size.

---

## 2. Go-live procedure

Phase 7.7 also introduced the tag-based deploy model
(`docs/ops/RELEASE.md`). A typical go-live cut is two steps:

1. **Ship the code**: `git tag -a vX.Y.Z -m "..."` + `git push origin vX.Y.Z`
   on `main`, approve the `production` environment gate in Actions, wait
   for Cloudflare Worker + Pages to deploy. This affects the
   dashboard + Worker only — the service host does NOT auto-update.
2. **Flip the services**: the sub-sections below (2.1–2.9). This is the
   DPAPI-wrapped `TradingMode=paper → live` change on the Windows
   service host.

Do these in order. Cloudflare deploys FIRST so the dashboard + Worker
are already on the release code when the services start calling live
endpoints. If step 1 fails, STOP — do not flip the services against
stale backend code.

### 2.1 Pre-flip announcement

Post in the `trading-system-critical` Telegram channel:

```
GO-LIVE FLIP starting <HH:MM local>. Paper → Live.
Operator at console. Abort signal: any CRITICAL alert in the next 4h.
```

Timestamp your own note for the post-live retrospective.

### 2.2 Stop the services

```powershell
Stop-Service OptionsExecutionService
Stop-Service TradingSupervisorService
```

Verify stopped:

```powershell
Get-Service TradingSupervisorService, OptionsExecutionService |
  Select-Object Name, Status
# Expected: both Status = Stopped
```

**Why stop?** The config change below must be read cleanly at startup.
Hot-reload is not wired for `TradingMode` — deliberately, because
mid-session flips are dangerous.

### 2.3 Encrypt the new TradingMode value

The `TradingMode` key is NOT currently DPAPI-wrapped in most setups —
it's a plain string. For maximum paranoia we wrap it anyway so the file
has no cleartext "live" in it. If your `appsettings.Production.json`
already has a plain-string `TradingMode`, this step produces a wrapped
blob you can paste in its place.

```powershell
# On the service host. EncryptConfigValue uses LocalMachine DPAPI scope.
cd C:\trading-system
# IMPORTANT: do NOT copy-paste `echo -n "live"` from a bash snippet —
# `echo` IS a PowerShell alias for Write-Output but `-n` is NOT a valid
# PowerShell option, so PowerShell would send the literal "-n live"
# bytes to EncryptConfigValue and wrap the wrong string.
#
# In PowerShell, the native pipe IS safe here: PowerShell appends a
# trailing line terminator, and EncryptConfigValue explicitly trims a
# single trailing \r\n / \n in its stdin reader (see
# src/Tools/EncryptConfigValue/Program.cs). Net effect: the 4 bytes
# "live" get wrapped, no trailing newline inside the blob.
'live' | dotnet run `
  --project src/Tools/EncryptConfigValue `
  --configuration Release

# If you prefer a belt-and-braces alternative (no pipe, interactive
# entry), run the tool without input — it will prompt with a hidden
# field and accept the secret from the terminal:
#   dotnet run --project src/Tools/EncryptConfigValue --configuration Release
# Then type `live` and press Enter.

# Output:
# DPAPI:AQAAANCMnd8BFdERjHoAwE/Cl+sBAAAA...
# Copy this entire line including the DPAPI: prefix.
```

**Also prepare the rollback value NOW, before touching anything:**

```powershell
# Same PowerShell idiom — native pipe + trust the tool's newline trim.
# Never write `echo -n "paper"` here; see the warning above.
'paper' | dotnet run `
  --project src/Tools/EncryptConfigValue `
  --configuration Release

# Save this output in the chat you opened for go-live (your text
# editor, a password manager note, whatever). You must be able to
# paste it in < 10 seconds during a rollback.
```

### 2.4 Edit appsettings.Production.json

For BOTH services — `TradingSupervisorService` and
`OptionsExecutionService` — open the `appsettings.Production.json`
in the service binary directory:

```
C:\Program Files\TradingSupervisorService\appsettings.Production.json
C:\Program Files\OptionsExecutionService\appsettings.Production.json
```

Change:

```jsonc
{
  "TradingMode": "paper"
}
```

to:

```jsonc
{
  "TradingMode": "DPAPI:AQAAANCMnd8BFdERjHoAwE/..."
}
```

(paste the exact DPAPI blob from § 2.3).

**Double-check**: only this one key changed. Use `git diff` if the
files are in git, or a side-by-side diff tool. Any unintended change
to other keys (e.g. a secret blob) is a silent rollback trap.

### 2.5 Start the supervisor FIRST

```powershell
Start-Service TradingSupervisorService
```

Watch the log for the mode banner:

```powershell
Get-Content "C:\trading-system\logs\supervisor-*.log" -Tail 50 -Wait
```

Expected lines (within 10 seconds):

```
[INF] Starting in TradingMode=live
[INF] IBKR connected (accountId=Uxxxxxxx, type=LIVE)
[INF] Heartbeat 1 emitted
```

**If the mode line says `paper`**: the encryption didn't take or the
file wasn't edited correctly. Stop now. Go to § 4 (rollback) and
reconfigure.

**If IBKR connection fails on LIVE but worked on paper**: TWS is
still logged in as a paper account. Close TWS and re-login with
live credentials. Do not start OptionsExecutionService until the
supervisor shows `type=LIVE`.

### 2.6 Start OptionsExecutionService

```powershell
Start-Service OptionsExecutionService
```

Tail the log:

```powershell
Get-Content "C:\trading-system\logs\options-*.log" -Tail 50 -Wait
```

Expected lines:

```
[INF] Starting in TradingMode=live
[INF] OrderPlacer wired to LIVE broker
[INF] SemaphoreGate enabled (composite from Worker)
[INF] CircuitBreaker armed: threshold=3, window=15min
```

### 2.7 Post-start verification (~5 minutes)

Run § 2 (Morning round) from `docs/ops/DAILY_OPS.md` IN FULL.
Every check must pass. Specifically:

- Semaphore returns not-red.
- `api/audit/orders` is reachable (even if empty — the endpoint exists).
- Outbox `pending=0` and draining normally.
- Both services show TradingMode=live in their logs.

### 2.8 First real order readiness

Before the first market event that would trigger a live order:

- Mentally rehearse the abort path (§ 4) one more time.
- Confirm you can see the semaphore widget AND the Telegram
  `trading-system-critical` channel at the same time without
  switching screens.
- `Safety:KillSwitchAllServices` env-var is NOT set (the service
  would refuse all orders if it were; harmless to verify).

### 2.9 Post-flip announcement

Telegram:

```
GO-LIVE COMPLETE <HH:MM local>. TradingMode=live, services healthy,
semaphore green. Operator remains at console.
```

---

## 3. First-day monitoring (mandatory, market hours)

You stay at the console for the entirety of the first live session.
No exceptions. The goals are (a) catch a genuine bug fast, (b) build
ground truth about what "live looks like" for this system.

**Watch surfaces (keep on screen)**:

1. Dashboard → Risk semaphore.
2. Dashboard → Positions.
3. Telegram `trading-system-critical` + `trading-system-warn`.
4. `Get-Content logs/supervisor-*.log -Tail 50 -Wait` in a terminal.
5. `Get-Content logs/options-*.log -Tail 50 -Wait` in a second terminal.

**When to abort** (go to § 4 rollback immediately):

- Any Critical alert fires in the first 4h.
- Any order is placed that you did NOT expect, regardless of outcome.
- Any positions-reconciliation drift between Worker and TWS.
- Semaphore flips to red with no concurrent market reason.
- A service crash (`Stopped` state) within the first 24h.

**When to stay calm and continue**:

- Orange semaphore — the system IS observing real conditions; if
  you told it "don't trade in orange", it won't.
- A rejected order because of a safety gate — the gate is doing its
  job. Document the rejection reason in the day-log.
- Sentry email notifying of a warn-level exception the system
  already logged. Follow up EOD, not mid-session.

---

## 4. Rollback (target: < 60 seconds)

**When**: any abort signal from § 3, OR operator judgment that the
live run is unsafe to continue.

**Commands**:

```powershell
# 1. Stop services immediately (no grace period).
Stop-Service OptionsExecutionService -Force
Stop-Service TradingSupervisorService -Force
```

Elapsed: ~5 seconds.

```powershell
# 2. Restore the TradingMode=paper DPAPI blob you saved in § 2.3.
#    Open both appsettings.Production.json files and paste the
#    paper-mode blob back in place of the live-mode blob.
notepad "C:\Program Files\TradingSupervisorService\appsettings.Production.json"
notepad "C:\Program Files\OptionsExecutionService\appsettings.Production.json"
```

Elapsed: ~30 seconds (two files, one paste each).

```powershell
# 3. Restart.
Start-Service TradingSupervisorService
Start-Service OptionsExecutionService
```

Elapsed: ~10 seconds.

```powershell
# 4. Verify mode reverted.
Get-Content "C:\trading-system\logs\supervisor-*.log" -Tail 10 |
  Select-String "TradingMode="
# Expected: "Starting in TradingMode=paper"
```

Total: ~60 seconds from abort decision to paper-mode confirmation.

**Post-rollback**:

1. Announce in Telegram:
   ```
   GO-LIVE ROLLED BACK <HH:MM>. System on paper. Investigating.
   ```
2. Open a priority issue in the repo with `phase7.7-abort` label.
   Include: abort reason, wall-clock times, last 200 log lines from
   both services.
3. Do NOT attempt go-live again until root cause is understood AND
   fixed AND verified in a fresh ≥ 3-day paper session.

### Rollback if TWS itself is the problem

If the issue is that TWS was logged into live when you wanted paper
(or vice versa), the config flip won't help — you need to re-login
TWS to the correct account.

1. Stop services (step 1 above).
2. Close TWS. Re-login to the intended account (`DU*` for paper,
   `U*` for live).
3. Restart services.

---

## 5. Post-live schedule

### 5.1 24-hour retrospective

**When**: ~18h after go-live (next market-close + overnight).

**What**: 30-min retro, written into `docs/ops/post-live/24h.md`.

Questions to answer:
- Any unexpected behavior, alerts, or operator interventions?
- Did the SLO budgets hold (see `SLO.md` § 4)?
- Did Positions reconciliation match TWS every check?
- Any BAD-looks-like items from DAILY_OPS.md triggered?
- One thing to improve before tomorrow.

### 5.2 72-hour retrospective

**When**: Friday EOD of the go-live week.

**What**: `docs/ops/post-live/72h.md` — same template as 24h, plus:

- Weekly SLO consumption report (even if week isn't over, 3-day
  snapshot).
- Audit of every order placed — each must be explainable.
- Telegram alert volume (should be ≤ 5 for the first 72h if
  things are healthy).

### 5.3 Two-week first-live report

**When**: 14 days after go-live.

**What**: `docs/ops/post-live/2-weeks.md`.

Structure:

```markdown
# Two-week first-live report — go-live YYYY-MM-DD

## Summary
<one paragraph: did the system meet operator expectations?>

## SLO consumption (14-day)
- S1 availability: <x.xx%> — budget consumed: <y%>
- S2 order P95: <n ms> — slow orders: <k>
- S3 data freshness: <stale minutes total>
- S4 ingest success: <x.xx%>

## Orders placed
- Total: <n>
- Filled: <n>
- Rejected by safety gates: <n> (breakdown by outcome)
- Rejected by broker: <n>

## Incidents
- <list with links to issues / retros>

## Operator notes
<paragraphs>

## Actions for Phase 8+
- <bullet list>
```

This report goes in the repo so every future operator (or auditor)
can see exactly how the first two weeks of live played out.

---

## 6. After the first two weeks

Operations transition from "first-live vigilance" to "steady state":

- Drop the midday round from DAILY_OPS (but keep morning + EOD).
- Still stay at console for the first 30 minutes after market open
  for another month.
- Review `order_audit_log` weekly (Monday) instead of daily.
- Monthly SLO reports become the primary operational surface.

---

## 7. Related docs

- `docs/ops/RELEASE.md` — the tag-based deploy procedure (§ 2 step 1
  above). Run THIS before flipping services.
- `docs/ops/PAPER_VALIDATION.md` — the 14-day validation this
  procedure requires as a precondition.
- `docs/ops/DAILY_OPS.md` — the checklist that replaces this
  runbook once you're steady-state.
- `docs/ops/DR.md` — backup / restore procedures the precondition
  block verifies.
- `docs/ops/RUNBOOK.md` — Playbook 9 covers first-24h live
  incidents and routes back to § 4 (rollback) here.
- `docs/ops/SECRETS.md` — DPAPI wrapping details referenced in § 2.3;
  also the GitHub Actions secrets section needed by the tag deploy.
- `docs/ops/SLO.md` — budget accounting for the post-live reports.
