# Runbook — Common Failure Playbooks

> On-call reference for the trading system. Open this on the second screen
> when something breaks. Every playbook is ~3 minutes to diagnose.
> Last updated: 2026-04-21.

---

## Quick links

- Cloudflare dashboard: https://dash.cloudflare.com → Workers & Pages → `ts`
- Sentry: https://sentry.io/organizations/padosoft/issues
- IBKR TWS: local machine, check `logs/supervisor-*.log`
- D1 console: Cloudflare dashboard → D1 → `trading-db`

---

## Playbook 1 — Worker returns 502 / 500

### Symptoms
- Dashboard widgets show "Something went wrong" or stale data.
- `curl -H "X-Api-Key:$K" https://trading-bot.padosoft.workers.dev/api/performance/summary`
  returns non-2xx.

### Diagnose

1. **Check Worker health endpoint** (no auth):
   ```bash
   curl https://trading-bot.padosoft.workers.dev/api/health
   ```
   If this fails: it's a Cloudflare outage or the Worker itself is broken.

2. **Check the Cloudflare status page**: https://www.cloudflarestatus.com
   - If red/yellow → wait it out, nothing actionable on our side.

3. **Tail Worker logs** (CF dashboard → Workers → ts → Logs → Live tail).
   Look for recent exceptions.

4. **Check D1 status**:
   ```bash
   bunx wrangler d1 execute trading-db --command="SELECT 1"
   ```
   If D1 is down → same page: wait.

### Mitigate

- **If D1 is at quota** (see Playbook 4) → follow that playbook.
- **If the Worker itself is bad** (recent deploy broke something):
  ```bash
  # Rollback to the previous deployment
  bunx wrangler deployments list
  bunx wrangler rollback --deployment-id <previous-id>
  ```
- **Aggregate routes fall back to mock** automatically via the
  `X-Data-Source: fallback-mock` header. The dashboard keeps rendering
  placeholder numbers; not a real outage as long as this header is present.

### After

Create Sentry issue → link to Worker deployment id → open PR with fix if
code was the cause.

---

## Playbook 2 — Dashboard shows stale data

### Symptoms
- Numbers don't change on refresh.
- Response header `X-Data-Source: fallback-mock` on aggregate endpoints.

### Diagnose

1. **Is the supervisor running on the dev/prod machine?**
   ```powershell
   Get-Service -Name "TradingSupervisorService"
   ```
   Expected: `Status: Running`. If stopped → start it.

2. **Is the Outbox draining?** Query local SQLite:
   ```sql
   SELECT COUNT(*), MIN(created_at), MAX(created_at)
   FROM sync_outbox WHERE status='pending';
   ```
   Healthy: count ≈ 0, newest row within 60s. Bad: count climbing → Worker
   cannot accept events.

3. **Check Worker ingest logs** (Cloudflare → Live tail, filter on "INGEST").
   Are events arriving? 400s → payload validation drift. 401 → API key
   mismatch between .NET and Worker. 5xx → D1 write failing.

### Mitigate

- **Ingest 401s** → rotate API key: see `docs/ops/SECRETS.md`
  (to be written in Phase 7.5). Until then: `bunx wrangler secret put API_KEY`,
  update `appsettings.Local.json` on the .NET side, restart services.
- **Ingest 400s** → check recent `service_logs` for Zod validation issues:
  ```sql
  SELECT ts, message, properties
  FROM service_logs
  WHERE message LIKE '%validation%'
    AND ts >= datetime('now','-1 hour');
  ```
- **Outbox not draining** → restart supervisor (`Restart-Service
  TradingSupervisorService`), which re-processes pending rows.

---

## Playbook 3 — IBKR disconnected

### Symptoms
- Dashboard semaphore widget frozen.
- Supervisor logs: `IBKR disconnected` or connection refused.
- Telegram alert: "Supervisor heartbeat stale > 60s".

### Diagnose

1. **Is TWS / IB Gateway running?** Open it on the dev machine.
   - If not → launch it manually. Autologin should bring the session up.
