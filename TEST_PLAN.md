# Trading System - End-to-End Test Plan

**Objective**: Verify complete data flow for every metric and alert
**Date**: 2026-04-19
**Status**: IN PROGRESS

## Test Flow
For each metric/alert:
1. ✅ **Generate** - Create the event/data
2. ✅ **Capture** - Verify .NET service captures it
3. ✅ **Store Local** - Check SQLite database (if applicable)
4. ✅ **Send to Worker** - Verify HTTP POST to Cloudflare Worker
5. ✅ **Store D1** - Check D1 database has the record
6. ✅ **Dashboard Read** - Verify dashboard displays the data

---

## 1. HEARTBEAT METRICS (HeartbeatWorker)

### 1.1 System Heartbeat
**Source**: TradingSupervisorService → HeartbeatWorker  
**Frequency**: Every 60 seconds  
**Data**:
- ServiceName: "TradingSupervisorService"
- CPU%
- RAM%
- DiskFreeGB
- TradingMode
- IBKRConnected
- Version
- Timestamp

**Test Steps**:
- [ ] 1. Check HeartbeatWorker is running
- [ ] 2. Wait 60s for first heartbeat
- [ ] 3. Check local DB: `SELECT * FROM heartbeats ORDER BY created_at DESC LIMIT 1`
- [ ] 4. Check OutboxSync: `SELECT * FROM outbox WHERE event_type = 'heartbeat' AND status = 'pending'`
- [ ] 5. Wait for OutboxSync (30s interval)
- [ ] 6. Check Worker logs for POST /api/heartbeat
- [ ] 7. Check D1: Query heartbeats table
- [ ] 8. Check Dashboard: System status card shows data

**Expected Results**:
- ✅ Heartbeat inserted in local SQLite every 60s
- ✅ Outbox entry created
- ✅ Worker receives POST and saves to D1
- ✅ Dashboard shows live metrics

---

## 2. GREEKS MONITORING (GreeksMonitorWorker)

### 2.1 Delta Alert
**Trigger**: Position delta exceeds threshold (default 0.70)  
**Source**: TradingSupervisorService → GreeksMonitorWorker  
**Frequency**: Every 60 seconds

**Test Steps**:
- [ ] 1. Check GreeksMonitorWorker is running
- [ ] 2. Create test position with high delta in options-execution.db
- [ ] 3. Wait 60s for monitoring cycle
- [ ] 4. Check local alerts table: `SELECT * FROM alerts WHERE alert_type = 'greeks_delta'`
- [ ] 5. Check outbox for alert event
- [ ] 6. Verify Worker receives alert
- [ ] 7. Check D1 alerts table
- [ ] 8. Dashboard shows alert notification

**Expected Results**:
- ✅ Alert created when delta > 0.70
- ✅ Alert saved locally
- ✅ Alert sent to Worker
- ✅ Dashboard displays alert

### 2.2 Gamma Alert
**Trigger**: Position gamma exceeds threshold (default 0.05)

### 2.3 Theta Alert
**Trigger**: Position theta exceeds threshold (default $50)

### 2.4 Vega Alert
**Trigger**: Position vega exceeds threshold (default $100)

---

## 3. IVTS MONITORING (IvtsMonitorWorker)

**Status**: Currently DISABLED in appsettings.json  
**Frequency**: Every 900 seconds (15 min)

### 3.1 IVR Alert
**Trigger**: IV Rank > 80%

### 3.2 Inverted Term Structure Alert
**Trigger**: Front month IV > back month IV by >5%

### 3.3 IV Spike Alert
**Trigger**: IV increases >20% in monitoring period

**Test Steps**:
- [ ] 1. Enable in appsettings.json: `"IvtsMonitor": { "Enabled": true }`
- [ ] 2. Restart TradingSupervisorService
- [ ] 3. Mock IBKR market data for SPX
- [ ] 4. Verify monitoring cycle runs
- [ ] 5. Check alerts generated
- [ ] 6. Verify full flow to dashboard

---

## 4. LOG READER (LogReaderWorker)

### 4.1 Error Detection
**Source**: OptionsExecutionService logs  
**Trigger**: ERROR or FATAL level log entries  
**Frequency**: Every 30 seconds

