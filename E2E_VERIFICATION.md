# End-to-End Alert Flow Verification

**Purpose**: Verify complete data flow from alert generation to dashboard display  
**Duration**: ~10 minutes  
**Prerequisites**: All services running (TradingSupervisor, Worker, Dashboard, TWS)

---

## Pre-Verification Checklist

- [ ] TradingSupervisorService compiled successfully (0 errors)
- [ ] All 23 tests passed (GreeksMonitor: 12, LogReader: 6, IvtsMonitor: 5)
- [ ] TEST_PLAN.md enhanced to 3549 lines
- [ ] Outbox integration code present in all 3 workers

**Status**: ✅ ALL PRE-CHECKS PASSED (verified by orchestrator agents)

---

## Step 1: Start Services

### 1.1 Start TradingSupervisorService
```powershell
# Navigate to service directory
cd src/TradingSupervisorService

# Run service
dotnet run --configuration Release

# Expected log output:
# "TradingSupervisorService started"
# "HeartbeatWorker started. Interval=60s"
# "LogReaderWorker started. LogPath=... Interval=30s"
# "OutboxSyncWorker started. Interval=30s"
# "GreeksMonitorWorker started. Interval=60s"
```

**Verify**: All workers show "started" in logs

### 1.2 Start Cloudflare Worker
```powershell
# Navigate to worker directory
cd infra/cloudflare/worker

# Start dev server
npm run dev
# OR: bun run dev

# Expected output:
# "⚡ Listening on http://localhost:8787"
```

**Verify**: Worker responds to `curl http://localhost:8787/api/heartbeats`

### 1.3 Verify TWS Running
- Open Interactive Brokers TWS (Paper Trading mode)
- Ensure API enabled (Configure → API → Settings → Enable ActiveX and Socket Clients)
- Port: 7497 (paper trading)

**Verify**: TradingSupervisorService logs show "IBKR connection established"

---

## Step 2: Trigger Alerts

### 2.1 Greeks Alert (Delta Threshold Breach)

**Method**: Modify configuration to force threshold breach

```powershell
# Edit appsettings.Local.json
# Change: "DeltaThreshold": 0.70 → "DeltaThreshold": 0.01

# Restart TradingSupervisorService
# Wait 60 seconds for GreeksMonitor cycle
```

**Expected Log**:
```
[INF] GreeksMonitorWorker: Position SPY has delta 0.85 (threshold 0.01) - creating alert
[INF] AlertRepository: Alert created successfully (AlertId=...)
[INF] OutboxRepository: Outbox entry created (EventId=..., EventType=alert)
```

**Verification Command**:
```powershell
sqlite3 src/TradingSupervisorService/data/supervisor.db "
SELECT alert_type, severity, message, created_at 
FROM alert_history 
WHERE alert_type LIKE 'Greeks%' 
ORDER BY created_at DESC LIMIT 1;
"
```

**Expected Result**:
```
GreeksDelta|warning|High delta risk: position SPY has delta 0.85 (threshold 0.01)|2026-04-20T12:00:00Z
```

### 2.2 LogReader Alert (ERROR Log Entry)

**Method**: Append ERROR line to monitored log file

```powershell
# Find log file path (check appsettings.json LogReader:OptionsServiceLogPath)
# Typically: logs/options-execution-YYYYMMDD.log

# Append ERROR line
echo "[2026-04-20 12:30:00 ERR] Test error message for E2E verification" >> logs/options-execution-20260420.log

# Wait 30 seconds for LogReaderWorker cycle
```

**Expected Log**:
```
[INF] LogReaderWorker: Detected ERROR log entry at line 1234
[INF] AlertRepository: Alert created successfully (AlertType=LogError)
[INF] OutboxRepository: Outbox entry created (EventType=alert)
```

**Verification Command**:
```powershell
sqlite3 src/TradingSupervisorService/data/supervisor.db "
SELECT alert_type, severity, message, created_at 
FROM alert_history 
WHERE alert_type = 'LogError' 
ORDER BY created_at DESC LIMIT 1;
"
```

**Expected Result**:
```
LogError|error|OptionsExecutionService: Test error message for E2E verification|2026-04-20T12:30:30Z
```

### 2.3 IVTS Alert (IV Rank Threshold Breach)

**Method**: Enable IvtsMonitor (currently disabled by default)

**Note**: This test requires 15 minutes wait and IBKR market data subscription.

**Optional**: Skip this test for initial E2E verification. Focus on Greeks + LogReader.

---

## Step 3: Verify Outbox Entries Created

**Wait**: 5 seconds after alerts triggered

**Verification Command**:
```powershell
sqlite3 src/TradingSupervisorService/data/supervisor.db "
SELECT 
    event_type,
    status,
    payload_json,
    created_at,
    sent_at
FROM sync_outbox 
WHERE event_type = 'alert' 
AND created_at > datetime('now', '-5 minutes')
ORDER BY created_at DESC;
"
```