2. **Is the market open?** (NY time 09:30-16:00 ET on weekdays.)
   - After-hours disconnects are normal; the collector stays idle until
     next session.
3. **Check supervisor log** for the last "IBKR connected" line:
   ```
   Get-Content logs/supervisor-*.log -Tail 200 | Select-String "IBKR"
   ```

### Mitigate

- **Restart TWS** → session rebuild usually fixes auth drift.
- **Restart supervisor** → forces the `IbkrClient` to reconnect:
  ```powershell
  Restart-Service TradingSupervisorService
  ```
- **If reconnect loops** (supervisor logs show reconnect every 30s but
  nothing stable):
  - Check IBKR maintenance window: https://www.interactivebrokers.com/en/software/tws/systemstatus.php
  - If not scheduled → open a TWS session manually, check for 2FA prompt
    that may have stalled the API socket.

### After

Once stable, `market_quotes_daily` + `vix_term_structure` should catch up
within 5 minutes. Verify:

```sql
SELECT MAX(ts) FROM service_logs WHERE service='supervisor';
SELECT MAX(snapshot_ts) FROM position_greeks;
```

---

## Playbook 4 — D1 quota hit

### Symptoms
- Worker logs show `D1_ERROR: too many writes` or similar.
- `d1.error` metric spiking in Cloudflare Analytics.
- Some aggregate routes returning 500 / fallback-mock.

### Diagnose

1. **Check D1 usage**: Cloudflare dashboard → D1 → trading-db → Usage.
   Free tier: 5M reads/day, 100k writes/day, 5GB storage.
2. **Find the table eating space**:
   ```sql
   SELECT name, SUM(pgsize) AS bytes
   FROM dbstat GROUP BY name ORDER BY bytes DESC;
   ```
   Likely suspects (2026-04): `service_logs`, `web_vitals`, `market_quotes_daily`.

### Mitigate — immediate

Aggressive trim + archive to R2:

```sql
-- service_logs: keep last 7 days (from 30)
DELETE FROM service_logs WHERE ts < date('now','-7 days');

-- web_vitals: keep last 14 days
DELETE FROM web_vitals WHERE timestamp < date('now','-14 days');

-- position_greeks: roll up to daily snapshots, delete intra-day rows
DELETE FROM position_greeks
WHERE snapshot_ts NOT IN (
  SELECT MAX(snapshot_ts)
  FROM position_greeks
  GROUP BY position_id, date(snapshot_ts)
);

-- VACUUM to actually free pages
VACUUM;
```

### Mitigate — medium term

- Move historic logs to R2 (parquet) nightly (scheduled Worker).
- Upgrade to D1 paid tier if Padosoft is committed long-term
  ($5/month/billing unit).

### After

Add the trim commands to a scheduled Worker cron (Phase 7.4+).

---

## Playbook 5 — Ingest rate climbing / Worker CPU limit hit

### Symptoms
- Worker logs show `Exceeded CPU time limit`.
- Ingest latency spikes visible in Analytics Engine.

### Diagnose

1. **Which event type is climbing?**:
   ```sql
   -- CF Analytics SQL
   SELECT blob3 AS type, COUNT(*)
   FROM trading_system_metrics
   WHERE blob1 = 'ingest.event_type'
     AND timestamp > NOW() - INTERVAL '1' HOUR
   GROUP BY blob3;
   ```
2. Common culprit: a .NET worker stuck in a retry loop emitting the same
   Outbox event thousands of times.

### Mitigate

- Find the offending supervisor loop: check
  `logs/supervisor-*.log` → last 500 lines, count repeating `EnqueueAsync`
  calls.
- Manually drain the bad Outbox rows:
  ```sql
  -- On the supervisor SQLite
  UPDATE sync_outbox SET status='failed', last_error='manually aborted'
  WHERE event_type='<type>' AND status='pending';
  ```
- Restart the offending service.

---

