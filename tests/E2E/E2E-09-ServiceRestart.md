---
title: "E2E-09: Service Restart and Campaign Resume"
tags: ["dev", "testing", "incident-response"]
status: reference
audience: ["developer"]
last-reviewed: "2026-04-21"
---

# E2E-09: Service Restart and Campaign Resume

> Manual test checklist for graceful restart and state persistence
> REQUIRES_PAPER: Yes — Active campaign required for full test

---

## Prerequisites

- [ ] E2E-04 (Campaign Open) completed
- [ ] Active campaign with open positions
- [ ] Both services running (TradingSupervisorService, OptionsExecutionService)

---

## Test Steps

### 1. Capture Pre-Restart State

**Query state before restart**:
```sql
-- In options.db
SELECT campaign_id, status, entry_price, entry_date FROM campaigns WHERE status = 'active';
SELECT position_id, contract_symbol, quantity, avg_cost FROM positions WHERE campaign_id = '[campaign_id]';
SELECT order_id, status, filled_price FROM orders WHERE campaign_id = '[campaign_id]';

-- In supervisor.db
SELECT COUNT(*) FROM sync_outbox WHERE sync_status = 'pending';
```

**Record**:
- [ ] Active campaign ID: _____________
- [ ] Position count: _____________
- [ ] Pending outbox events: _____________

---

### 2. Graceful Shutdown (Ctrl+C)

**Action**: Press Ctrl+C in each service terminal

**Expected in logs**:
- [ ] Log: "Application is shutting down..."
- [ ] Log: "Stopping BackgroundService: [WorkerName]"
- [ ] Log: "IbkrConnectionWorker: Disconnecting from IBKR"
- [ ] Log: "All workers stopped gracefully"
- [ ] Log: "Shutdown complete"

**Expected**:
- [ ] Clean shutdown (exit code 0)
- [ ] No database corruption
- [ ] WAL checkpoint completed

---

### 3. Restart Services

**Action**: Start both services again

```powershell
cd src\TradingSupervisorService
dotnet run &

cd ..\OptionsExecutionService
dotnet run &
```

**Expected in logs** (TradingSupervisorService):
- [ ] Log: "Starting TradingSupervisorService"
- [ ] Log: "Database migrations: already applied"
- [ ] Log: "HeartbeatWorker: Starting"
- [ ] Log: "OutboxSyncWorker: Resuming sync ([N] pending events)"

**Expected in logs** (OptionsExecutionService):
- [ ] Log: "Starting OptionsExecutionService"
- [ ] Log: "Database migrations: already applied"
- [ ] Log: "Loading strategies from cache"
- [ ] Log: "IbkrConnectionWorker: Connecting to IBKR"
- [ ] Log: "Connected to IBKR successfully"
- [ ] Log: "CampaignMonitorWorker: Resuming monitoring"

---

### 4. Verify Campaign State Restored

**Expected in logs**:
- [ ] Log: "Loading active campaigns from database"
- [ ] Log: "Found [N] active campaigns"
- [ ] Log: "Campaign [id]: Resuming monitoring (DTE: [days], P&L: [amount])"

**Verification Query**:
```sql
-- Campaign status should be unchanged
SELECT campaign_id, status, entry_price FROM campaigns WHERE campaign_id = '[campaign_id]';
-- Should match pre-restart state
```

**Expected**:
- [ ] Campaign status = 'active' (unchanged)
- [ ] Entry data preserved
- [ ] No duplicate campaigns created

---

### 5. Verify Position State Restored

**Verification Query**:
```sql
SELECT position_id, contract_symbol, quantity, avg_cost FROM positions WHERE campaign_id = '[campaign_id]';
```

**Expected**:
- [ ] All 4 positions present
- [ ] Quantities unchanged
- [ ] avg_cost preserved
- [ ] No duplicate positions

---

### 6. Verify IBKR Connection Restored

**Expected in logs**:
- [ ] Log: "IBKR connection restored"
- [ ] Log: "Requesting position updates from IBKR"
- [ ] Log: "Position sync: [N] positions match"

**Verification**:
- [ ] Local positions match IBKR account
- [ ] Market data streaming resumes
- [ ] Greeks calculations resume

---

### 7. Verify Campaign Monitoring Resumes

**Action**: Wait for CampaignMonitorWorker cycle (e.g., 60 seconds)

**Expected in logs**:
- [ ] Log: "CampaignMonitorWorker: Monitoring campaign [id]"
- [ ] Log: "Calculating current P&L: [amount]"
- [ ] Log: "Evaluating exit rules: profit target, stop loss, max days"
- [ ] Log: "Exit rules: NOT MET (continuing monitoring)"

**Expected**:
- [ ] Monitoring resumes seamlessly
- [ ] P&L calculations accurate
- [ ] Exit rules evaluated normally

---

### 8. Verify Outbox Sync Resumes

**Expected in logs** (TradingSupervisorService):
- [ ] Log: "OutboxSyncWorker: Processing pending events"
- [ ] Log: "Syncing [N] events queued before restart"

**Verification Query**:
```sql
SELECT COUNT(*) FROM sync_outbox WHERE sync_status = 'pending';
-- Should decrease as events sync
```

**Expected**:
- [ ] Pending events synced
- [ ] No data loss
- [ ] Correct order preserved

---

### 9. Test Log Reader State Restoration

**Expected in logs** (TradingSupervisorService):
- [ ] Log: "LogReaderWorker: Resuming from last read position"
- [ ] Log: "Log file: options-[date].log, position: [byte offset]"

**Verification Query**:
```sql
SELECT file_path, last_position FROM log_reader_state;
```

**Expected**:
- [ ] Log reader resumes from last known position
- [ ] No duplicate log entries processed
- [ ] No log entries skipped

---

### 10. Test Force Kill (Hard Crash Simulation)

**Action**: Kill process with Task Manager or `kill -9`

**Warning**: This tests crash recovery. Database should be resilient.

**Expected after restart**:
- [ ] SQLite WAL recovery runs automatically
- [ ] No data corruption
- [ ] Services start normally
- [ ] All state restored from database

**Verification**:
```sql
PRAGMA integrity_check;
-- Should return: ok
```

---

## Success Criteria

- [ ] Graceful shutdown completes cleanly
- [ ] Restart completes without errors
- [ ] Active campaigns restored
- [ ] Positions restored (matching IBKR)
- [ ] Orders restored
- [ ] Campaign monitoring resumes
- [ ] IBKR connection restored
- [ ] Outbox sync resumes
- [ ] Log reader position restored
- [ ] No data loss or corruption
- [ ] Hard crash recovery works (WAL)

---

## Performance Benchmarks

- **Shutdown time**: < 5 seconds (graceful)
- **Restart time**: < 10 seconds (to ready state)
- **State restoration**: < 1 second (DB query)
- **IBKR reconnection**: < 5 seconds
- **Campaign resume latency**: < 1 monitoring cycle

---

## Cleanup

No cleanup required (test uses existing state)

---

## Troubleshooting

### Campaign Not Restored

- Check database: `SELECT * FROM campaigns WHERE status = 'active'`
- Verify no corruption: `PRAGMA integrity_check`
- Check logs for query errors

### Positions Mismatch with IBKR

- Wait for position sync (may take 30-60s)
- Check IBKR connection status
- Verify account ID matches

### Outbox Events Lost

- Check WAL checkpoint: `PRAGMA wal_checkpoint(FULL)`
- Verify sync_outbox table exists
- Check for disk space issues

---

**Test Duration**: 5-10 minutes  
**Last Updated**: 2026-04-05  
**Dependencies**: E2E-04 (Campaign Open) for full test
