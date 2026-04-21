---
title: "E2E-02: Heartbeat Sync to Cloudflare and Outbox Processing"
tags: ["dev", "testing", "worker"]
status: reference
audience: ["developer"]
last-reviewed: "2026-04-21"
---

# E2E-02: Heartbeat Sync to Cloudflare and Outbox Processing

> Manual test checklist for event synchronization from services to Cloudflare Worker
> REQUIRES_PAPER: Partial — Cloudflare Worker must be deployed, IBKR optional

---

## Prerequisites

- [ ] E2E-01 (Startup) completed successfully
- [ ] Both services running (TradingSupervisorService, OptionsExecutionService)
- [ ] Cloudflare Worker deployed with D1 database initialized
- [ ] Worker URL configured in supervisor.json (`CloudflareWorkerUrl`)
- [ ] Worker authentication token configured (if required)

---

## Test Steps

### 1. Verify Outbox Table Exists

**Action**: Query supervisor.db

```sql
SELECT COUNT(*) FROM sync_outbox;
-- Should return 0 (empty on first run)

SELECT name FROM sqlite_master WHERE type='table' AND name='sync_outbox';
-- Should return 1 row (table exists)
```

**Expected**:
- [ ] Table `sync_outbox` exists
- [ ] Initial row count is 0

---

### 2. Generate Heartbeat Event

**Action**: Wait 30 seconds for HeartbeatWorker to run

**Expected in logs**:
- [ ] Log: "HeartbeatWorker: Recording heartbeat"
- [ ] Log: "Heartbeat inserted into database"
- [ ] Log: "Outbox event created: event_type=heartbeat"

**Verification Query**:
```sql
-- In supervisor.db
SELECT * FROM sync_outbox ORDER BY created_at DESC LIMIT 5;
-- Should show at least 1 row:
--   - event_id: UUID
--   - event_type: 'heartbeat'
--   - payload: JSON with heartbeat data
--   - sync_status: 'pending'
--   - attempts: 0
--   - created_at: recent timestamp
--   - synced_at: NULL
```

**Expected**:
- [ ] At least 1 outbox event with type 'heartbeat'
- [ ] sync_status = 'pending'
- [ ] payload is valid JSON

---

### 3. Outbox Sync Worker Processing

**Action**: Wait 10 seconds for OutboxSyncWorker to run (runs every 10s)

**Expected in logs**:
- [ ] Log: "OutboxSyncWorker: Processing pending events"
- [ ] Log: "Found X pending events in outbox"
- [ ] Log: "Sending event [event_id] to Cloudflare Worker"
- [ ] Log: "HTTP POST to [worker_url]/events"
- [ ] Log: "Worker response: 200 OK"
- [ ] Log: "Event [event_id] marked as synced"

**Verification Query**:
```sql
SELECT * FROM sync_outbox WHERE sync_status = 'synced' ORDER BY synced_at DESC LIMIT 5;
-- Should show at least 1 row:
--   - sync_status: 'synced'
--   - attempts: 1 (or more if retried)
--   - synced_at: NOT NULL (recent timestamp)
--   - last_error: NULL
```

**Expected**:
- [ ] Event sync_status changed from 'pending' to 'synced'
- [ ] synced_at is populated
- [ ] last_error is NULL (no errors)

---

### 4. Verify Data in Cloudflare D1

**Action**: Query Cloudflare D1 database via wrangler or dashboard

```bash
# Using wrangler CLI
cd infra/cloudflare/worker
bunx wrangler d1 execute trading-db --command "SELECT * FROM service_heartbeats ORDER BY recorded_at DESC LIMIT 5"
```

**Expected**:
- [ ] At least 1 row in `service_heartbeats` table
- [ ] Heartbeat data matches local supervisor.db
- [ ] Fields: hostname, cpu_percent, ram_percent, trading_mode='paper'
- [ ] recorded_at timestamp matches

**Alternative** (if Worker API is accessible):
```powershell
# Query Worker API endpoint
$response = Invoke-RestMethod -Uri "https://[worker-url]/api/heartbeats?limit=5" -Method Get
$response | ConvertTo-Json
```

**Expected**:
- [ ] HTTP 200 response
- [ ] JSON array with at least 1 heartbeat
- [ ] Data matches local database

---

### 5. Test Retry Logic on Worker Failure

**Action**: Stop Cloudflare Worker or disconnect internet

```powershell
# Simulate network failure by modifying hosts file (or firewall rule)
# Or: Set invalid Worker URL in supervisor.json and restart service
```