## Playbook 6 — Sentry inbox flooded with the same error

### Symptoms
- Sentry dashboard shows 10k+ occurrences of the same error in < 1h.

### Mitigate

1. **Silence the issue** in Sentry → "Ignore until condition met".
2. **Rate-limit at the source**: if the error is in `api.ts`, add a
   throttle (the existing retry policy is 2 retries at
   `dashboard/src/lib/api.ts:11`; verify the recent error didn't break the
   retry budget).
3. **Deploy the fix** as a hotfix PR.

### After

Revisit Sentry alert thresholds — if a single error type can flood the
inbox, lower `tracesSampleRate` temporarily or add fingerprinting rules
in the Sentry project settings.

---

## Phase 7.4 — Safety gate playbooks

### Order blocked by SemaphoreGate

**Symptom**: log line `SemaphoreGate blocked order: ...` + Critical alert
"Order blocked by SemaphoreGate" in Telegram/email.

**What happened**: the Cloudflare Worker's composite risk indicator
(`GET /api/risk/semaphore`) returned `status=red`, so the .NET-side
`SemaphoreGate` refused the order before it reached IBKR. An audit row
with `outcome=rejected_semaphore` was written to `order_audit_log`.

**Investigate in order**:

1. Confirm the Worker state:

   ```bash
   curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
     https://<your-worker>.workers.dev/api/risk/semaphore | jq .
   ```

   Check the `status` field and the four sub-indicator rows. The
   `X-Data-Source: fallback-mock` header means D1 has no real data (dev mode);
   the mock payload returns `orange` so you shouldn't see RED from a fresh
   install unless real data is flowing.

2. Walk the sub-indicators. If **ivts > 1.15** or **vix_level percentile > 80**,
   the market is telling you to stop. Accept the block.

3. If the block looks wrong (stale data, bad thresholds), override
   temporarily: set `Safety:OverrideSemaphore=true` in the service's
   environment and restart. The service WILL log a Fatal-level multi-line
   banner AND send a Critical alert so nobody forgets it's on.

4. **Always remove the override** once the incident is resolved. The
   override persists across restarts.

5. Query the audit table to confirm the block chain:

   ```bash
   curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
     "https://<your-worker>.workers.dev/api/audit/orders?outcome=rejected_semaphore&limit=20" | jq .
   ```

### Trading paused by DailyPnLWatcher

**Symptom**: log line `DailyPnLWatcher: trading paused` + Critical alert
"DailyPnLWatcher: trading paused". All subsequent orders are rejected
with `outcome=rejected_pnl_pause`.

**What happened**: today's equity vs yesterday's close moved more than
`Safety:MaxDailyDrawdownPct` (default 2%). The watcher flipped
`safety_flags.trading_paused = '1'` and stopped. There is NO auto-unpause.

**Investigate in order**:

1. Confirm the drawdown is real (not a data spike):

   ```bash
   curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
     https://<your-worker>.workers.dev/api/performance/today | jq .
   ```

   Look at `accountValue`, `yesterdayClose`, `pnlPct`. A plausible
   drawdown reads like `pnlPct: -2.31`. A spike looks like
   `pnlPct: -78.2` — that's almost certainly a bad `account_equity_daily`
   row from the collector, not a real event.

2. If the drawdown is real: **do nothing with the flag**. Review
   positions, close risk, and only unpause when you're comfortable.

3. If the drawdown is a data bug: fix the bad row, then unpause:

   ```bash
   # Locally on the service host:
   sqlite3 data/options-execution.db "DELETE FROM safety_flags WHERE key='trading_paused';"
   # Or, if you prefer an UPDATE:
   sqlite3 data/options-execution.db "UPDATE safety_flags SET value='0' WHERE key='trading_paused';"
   ```

   The flag-store is strict about "1" meaning set; any other value
   (including "0") unpauses immediately on the next order.

### Circuit breaker open

**Symptom**: log line `CIRCUIT BREAKER TRIPPED: N failures in M minutes`
+ subsequent orders rejected with `outcome=rejected_breaker`.

