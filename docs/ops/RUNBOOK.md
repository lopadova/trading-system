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

## Escalation

- **Padosoft on-call**: lorenzo.padovani@padosoft.com (this is a solo-op
  runbook; escalation = the operator themselves).
- **Cloudflare support**: dashboard → Support (paid plan only).
- **IBKR support**: 1-877-442-2757 (US) during market hours.

---

*Related: `docs/ops/OBSERVABILITY.md` (signal pipeline),
`docs/DEPLOYMENT_GUIDE.md` (deploy + rollback).*