**Expected within 30 seconds**:
- [ ] Log: "OutboxSyncWorker: Processing pending events"
- [ ] Log: "HTTP POST failed: [error message]"
- [ ] Log: "Event [event_id] sync failed, attempt 1"
- [ ] Log: "Retrying in [seconds] seconds"

**Verification Query**:
```sql
SELECT * FROM sync_outbox WHERE sync_status = 'pending' AND attempts > 0;
-- Should show events with:
--   - sync_status: 'pending'
--   - attempts: 1, 2, 3, ... (incrementing)
--   - last_error: NOT NULL (contains error message)
--   - next_retry_at: future timestamp
```

**Expected**:
- [ ] Failed events remain in 'pending' status
- [ ] attempts counter increments
- [ ] last_error contains descriptive message
- [ ] Exponential backoff applied (next_retry_at increases)

---

### 6. Verify Retry Success After Recovery

**Action**: Restore Cloudflare Worker / network connectivity

**Expected within next retry interval**:
- [ ] Log: "Retrying event [event_id], attempt [N]"
- [ ] Log: "Worker response: 200 OK"
- [ ] Log: "Event [event_id] marked as synced"

**Verification Query**:
```sql
SELECT * FROM sync_outbox WHERE event_id = '[failed_event_id]';
-- Should show:
--   - sync_status: 'synced'
--   - attempts: N (number of retries)
--   - synced_at: NOT NULL
--   - last_error: NULL (cleared on success)
```

**Expected**:
- [ ] Previously failed events now synced
- [ ] attempts shows total retry count
- [ ] last_error cleared

---

### 7. Test Max Retry Limit

**Action**: Modify supervisor.json to set max retry attempts = 3, then simulate permanent failure

```json
{
  "OutboxSync": {
    "MaxRetryAttempts": 3,
    "CloudflareWorkerUrl": "https://invalid.example.com/events"
  }
}
```

**Expected after 3 retry attempts**:
- [ ] Log: "Event [event_id] failed after 3 attempts"
- [ ] Log: "Moving event to failed status"

**Verification Query**:
```sql
SELECT * FROM sync_outbox WHERE sync_status = 'failed';
-- Should show:
--   - sync_status: 'failed'
--   - attempts: 3 (max reached)
--   - last_error: NOT NULL
```

**Expected**:
- [ ] Events with failed status stop retrying
- [ ] Manual intervention required (or admin API to reset)

---

### 8. Outbox Cleanup (Optional Feature)

**Action**: If implemented, verify old synced events are archived/deleted

**Verification Query**:
```sql
-- After 24 hours (or configured retention period)
SELECT COUNT(*) FROM sync_outbox WHERE sync_status = 'synced' AND synced_at < datetime('now', '-1 day');
-- Should return 0 (if cleanup is active)
```

**Expected** (if cleanup implemented):
- [ ] Old synced events removed to prevent table bloat
- [ ] Failed events retained for troubleshooting

---

## Success Criteria

- [ ] Outbox events created for heartbeats
- [ ] Events successfully synced to Cloudflare Worker (verified in D1)
- [ ] Retry logic works on temporary failures
- [ ] Exponential backoff prevents request storms
- [ ] Max retry limit prevents infinite loops
- [ ] Failed events logged with descriptive errors
- [ ] Worker API returns synced data correctly

---

## Performance Benchmarks

- **Outbox creation**: < 10ms per event
- **Sync latency**: < 5 seconds from creation to Worker (under normal conditions)
- **Batch sync**: Up to 100 events per sync cycle
- **Retry interval**: 10s, 30s, 60s, 120s, ... (exponential backoff)

---

## Cleanup

```powershell
# Reset configuration
# Restore correct Worker URL in supervisor.json

# Clear test events (optional)
$conn = New-Object -TypeName System.Data.SQLite.SQLiteConnection -ArgumentList "Data Source=C:\ProgramData\TradingSystem\supervisor.db"
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "DELETE FROM sync_outbox WHERE event_type = 'heartbeat' AND created_at < datetime('now', '-1 hour')"
$cmd.ExecuteNonQuery()
$conn.Close()
```

---

## Troubleshooting

### Events Stuck in Pending

- Check Worker URL is correct and accessible
- Verify authentication token (if required)
- Check firewall/proxy settings
- Inspect last_error column for details

### Worker Returns 400/500

- Check payload JSON format
- Verify D1 database schema matches
- Check Worker logs in Cloudflare dashboard
- Ensure CORS headers configured

### High Retry Attempts

- Indicates persistent connectivity issues
- Check network stability
- Consider increasing retry interval
- Review Worker capacity/rate limits

---

**Test Duration**: ~5 minutes  
**Last Updated**: 2026-04-05  
**Dependencies**: E2E-01 (Startup), Cloudflare Worker deployed