**What happened**: `Safety:CircuitBreakerFailureThreshold` broker-class
failures accumulated within the rolling window. Only `BrokerReject` and
`Unknown` failures count — `NetworkError` is ignored so transport noise
doesn't trip it. The breaker auto-resets after
`Safety:CircuitBreakerCooldownMinutes`.

**Investigate in order**:

1. Query recent broker rejections to identify the pattern:

   ```bash
   curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
     "https://<your-worker>.workers.dev/api/audit/orders?outcome=rejected_broker&limit=20" | jq .
   ```

   Typical culprits:
   - insufficient margin → reduce position sizes / deposit funds
   - invalid contract → strategy has a bug in contract construction
   - outside RTH → strategy firing during extended hours

2. If the root cause is transient, wait for the cooldown.

3. If the root cause is a fixed bug, re-deploy and the breaker resets
   automatically on next failed attempt clearing the window.

### Querying the Order Audit log

The Worker exposes a read endpoint. Every order placement — success or
blocked — has a row.

```bash
# Last 50 rows (any outcome)
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  "https://<your-worker>.workers.dev/api/audit/orders?limit=50" | jq .

# All blocks from a specific day
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  "https://<your-worker>.workers.dev/api/audit/orders?from=2026-04-20&outcome=rejected_semaphore" | jq .

# All errors
curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
  "https://<your-worker>.workers.dev/api/audit/orders?outcome=error&limit=100" | jq .
```

Valid `outcome` values: `placed`, `filled`, `rejected_semaphore`,
`rejected_pnl_pause`, `rejected_max_size`, `rejected_max_value`,
`rejected_max_risk`, `rejected_min_balance`, `rejected_breaker`,
`rejected_broker`, `error`.

The local mirror (`order_audit_log_local` in `options-execution.db`)
keeps the same rows for offline inspection when the Worker is unreachable.

---

## Playbook 7 — Rotating secrets

### When

Every 90 days per environment, or **immediately** on any of:

- Suspected leak (secret committed to git, emailed, posted in chat).
- Operator offboarding (former operator had access to the password manager).
- Provider-initiated rotation (Cloudflare forces key rotation; BotFather
  warns of abuse).

### Procedure

Full step-by-step lives in [`docs/ops/SECRETS.md`](./SECRETS.md). The short
version:

```bash
# 1. Regenerate the secret at the source (Cloudflare / Telegram / etc.).
#    Store BOTH old + new in your password manager before touching any file.

# 2. Update cleartext env file (gitignored).
$EDITOR secrets/.env.production

# 3. Re-wrap DPAPI side (if applicable — secrets shared with .NET services).
echo -n "NEW_VALUE" | dotnet run --project src/Tools/EncryptConfigValue
#   → paste DPAPI:<blob> into appsettings.Production.json

# 4. Push to Cloudflare.
./scripts/provision-secrets.sh production

# 5. Restart services.
Restart-Service TradingSupervisorService
Restart-Service OptionsExecutionService

# 6. Monitor 10 minutes (see Playbook 1 + 2 for signals to watch).

# 7. Revoke OLD secret at source. Done.
```

### Rollback

If step 6 surfaces 401s / AUTH_FAILED metric spikes:

1. Copy OLD secret back from your password manager.
2. Re-run steps 2-5 with the old value.
3. Do not revoke the old secret at the source until investigation is complete.

### After

If this was an emergency rotation, instantiate the
[`.github/ISSUE_TEMPLATE/secret-rotation.md`](../../.github/ISSUE_TEMPLATE/secret-rotation.md)
issue to track follow-up (revoke old secret at source, update PM entries,
schedule the next calendar rotation).

---

## Playbook 8 — Test regressions

### When

One of these alarms has fired (or is about to):

- CI went red with a **Playwright E2E failure** that wasn't red 5 minutes ago.
- A **k6 load-test run** shows a baseline latency drift >20% vs. the
  last-recorded numbers in `docs/ops/LOAD_TESTING.md`.