**Expected Results**:
- At least 2 entries (GreeksDelta + LogError)
- event_type: "alert"
- status: "pending" (initially) or "sent" (after OutboxSyncWorker processes)
- payload_json: Contains camelCase JSON with alertId, alertType, severity, message
- created_at: Recent timestamp

**Sample Payload**:
```json
{
  "alertId": "123e4567-e89b-12d3-a456-426614174000",
  "alertType": "GreeksDelta",
  "severity": "warning",
  "message": "High delta risk: position SPY has delta 0.85 (threshold 0.01)",
  "detailsJson": "{\"position\":\"SPY\",\"delta\":0.85,\"threshold\":0.01}",
  "sourceService": "TradingSupervisorService",
  "createdAt": "2026-04-20T12:00:00.0000000Z",
  "resolvedAt": null,
  "resolvedBy": null
}
```

---

## Step 4: Verify OutboxSyncWorker Processes Entries

**Wait**: 30 seconds (OutboxSyncWorker interval)

**Expected Log**:
```
[INF] OutboxSyncWorker: Starting sync cycle...
[INF] OutboxSyncWorker: Found 2 pending events (heartbeat: 0, alert: 2, position: 0)
[INF] OutboxSyncWorker: Sending event... (EventId=..., EventType=alert)
[INF] OutboxSyncWorker: Event sent successfully (Status=OK, Duration=245ms)
[INF] OutboxSyncWorker: Outbox sync cycle completed: 2 sent, 0 failed
```

**Verification Command**:
```powershell
sqlite3 src/TradingSupervisorService/data/supervisor.db "
SELECT 
    event_type,
    status,
    COUNT(*) as count,
    MAX(sent_at) as last_sent
FROM sync_outbox 
WHERE created_at > datetime('now', '-5 minutes')
GROUP BY event_type, status;
"
```

**Expected Result**:
```
alert|sent|2|2026-04-20 12:01:30
```

**If status is still "pending"**:
- Check OutboxSyncWorker logs for errors
- Check Worker API is accessible: `curl http://localhost:8787/api/v1/ingest`
- Verify API_KEY matches in .dev.vars and appsettings.Local.json

---

## Step 5: Verify Cloudflare Worker Received Alerts

**Verification Command**:
```powershell
curl http://localhost:8787/api/alerts | jq '.[-5:]'
```

**Expected Response** (2 recent alerts):
```json
[
  {
    "alertId": "123e4567-e89b-12d3-a456-426614174000",
    "alertType": "GreeksDelta",
    "severity": "warning",
    "message": "High delta risk: position SPY has delta 0.85 (threshold 0.01)",
    "detailsJson": "{\"position\":\"SPY\",\"delta\":0.85}",
    "sourceService": "TradingSupervisorService",
    "createdAt": "2026-04-20T12:00:00Z",
    "resolvedAt": null
  },
  {
    "alertId": "234e5678-f90c-23e4-b567-537725285111",
    "alertType": "LogError",
    "severity": "error",
    "message": "OptionsExecutionService: Test error message for E2E verification",
    "sourceService": "TradingSupervisorService",
    "createdAt": "2026-04-20T12:30:30Z"
  }
]
```

**If response is empty or 404**:
- Check Worker logs for "POST /api/v1/ingest" requests
- Verify Worker started on port 8787
- Check D1 database directly: `wrangler d1 execute trading-db-dev --command "SELECT * FROM alert_history ORDER BY created_at DESC LIMIT 5"`

---

## Step 6: Verify Dashboard Displays Alerts

### 6.1 Open Dashboard
```
http://localhost:3000/alerts
```

### 6.2 Expected Display

**AlertsPage.tsx should show**:
- 2 new alerts in the table
- GreeksDelta: Orange/yellow warning badge
- LogError: Red error badge
- Correct timestamps
- Correct messages

**Filters should work**:
- Filter by Severity: Warning → shows GreeksDelta only
- Filter by Type: LogError → shows LogError only
- Filter by Status: Unresolved → shows both (since resolvedAt is null)

### 6.3 Real-Time Updates

**Test auto-refresh**:
1. Trigger another alert (change threshold again)
2. Wait 10 seconds (default refresh interval)
3. Dashboard should show new alert without manual refresh

---

## Step 7: Performance Verification

### 7.1 Measure Latency

**Alert Generation → Dashboard Display**:

```
Alert Created:        12:00:00 (in service log)
Outbox Entry Created: 12:00:00 (same millisecond)
OutboxSync Sent:      12:00:30 (30s later - worker interval)
Worker Processed:     12:00:30 (< 1s processing)
Dashboard Refresh:    12:00:40 (10s later - refresh interval)
```

**Total Latency**: ~40 seconds (acceptable for non-critical alerts)

### 7.2 Verify No Data Loss