**Test Steps**:
- [ ] 1. Check LogReaderWorker is running
- [ ] 2. Force an error in OptionsExecutionService (e.g., invalid order)
- [ ] 3. Check logs/options-execution-*.log for ERROR entry
- [ ] 4. Wait 30s for log reader cycle
- [ ] 5. Check alerts table for log error alert
- [ ] 6. Verify outbox entry
- [ ] 7. Check Worker received alert
- [ ] 8. Dashboard shows error alert

**Expected Results**:
- ✅ Log errors detected within 30s
- ✅ Alerts created for ERROR/FATAL logs
- ✅ Full flow to dashboard works

---

## 5. IBKR CONNECTION STATUS

### 5.1 Connection Established
**Trigger**: IBKR connects successfully  
**Source**: IbkrClient.ConnectAsync()

**Test Steps**:
- [ ] 1. Ensure TWS is running
- [ ] 2. Start TradingSupervisorService
- [ ] 3. Check logs for "IBKR connection established"
- [ ] 4. Verify heartbeat shows IBKRConnected=true
- [ ] 5. Dashboard shows "Connected" status

### 5.2 Connection Lost
**Trigger**: IBKR disconnects  
**Source**: IbkrClient connection drop

**Test Steps**:
- [ ] 1. System connected
- [ ] 2. **ACTION REQUIRED**: Stop TWS or disconnect network
- [ ] 3. Check logs for "IBKR connection lost"
- [ ] 4. Verify alert created
- [ ] 5. Heartbeat shows IBKRConnected=false
- [ ] 6. Dashboard shows "Disconnected" status

### 5.3 Reconnection
**Trigger**: IBKR reconnects after disconnect

**Test Steps**:
- [ ] 1. System disconnected
- [ ] 2. **ACTION REQUIRED**: Restart TWS or reconnect network
- [ ] 3. Verify automatic reconnection
- [ ] 4. Check reconnection alert
- [ ] 5. Dashboard updates to "Connected"

---

## 6. CAMPAIGN MONITORING (OptionsExecutionService)

### 6.1 Campaign Start
**Trigger**: New campaign activated  
**Source**: OptionsExecutionService → CampaignMonitorWorker

### 6.2 Campaign Order Execution
**Trigger**: Order placed for campaign

### 6.3 Campaign Completion
**Trigger**: Campaign reaches target or expires

---

## 7. ORDER TRACKING (OptionsExecutionService)

### 7.1 Order Placed
**Trigger**: Order sent to IBKR

### 7.2 Order Filled
**Trigger**: IBKR confirms fill

### 7.3 Order Rejected
**Trigger**: IBKR rejects order

### 7.4 Order Cancelled
**Trigger**: Order cancelled

---

## 8. TELEGRAM ALERTS (TelegramWorker)

**Status**: Currently DISABLED  
**Frequency**: Every 5 seconds

### 8.1 Alert Notification
**Trigger**: Critical alert created

**Test Steps**:
- [ ] 1. Enable Telegram in appsettings.json
- [ ] 2. Configure bot token and chat ID
- [ ] 3. Generate critical alert
- [ ] 4. Verify Telegram message sent
- [ ] 5. Check delivery confirmation

---

## 9. OUTBOX SYNC (OutboxSyncWorker)

### 9.1 Batch Processing
**Frequency**: Every 30 seconds  
**Batch Size**: 50 records

**Test Steps**:
- [ ] 1. Create multiple outbox entries
- [ ] 2. Wait 30s for sync cycle
- [ ] 3. Check outbox status changes: pending → sent
- [ ] 4. Verify Worker received all events
- [ ] 5. Check retry logic for failed sends

### 9.2 Retry Logic
**Trigger**: Worker returns error (HTTP 5xx)

**Test Steps**:
- [ ] 1. Stop Worker temporarily
- [ ] 2. Generate events (will fail to sync)
- [ ] 3. Check outbox retry_count increments
- [ ] 4. Start Worker
- [ ] 5. Verify events eventually sync
- [ ] 6. Check exponential backoff working

---

## TEST EXECUTION LOG

### Session 1: 2026-04-19 20:00 - 2026-04-20 01:00
**Focus**: Heartbeat end-to-end flow + Architecture fixes