- A **contract test** started failing (`tests/TradingSupervisorService.ContractTests/`
  on the .NET side or `infra/cloudflare/worker/test/contract.test.ts` on
  the Worker side).

### Triage flow

```
1. Playwright flake?
   → Re-run the job. If it fails twice in a row with the SAME assertion,
     it's not flake. Go to "Playwright failure below".
   → If it fails in different places each retry, suspect a timing / animation
     change. Check motion-library versions in the dashboard commit range.

2. k6 baseline drift?
   → Go to "k6 baseline drift below".

3. Contract test?
   → Go to "Contract drift below".
```

### Playwright failure

Triggered by `.github/workflows/playwright-e2e.yml`.

1. Download the `playwright-report` artifact from the failed run (retained
   14 days). Open `index.html` locally — the report has the full trace
   viewer for each failed test.
2. Categorise the failure:
   - **Assertion on a selector that used to work** → a component likely
     changed its `aria-label` / text / role. Update the spec; the spec
     lives under `dashboard/tests/e2e/`.
   - **Timeout waiting for `networkidle`** → a new fetch loop is running
     forever. Check recent additions to `usePerformanceSummary`,
     `usePositions`, etc. — is there an uncapped polling interval?
   - **Console error captured by `sidebar-navigation.spec.ts`** → a
     component is throwing at render time. Check the browser console in
     the trace. Fix the component, not the test.
3. If the failure is in CI only (not locally), confirm you're running
   against the SAME Vite dev server config. The CI workflow supports a
   `PLAYWRIGHT_STAGING_URL` mode; if that's set, the failure may be a
   staging-env issue (not a code issue).
4. **Do not** disable the failing test without opening a tracking issue.
   Zero-tolerance on tech debt per `.claude/rules/code-quality.md`.

### k6 baseline drift

See `docs/ops/LOAD_TESTING.md` for the graded policy. Quick version:

1. Re-run the scenario three times. If the median p(95) is still >20% off
   baseline, it's real.
2. Run `git log --oneline <last-known-good>..HEAD --grep 'worker\|d1'`
   to shortlist commits that could have changed Worker/D1 behaviour.
3. Most common culprits (in order):
   - New D1 query in a route with no covering index.
   - A middleware addition in `infra/cloudflare/worker/src/middleware/`
     that runs on every request (auth caching regression).
   - Cache TTL change in `lib/metrics.ts` / route handlers.
4. If you can't root-cause in 60 min, update `LOAD_TESTING.md` with a
   TODO line documenting the regression, open an issue, and block the
   offending PR from reaching production.

### Contract drift (fixture vs. code)

See `tests/Contract/README.md` for the full procedure. Triage:

1. `.NET` side failure (`TradingSupervisorService.ContractTests`) — a
   DTO / anonymous-object emitter changed shape. Either update the
   fixture under `tests/Contract/fixtures/outbox-events/<type>.json`
   (if the change was intentional) or revert the producer change.
2. Worker side failure (`contract.test.ts`) — a Zod schema diverged
   from the fixture. Either update the schema in
   `infra/cloudflare/worker/src/routes/ingest.ts` (if the fixture was
   revised correctly on the .NET side) or revert the Zod change.
3. **Critical**: both sides MUST be updated in the SAME PR. A fixture
   update without a matching Zod/DTO change (or vice versa) means the
   two halves of the contract disagree — that's the exact drift the
   tests exist to catch.
4. After updating, run BOTH suites:
   ```bash
   dotnet test tests/TradingSupervisorService.ContractTests --no-build
   cd infra/cloudflare/worker && bunx vitest run test/contract.test.ts
   ```

### After