**Command**:
```powershell
# Count alerts in local DB
$localCount = sqlite3 src/TradingSupervisorService/data/supervisor.db "
SELECT COUNT(*) FROM alert_history 
WHERE created_at > datetime('now', '-10 minutes');"

# Count alerts in Worker (via API)
$workerCount = (curl -s http://localhost:8787/api/alerts?since=10m | jq 'length')

Write-Host "Local alerts: $localCount"
Write-Host "Worker alerts: $workerCount"
Write-Host "Match: $($localCount -eq $workerCount)"
```

**Expected**: Counts match (no data loss)

---

## Step 8: Error Scenario Testing

### 8.1 Worker Unavailable

**Test**: Stop Worker, trigger alert, verify retry logic

```powershell
# 1. Stop Worker (Ctrl+C in worker terminal)

# 2. Trigger alert (modify threshold)

# 3. Check outbox status after 30s
sqlite3 src/TradingSupervisorService/data/supervisor.db "
SELECT event_id, status, retry_count, last_error 
FROM sync_outbox 
WHERE status = 'pending' AND retry_count > 0;"

# Expected: Alert in 'pending' status with retry_count=1, last_error="Connection refused"

# 4. Restart Worker

# 5. Wait 30s for retry

# 6. Verify status changed to 'sent'
```

### 8.2 IBKR Disconnect

**Test**: Close TWS, verify service survives

```powershell
# 1. Close TWS application

# 2. Check TradingSupervisorService logs
# Expected: "IBKR connection lost" (warning, not error)

# 3. Verify HeartbeatWorker continues
# Expected: IBKRConnected=false in heartbeats

# 4. Restart TWS

# 5. Check logs
# Expected: "IBKR connection re-established"
```

---

## Success Criteria

### All Checks Must Pass ✅

- [ ] **Build**: 0 errors, 0 warnings
- [ ] **Tests**: 23/23 passed (12 + 6 + 5)
- [ ] **Alert Generation**: Greeks + LogReader alerts created in local DB
- [ ] **Outbox Integration**: Entries created with event_type="alert", status="pending"
- [ ] **OutboxSync**: Status changes to "sent" within 30 seconds
- [ ] **Worker Ingestion**: Alerts visible via `curl http://localhost:8787/api/alerts`
- [ ] **Dashboard Display**: Alerts visible in AlertsPage.tsx
- [ ] **Latency**: Alert → Dashboard < 60 seconds
- [ ] **No Data Loss**: Local count = Worker count
- [ ] **Error Recovery**: Worker restart recovers pending alerts
- [ ] **IBKR Resilience**: Service survives TWS disconnect

---

## Troubleshooting

### Issue: Outbox entries not created
**Check**:
```powershell
# Verify workers have IOutboxRepository
grep -n "IOutboxRepository" src/TradingSupervisorService/Workers/*.cs
```
**Expected**: All 3 workers (Greeks, LogReader, Ivts) have the field

### Issue: OutboxSync not sending
**Check**:
```powershell
# Verify OutboxSyncWorker is running
grep "OutboxSyncWorker started" logs/service.log

# Check API_KEY configuration
grep "API key configured" logs/service.log
# Expected: "length=64" (not 14)
```

### Issue: Worker returns 401 Unauthorized
**Check**:
```powershell
# Compare API keys
grep "API_KEY" infra/cloudflare/worker/.dev.vars
grep "CloudflareWorkerApiKey" src/TradingSupervisorService/appsettings.Local.json
```
**Fix**: Ensure keys match exactly (64 characters)

### Issue: Dashboard shows old data
**Check**:
```powershell
# Verify Worker API returns recent data
curl http://localhost:8787/api/alerts?since=5m | jq '.[].createdAt'
```
**Fix**: Hard refresh (Ctrl+Shift+R), check React Query cache settings

---

## Completion Report Template

```markdown
## E2E Verification Results - [DATE]

**Tester**: [Name]
**Duration**: [Minutes]
**Environment**: Paper Trading

### Results
- [ ] All services started successfully
- [ ] Greeks alert triggered and displayed
- [ ] LogReader alert triggered and displayed
- [ ] Outbox sync working (pending → sent)
- [ ] Worker ingestion confirmed
- [ ] Dashboard display verified
- [ ] Latency within acceptable range (< 60s)
- [ ] No data loss detected
- [ ] Error recovery tested

### Issues Found
[None / List issues]

### Sign-Off
**Status**: PASS / FAIL
**Notes**: [Any observations]
```

---

## Next Steps After E2E Verification

1. **If PASS**: Mark Task #4 as completed, proceed to production deployment
2. **If FAIL**: Document issues in SESSION_PROGRESS.md, iterate until fixed
3. **Performance Tuning** (optional):
   - Reduce OutboxSync interval from 30s → 10s for lower latency
   - Increase React Query refresh interval for less server load
   - Add batch processing if alert volume is high

---

**Document Version**: 1.0  
**Last Updated**: 2026-04-20  
**Related**: TEST_PLAN.md, SESSION_PROGRESS.md