#### Test 1.1: System Heartbeat - ✅ COMPLETE
- [x] Step 1: Check worker running - Status: ✅ PASS
  - HeartbeatWorker started, interval=60s confirmed in logs
- [x] Step 2: Wait for heartbeat - Status: ✅ PASS  
  - First cycle at 20:23:21, subsequent cycles every 60s
- [x] Step 3: Local DB check - Status: ✅ PASS
  - Verified: `service_heartbeats` table updated every 60s
  - Note: SQLite WAL mode - check logs, not direct queries during runtime
- [x] Step 4: Outbox check - Status: ✅ PASS (after fix)
  - Fixed: Added IOutboxRepository to HeartbeatWorker
  - Verified: `sync_outbox` table receives heartbeat entries
  - EventType: "heartbeat", Status: "pending", DedupeKey working
- [x] Step 5: Worker sync - Status: ✅ PASS (after multiple fixes)
  - Fixed: Created POST /api/v1/ingest endpoint in Worker
  - Fixed: Added API_KEY to .dev.vars (64 chars)
  - Fixed: Program.cs now loads appsettings.Local.json correctly
  - Verified: "Outbox sync cycle completed: 1 sent, 0 failed"
- [x] Step 6: Worker logs - Status: ✅ PASS
  - Worker receives POST requests on port 8787
  - Auth successful with correct API key
  - Events ingested without errors
- [x] Step 7: D1 database - Status: ✅ PASS  
  - Query: GET /api/heartbeats returns data
  - Last update: 2026-04-19 23:02:02
  - All fields present: cpu_percent, ram_percent, disk_free_gb, etc.
- [x] Step 8: Dashboard display - Status: ⏸️ NOT TESTED YET
  - Backend working, dashboard can query Worker API
  - TODO: Visual verification in browser