- File a post-mortem issue in the repo tagged `phase7.6-regression` if
  the incident had customer-visible impact (none of the Phase 7.6 test
  types directly affect users — they're all guard rails).
- Update this playbook with anything you learned that isn't captured here.
- If a new fixture was added, verify the `Every_known_event_type_has_a_fixture`
  sentinel test in `OutboxEventContractTests.cs` still passes. That test
  is the drift-detector-of-last-resort.

---

## Playbook 9 — Go-live incident (first 24h live)

Phase 7.7 added this playbook specifically for the window between
`GO_LIVE.md` flip and the 24h-post-live retrospective. The rule of
the first 24h is: err hard toward aborting. Real money in the loop
changes the calculus; a false positive (unnecessary rollback) costs
an hour of operator time. A false negative (ignored real problem)
costs money.

### Trigger signals

Any of these during the first 24h of live == go to `GO_LIVE.md` § 4
(rollback) IMMEDIATELY, without further diagnosis:

- A Critical Telegram alert that wasn't seen during the 14-day paper
  validation.
- An order placed that you did not expect (regardless of outcome).
- Positions in the Worker diverge from TWS (operator visual check).
- Semaphore flips red with no concurrent market reason (check
  Bloomberg, major news feed).
- Either service enters `Stopped` state without an operator request.
- Any log line at `Fatal` level.

### Before rolling back

Quick context capture (30 seconds max — do not let this delay the
rollback decision):

```powershell
# Snapshot last 200 log lines from both services so you have
# forensic data AFTER rollback.
$ts = Get-Date -Format "yyyyMMdd-HHmmss"
Copy-Item "C:\trading-system\logs\supervisor-*.log" `
          "C:\trading-system\incidents\go-live-$ts-supervisor.log" -Force
Copy-Item "C:\trading-system\logs\options-*.log" `
          "C:\trading-system\incidents\go-live-$ts-options.log" -Force
```

### Execute rollback

Follow `docs/ops/GO_LIVE.md` § 4 step-by-step. Target: paper-mode
confirmation within 60 seconds of the abort decision.

### Post-rollback investigation

1. Pull the incident log files captured above into the repo
   (attach to the `phase7.7-abort` issue).
2. Cross-reference the trigger signal with the order audit log:
   ```bash
   # If an unexpected order triggered the abort:
   curl -s -H "X-Api-Key: $CLOUDFLARE_API_KEY" \
     "https://trading-bot.padosoft.workers.dev/api/audit/orders?from=$(date -u +%Y-%m-%dT%H:00:00)" \
     | jq '.rows[]'
   ```
3. Root-cause analysis MUST complete before any re-go-live attempt.
   Patch fixes go through a fresh ≥ 3-day paper session (not the
   full 14-day — this is a pattern drift, not a regression from
   the validated build).

### What NOT to do

- Do not try to "fix forward" during the first 24h. If the issue
  warrants fixing, the system should be in paper while you fix.
- Do not silence alerts. An alert that fired during the first 24h
  is the highest-signal alert we'll ever get.
- Do not change TradingMode repeatedly. One flip per day max.
  Multiple flips in a day = operator is improvising = time to stop
  and think.

### Re-go-live preconditions

After an abort, to attempt go-live again:

1. Root cause documented in `docs/ops/post-live/abort-YYYY-MM-DD.md`.
2. Fix merged to main, CI green.
3. Fresh 3-day paper-mode run from the patched build (not the old
   validation — that build is tainted by the fix gap).
4. Re-run `GO_LIVE.md` precondition checklist (all 9 items).
5. Announce in Telegram before the new flip.

---

## Escalation

- **Padosoft on-call**: lorenzo.padovani@padosoft.com (this is a solo-op
  runbook; escalation = the operator themselves).
- **Cloudflare support**: dashboard → Support (paid plan only).
- **IBKR support**: 1-877-442-2757 (US) during market hours.

---

*Related: `docs/ops/OBSERVABILITY.md` (signal pipeline),
`docs/DEPLOYMENT_GUIDE.md` (deploy + rollback),
`docs/ops/GO_LIVE.md` (first-24h specifics referenced in Playbook 9),
`docs/ops/SLO.md` (objectives that determine what counts as an incident).*
