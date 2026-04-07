# E2E-08: Cloudflare Worker Unreachable and Outbox Queuing

> Manual test checklist for Worker failure and event queuing resilience
> REQUIRES_PAPER: No — Can test without IBKR (uses local services only)

---

## Prerequisites

- [ ] TradingSupervisorService running
- [ ] Cloudflare Worker deployed (initially reachable)
- [ ] OutboxSyncWorker enabled

---

## Test Steps

### 1. Verify Normal Sync Operation

**Expected in logs**:
- [ ] Log: "OutboxSyncWorker: Processing [N] pending events"
- [ ] Log: "Event [id] synced successfully to Cloudflare Worker"

**Verification**:
```sql
SELECT COUNT(*) FROM sync_outbox WHERE sync_status = 'synced';
-- Should show >= 1 (baseline)
```

---

### 2. Simulate Worker Unreachable

**Action (choose one)**:

**Option A**: Firewall block
```powershell
New-NetFirewallRule -DisplayName "Block Cloudflare" -Direction Outbound -RemoteAddress 104.21.0.0/16 -Action Block
```

**Option B**: Invalid URL in config
```json
{
  "CloudflareWorkerUrl": "https://invalid.example.com/events"
}
```

**Option C**: Unpublish Worker (via Cloudflare dashboard)

---

### 3. Monitor Outbox Queuing

**Expected in logs** (on next sync cycle):
- [ ] Log: "HTTP POST to Worker failed: [error]"
- [ ] Log: "Event [id] sync failed, attempt 1"
- [ ] Log: "Queuing event for retry in 10 seconds"

**Verification**:
```sql
SELECT * FROM sync_outbox WHERE sync_status = 'pending' AND attempts > 0;
-- Should show events with incremented attempts
```

**Expected**:
- [ ] Events remain in 'pending' status
- [ ] attempts counter increments
- [ ] last_error contains error message
- [ ] next_retry_at populated with future timestamp

---

### 4. Generate More Events

**Action**: Wait for HeartbeatWorker to generate new heartbeat events

**Expected**:
- [ ] New events created despite Worker down
- [ ] All events queue in sync_outbox
- [ ] No data loss

**Verification**:
```sql
SELECT COUNT(*) FROM sync_outbox WHERE sync_status = 'pending';
-- Should show multiple pending events (growing)
```

**Expected**:
- [ ] Outbox acts as buffer
- [ ] Events persist to disk (SQLite)
- [ ] Service continues operating normally

---

### 5. Monitor Retry Backoff

**Expected in logs** (over 5 minutes):
- [ ] Log: "Retry attempt 1: delay 10s"
- [ ] Log: "Retry attempt 2: delay 20s"
- [ ] Log: "Retry attempt 3: delay 40s"
- [ ] Log: "Retry attempt 4: delay 60s (capped)"

**Expected**:
- [ ] Exponential backoff applied
- [ ] Max delay cap (e.g., 60s)
- [ ] Retries continue until max attempts

---

### 6. Restore Worker Connectivity

**Action**: Remove firewall rule or fix config

```powershell
Remove-NetFirewallRule -DisplayName "Block Cloudflare"
# OR: Restore correct Worker URL in config and restart service
```

---

### 7. Verify Batch Sync on Recovery

**Expected in logs** (on next sync cycle):
- [ ] Log: "Worker connectivity restored"
- [ ] Log: "Processing [N] pending events in batch"
- [ ] Log: "Batch sync: [N] succeeded, 0 failed"
- [ ] Log: "All queued events synced successfully"

**Verification**:
```sql
SELECT COUNT(*) FROM sync_outbox WHERE sync_status = 'pending';
-- Should return 0 (all synced)

SELECT COUNT(*) FROM sync_outbox WHERE sync_status = 'synced' AND synced_at > datetime('now', '-5 minutes');
-- Should show batch of recently synced events
```

**Expected**:
- [ ] All queued events synced
- [ ] Correct order preserved (FIFO)
- [ ] No duplicate syncs

---

### 8. Verify Data Integrity in D1

**Action**: Query Cloudflare D1 database

```bash
bunx wrangler d1 execute trading-db --command "SELECT COUNT(*) FROM service_heartbeats WHERE recorded_at > datetime('now', '-10 minutes')"
```

**Expected**:
- [ ] All events received (count matches local outbox)
- [ ] No gaps in data
- [ ] Timestamps in correct order

---

## Success Criteria

- [ ] Events queue when Worker unreachable
- [ ] Retry logic with exponential backoff
- [ ] Service continues operating (no crash)
- [ ] No data loss during outage
- [ ] Batch sync on recovery
- [ ] All events eventually synced (eventual consistency)
- [ ] Correct order preserved

---

## Performance Benchmarks

- **Outbox queue capacity**: Unlimited (SQLite disk-based)
- **Batch sync throughput**: 100+ events/second
- **Recovery time**: < 1 minute after connectivity restored
- **Memory overhead**: Minimal (events stored in DB, not RAM)

---

## Cleanup

```sql
-- Delete old synced events (optional)
DELETE FROM sync_outbox WHERE sync_status = 'synced' AND synced_at < datetime('now', '-1 day');
```

---

**Test Duration**: 10-15 minutes  
**Last Updated**: 2026-04-05  
**Dependencies**: TradingSupervisorService running