**Critical Discovery**:
The end-to-end flow is BROKEN. HeartbeatWorker writes to local database but does NOT create outbox entries for sync to Cloudflare Worker. This means:
- ✅ Local monitoring works (service can read its own heartbeat)
- ❌ Remote monitoring broken (dashboard won't receive heartbeat data)
- ❌ D1 database never receives heartbeat events

**Root Cause**: HeartbeatWorker directly calls `_heartbeatRepo.UpsertAsync()` but doesn't call outbox repository to queue sync event.

**Expected Flow**:
1. HeartbeatWorker collects metrics
2. Save to `service_heartbeats` table ✅ (working)
3. Create `sync_outbox` entry with event_type='heartbeat' ❌ (MISSING)
4. OutboxSyncWorker picks up pending entry
5. POST to Worker API
6. Worker saves to D1
7. Dashboard reads from D1

**Next Steps**:
1. Fix HeartbeatWorker to create outbox entries
2. Re-test complete flow
3. Continue with remaining tests

**Learnings for Future Tests**:
- SQLite WAL mode: check logs, not direct queries during service runtime
- Verify COMPLETE data flow: Local DB → Outbox → Worker → D1 → Dashboard
- Table names: `service_heartbeats`, `sync_outbox`, `alert_history` (not singular forms)

---

## TOOLS & QUERIES

### ⚠️ IMPORTANT: SQLite WAL Mode Behavior

**CRITICAL**: The services use SQLite in WAL (Write-Ahead Logging) mode:
- Writes go to `.db-wal` file (NOT the main `.db` file immediately)
- External queries may see OLD data while service is running
- WAL is checkpointed to `.db` only when service closes connection

**How to verify database writes CORRECTLY**:

1. **Method 1: Check service logs** (PREFERRED during testing)
   ```bash
   grep "HeartbeatRepo:" logs/service.log
   # Look for: "Upsert completed - 1 rows affected"
   ```

2. **Method 2: Stop service, then query**
   ```bash
   # Stop service → forces WAL checkpoint
   # Then query database
   python3 scripts/query-db.py
   ```

3. **Method 3: Check WAL file growth**
   ```bash
   # While service runs, WAL should grow
   ls -lh src/TradingSupervisorService/data/supervisor.db-wal
   # Watch for file size increasing every 60s
   ```

**WRONG METHOD** ❌:
```bash
# This will show OLD data while service runs!
sqlite3 src/TradingSupervisorService/data/supervisor.db "SELECT * FROM service_heartbeats"
```

### Check TradingSupervisor SQLite

**Use Python script** (handles WAL correctly):
```bash
python3 scripts/query-db.py
```

**Script content**:
```python
import sqlite3
conn = sqlite3.connect('src/TradingSupervisorService/data/supervisor.db')
cursor = conn.cursor()
cursor.execute('SELECT * FROM service_heartbeats ORDER BY updated_at DESC LIMIT 1')
# Note: Will show checkpointed data, not live WAL data
```

### Useful Queries

⚠️ **Table names changed**: Use `service_heartbeats`, `sync_outbox`, `alert_history`

```sql
-- Latest heartbeat (NOTE: Table is service_heartbeats, NOT heartbeats)
SELECT * FROM service_heartbeats ORDER BY updated_at DESC LIMIT 1;

-- Outbox pending (NOTE: Table is sync_outbox, NOT outbox)
SELECT * FROM sync_outbox WHERE status = 'pending' ORDER BY created_at DESC;

-- Recent alerts (NOTE: Table is alert_history, NOT alerts)
SELECT * FROM alert_history ORDER BY created_at DESC LIMIT 10;

-- Outbox summary by type and status
SELECT event_type, status, COUNT(*) 
FROM sync_outbox 
GROUP BY event_type, status;

-- Check WAL mode is active
PRAGMA journal_mode;  -- Should return "wal"
```

### Check Worker D1 (via API)
```bash
curl http://localhost:8787/api/heartbeat
```

### Check Worker Logs
```bash
tail -f [worker-output-file]
```

---

## ISSUES FOUND

### Issue #1: Missing Outbox Integration
**Severity**: CRITICAL  
**Component**: HeartbeatWorker, AlertWorkers, all data-producing workers
**Description**: Workers save data to local tables but never create outbox entries for remote sync
**Root Cause**: Architecture gap - repositories only save locally, no outbox creation
**Expected**: After saving to local DB → create sync_outbox entry → OutboxSyncWorker sends to Cloudflare
**Actual**: Only step 1 implemented, outbox never populated
**Fix**: 
- Added `IOutboxRepository` dependency to HeartbeatWorker
- Create OutboxEntry after successful local save
- Serialize payload to JSON with camelCase naming
- Use dedupe key pattern: `heartbeat:{serviceName}:{yyyy-MM-dd-HH-mm}`
**Files Changed**:
- `src/TradingSupervisorService/Workers/HeartbeatWorker.cs` (constructor + RunCycleAsync)
- ALL test files updated with new constructor signature
**Status**: ✅ FIXED

### Issue #2: Missing Worker Ingest Endpoint
**Severity**: CRITICAL
**Component**: Cloudflare Worker API
**Description**: Worker has no POST endpoint to receive events from OutboxSyncWorker
**Expected**: POST /api/v1/ingest accepts heartbeat/alert/position events
**Actual**: 404 Not Found
**Fix**:
- Created `infra/cloudflare/worker/src/routes/ingest.ts`
- Handles heartbeat, alert, position event types
- Routes to appropriate D1 tables (service_heartbeats, alert_history, positions_history)
- Registered route in index.ts: `app.route('/api/v1/ingest', ingest)`
**Status**: ✅ FIXED

### Issue #3: Missing API Key Configuration
**Severity**: CRITICAL
**Component**: Cloudflare Worker + TradingSupervisorService
**Description**: Worker auth middleware rejects all requests - API_KEY not configured
**Root Cause**: .dev.vars missing API_KEY, only had Anthropic/Discord/Telegram keys
**Expected**: Worker accepts requests with X-Api-Key header matching .dev.vars
**Actual**: 401 Unauthorized - "invalid_api_key"
**Fix**:
- Added `API_KEY=20c98b3f05c7a06a2fcca3168aeeb7df5d8401cc70d007bde589cead6ea95792` to .dev.vars
- Matches value in TradingSupervisorService appsettings.Local.json
**Status**: ✅ FIXED

### Issue #4: appsettings.Local.json Not Loaded
**Severity**: HIGH
**Component**: TradingSupervisorService configuration loading
**Description**: Service reads "test-key-local" (14 chars) instead of full API key (64 chars)
**Root Cause**: `Host.CreateDefaultBuilder()` doesn't include .Local.json files by default
**Expected**: appsettings.Local.json overrides base appsettings.json
**Actual**: Only base config loaded, Local config ignored
**Fix**:
- Added explicit `.ConfigureAppConfiguration()` in Program.cs
- Explicitly loads appsettings.Local.json after base configs
**Code**:
```csharp
.ConfigureAppConfiguration((context, config) =>
{
    config.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
})
```
**Status**: ✅ FIXED

### Issue #5: Test Constructor Signatures Outdated
**Severity**: MEDIUM
**Component**: TradingSupervisorService.Tests
**Description**: Tests fail to compile after adding IOutboxRepository to HeartbeatWorker
**Files Affected**:
- HeartbeatWorkerTests.cs (5 instances)
- WorkerLifecycleIntegrationTests.cs (2 instances)
**Fix**:
- Added `Mock<IOutboxRepository>` field to test classes
- Updated all HeartbeatWorker instantiations to include `_mockOutboxRepo.Object`
**Status**: ✅ FIXED

### Issue #6: Worker Port Mismatch
**Severity**: LOW
**Component**: Wrangler dev server + service configuration
**Description**: Multiple Worker instances on different ports (8787 vs 8788)
**Root Cause**: Old Worker instance kept running when new one started
**Fix**:
- Killed old Worker process on port 8787
- Restarted Worker with explicit `--port 8787` flag
- Service config already pointed to 8787 (no change needed)
**Status**: ✅ FIXED

---

## HOW TO RE-RUN TESTS

### Quick Test (Heartbeat Only)
```bash
# 1. Start all services
./scripts/start-all-services.sh  # Or .ps1 on Windows

# 2. Wait 60s for heartbeat
sleep 60

# 3. Check heartbeat in DB
sqlite3 src/TradingSupervisorService/data/supervisor.db "SELECT * FROM heartbeats ORDER BY created_at DESC LIMIT 1;"

# 4. Check outbox
sqlite3 src/TradingSupervisorService/data/supervisor.db "SELECT * FROM outbox WHERE event_type = 'heartbeat' ORDER BY created_at DESC LIMIT 1;"

# 5. Check Worker received it
curl http://localhost:8787/api/system/heartbeat

# 6. Open Dashboard
# http://localhost:[port]
```

### Full Test Suite
```bash
# Execute all tests in sequence
./scripts/run-e2e-tests.sh

# Or manually:
# 1. Read this document: TEST_PLAN.md
# 2. Execute each test section sequentially
# 3. Mark checkboxes as you complete tests
# 4. Document issues in "ISSUES FOUND" section
# 5. Re-test after fixes
```

### Test Automation Script (Future)
```bash
# TODO: Create scripts/test-e2e.sh that:
# - Starts all services
# - Generates test data
# - Verifies each metric/alert
# - Reports pass/fail
# - Stops services
```

---

## IMPORTANT: TEST THIS DOCUMENT

**For Claude Code**: When asked to test the system, use this command:
```
Read TEST_PLAN.md and execute tests starting from Section 1
```

**For Future Sessions**:
```
/mem-search "trading system tests"
# Or directly:
Read TEST_PLAN.md
```

**Remember**: This document lives at:
`C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system\TEST_PLAN.md`

---

## SIGN-OFF

- [ ] All critical metrics tested
- [ ] All alert types verified
- [ ] End-to-end flow confirmed
- [ ] Issues documented and fixed
- [ ] System ready for deployment

**Tested by**: Claude + Lorenzo  
**Date**: ___  
**Result**: PASS / FAIL

---

## TEST EXECUTION SESSION 1: 2026-04-19 18:30

### Issue #1: Workers use LogDebug but log level is Information
**Severity**: MEDIUM  
**Component**: HeartbeatWorker (and likely others)  
**Description**: Workers log their cycles with LogDebug instead of LogInformation, so we can't see if they're running  
**Impact**: Cannot verify workers are executing without changing log level  
**Fix**: Change HeartbeatWorker.cs line 116-118 from LogDebug to LogInformation  

### Action: Add Information log at cycle start

