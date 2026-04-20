# Trading System - End-to-End Test Plan

**Version**: 2.0  
**Last Updated**: 2026-04-20  
**Status**: ACTIVE  
**Objective**: Verify complete data flow for every metric and alert

**Change Log**:
- v2.0 (2026-04-20): Added Sections 6, 7, 10; Expanded Section 8 (11 categories, 40+ tests); Added Reusability Framework
- v1.1 (2026-04-19): Session 1 results, 6 issues fixed, outbox integration completed
- v1.0 (2026-04-18): Initial version

---

## Table of Contents

1. [How to Use This Plan](#how-to-use-this-plan)
2. [Quick Test (5 Minutes)](#quick-test-5-minutes)
3. [Session Template](#session-template)
4. [Database Query Cheat Sheet](#database-query-cheat-sheet)
5. [Test Sections](#test-flow) (Sections 1-10)
6. [Final Checklist](#final-checklist)
7. [Troubleshooting Appendix](#appendix-troubleshooting)

---

## How to Use This Plan

### For First-Time Testers

1. **Read this section completely** (you are here!)
2. **Set up environment**:
   - Install dependencies: .NET 9, Node.js, Bun, SQLite
   - Start services: `.\scripts\start-all-services.ps1` (Windows) or `./scripts/start-all-services.sh` (Linux)
   - Verify IBKR TWS running (port 7497 for paper trading)
3. **Choose test mode**:
   - **Quick Test** (5 min): Heartbeat only → [Jump to Quick Test](#quick-test-5-minutes)
   - **Full Test** (2-4 hrs): All 10 sections → [Start with Section 1](#test-flow)
   - **Targeted Test** (Variable): Specific section after bug fix → [Section Index](#test-flow)
4. **Follow test procedures sequentially**: Each section has numbered checkboxes
5. **Document results**: Use [Session Template](#session-template) below
6. **Archive logs**: Save to `logs/test-session-YYYYMMDD.md` after completion

### For Returning Testers

1. **Check last session**: Read `logs/test-session-*.md` for last completed test
2. **Resume from next test**: Use checkboxes to track progress
3. **Re-run failed tests**: After fixing issues, re-execute specific test procedures
4. **Update session log**: Append new results to session file

### Test Modes

| Mode | Duration | Coverage | Use Case |
|------|----------|----------|----------|
| **Quick Test** | 5 min | Heartbeat only | Smoke test after deployment/restart |
| **Full Test** | 2-4 hrs | All 10 sections | Pre-production validation, weekly QA |
| **Targeted Test** | 10-30 min | Single section | Bug fix verification, feature testing |

---

## Quick Test (5 Minutes)

**Objective**: Verify core system health (heartbeat flow end-to-end)

**Steps**:
```bash
# 1. Start services (if not running)
.\scripts\start-all-services.ps1

# 2. Wait 60 seconds for heartbeat cycle
Start-Sleep -Seconds 60

# 3. Check local database (TradingSupervisorService)
sqlite3 src/TradingSupervisorService/data/supervisor.db \
  "SELECT service_name, cpu_percent, ram_percent, ibkr_connected, updated_at 
   FROM service_heartbeats ORDER BY updated_at DESC LIMIT 1;"
# Expected: 1 row with recent timestamp (< 60s old)

# 4. Check outbox sync
sqlite3 src/TradingSupervisorService/data/supervisor.db \
  "SELECT COUNT(*) FROM sync_outbox WHERE event_type='heartbeat' AND status='sent';"
# Expected: > 0 (at least 1 heartbeat synced)

# 5. Check Cloudflare Worker (D1 database via API)
curl http://localhost:8787/api/heartbeats | jq '.[-1]'
# Expected: JSON with service_name, cpu_percent, ram_percent, updated_at

# 6. Check Dashboard (optional - visual verification)
# Open: http://localhost:3000
# Navigate to: System Status page
# Verify: Heartbeat metrics displayed
```

**Success Criteria**:
- ✅ Heartbeat in local DB (updated within last 60s)
- ✅ Outbox entry synced (status='sent')
- ✅ Heartbeat in Cloudflare Worker D1 database
- ✅ (Optional) Dashboard displays metrics

**If ANY check fails**: See [Troubleshooting Appendix](#appendix-troubleshooting)

**Timeline**: Start services → Wait 60s → Run checks → Result: < 5 minutes total

---

## Session Template

**Copy this template for each test session**. Save to `logs/test-session-YYYYMMDD.md`.

```markdown
---

### Session {N}: YYYY-MM-DD HH:MM
**Tester**: {Your Name}
**Goal**: {Quick | Full | Targeted: Section X}
**Environment**: {Paper | Live (NOT RECOMMENDED) | Test}
**Services Version**: {Git commit hash or version number}
**Duration**: {Start time} - {End time}

#### Results Summary
- **Sections Tested**: {1, 2, 3, ...}
- **Tests Passed**: {X} / {Y}
- **Tests Failed**: {Z}
- **New Issues**: {N} (see Issues Found below)
- **Fixed Issues**: {M} (from previous sessions)

#### Test Execution

**Section 1: Heartbeat**
- [ ] 1.1 System Heartbeat - {PASS | FAIL | SKIP}
  - Notes: {Any observations}

**Section 2: Greeks Monitoring**
- [ ] 2.1 Delta Alert - {PASS | FAIL | SKIP}
- [ ] 2.2 Gamma Alert - {PASS | FAIL | SKIP}
- [ ] 2.3 Theta Alert - {PASS | FAIL | SKIP}
- [ ] 2.4 Vega Alert - {PASS | FAIL | SKIP}

{Continue for all tested sections...}

#### Issues Found

**Issue #{N}**:
- **Severity**: {CRITICAL | HIGH | MEDIUM | LOW}
- **Component**: {Service/Worker name}
- **Description**: {What went wrong}
- **Steps to Reproduce**: {1, 2, 3...}
- **Expected**: {What should happen}
- **Actual**: {What actually happened}
- **Fix**: {How it was resolved OR "PENDING"}
- **Status**: {FIXED | IN PROGRESS | DEFERRED}

{Repeat for each issue...}

#### Performance Metrics
- Heartbeat latency: {X} seconds (expected < 2s)
- Order placement latency: {X} seconds (expected < 5s)
- Campaign activation time: {X} seconds (expected < 10s)
- Dashboard load time: {X} seconds (expected < 3s)
- AI conversion time: {X} seconds (expected < 20s)

#### Sign-Off
- [ ] All planned tests executed
- [ ] Issues documented with severity
- [ ] Logs archived to `logs/test-session-YYYYMMDD/`
- [ ] Known issues list updated (if applicable)

**Final Result**: {PASS | FAIL}
**Notes**: {Any additional observations, recommendations}

---
```

---

## Database Query Cheat Sheet

### Supervisor Database (`supervisor.db`)

**Location**: `src/TradingSupervisorService/data/supervisor.db`

```sql
-- Latest heartbeat
SELECT service_name, cpu_percent, ram_percent, disk_free_gb, ibkr_connected, updated_at
FROM service_heartbeats 
ORDER BY updated_at DESC LIMIT 1;

-- Pending outbox entries (not yet synced)
SELECT event_type, COUNT(*) as count
FROM sync_outbox 
WHERE status='pending' 
GROUP BY event_type;

-- Recently synced outbox entries
SELECT event_type, status, created_at, sent_at
FROM sync_outbox 
WHERE status='sent' 
ORDER BY sent_at DESC LIMIT 10;

-- Outbox sync success rate (last 24 hours)
SELECT 
  status, 
  COUNT(*) as count,
  ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM sync_outbox WHERE created_at > datetime('now', '-24 hours')), 2) as percentage
FROM sync_outbox 
WHERE created_at > datetime('now', '-24 hours')
GROUP BY status;

-- Recent alerts (all types)
SELECT alert_type, severity, message, created_at
FROM alert_history 
ORDER BY created_at DESC LIMIT 10;

-- Alerts by type (last 24 hours)
SELECT alert_type, severity, COUNT(*) as count
FROM alert_history 
WHERE created_at > datetime('now', '-24 hours')
GROUP BY alert_type, severity;

-- Critical alerts (severity = 'critical')
SELECT alert_type, message, created_at
FROM alert_history 
WHERE severity='critical' 
ORDER BY created_at DESC LIMIT 20;
```

### Options Execution Database (`options-execution.db`)

**Location**: `src/OptionsExecutionService/data/options-execution.db`

```sql
-- Active campaigns
SELECT campaign_id, strategy_name, status, activated_at, 
       julianday('now') - julianday(activated_at) as days_active
FROM campaigns 
WHERE status='Active';

-- Campaign summary (all states)
SELECT status, COUNT(*) as count
FROM campaigns 
GROUP BY status;

-- Closed campaigns with P&L (last 7 days)
SELECT campaign_id, strategy_name, close_reason, realized_pnl, 
       activated_at, closed_at,
       julianday(closed_at) - julianday(activated_at) as days_held
FROM campaigns 
WHERE status='Closed' AND closed_at > datetime('now', '-7 days')
ORDER BY closed_at DESC;

-- Active positions for a campaign
SELECT position_id, symbol, contract_symbol, quantity, entry_price, current_price, unrealized_pnl
FROM positions 
WHERE campaign_id = '{campaign_id}';

-- Recent order history
SELECT order_id, campaign_id, symbol, contract_symbol, side, type, status, 
       quantity, filled_quantity, avg_fill_price, created_at
FROM order_tracking 
ORDER BY created_at DESC LIMIT 20;

-- Order success rate (last 24 hours)
SELECT 
  status, 
  COUNT(*) as count,
  ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM order_tracking WHERE created_at > datetime('now', '-24 hours')), 2) as percentage
FROM order_tracking 
WHERE created_at > datetime('now', '-24 hours')
GROUP BY status;

-- Failed orders (Rejected or Failed status)
SELECT order_id, symbol, status, validation_error, rejection_reason, created_at
FROM order_tracking 
WHERE status IN ('Rejected', 'Failed', 'ValidationFailed')
ORDER BY created_at DESC LIMIT 10;

-- Campaign positions P&L summary
SELECT campaign_id, 
       COUNT(*) as total_positions,
       SUM(unrealized_pnl) as total_unrealized_pnl
FROM positions
GROUP BY campaign_id;
```

### Cloudflare Worker (D1 via API)

**Base URL**: `http://localhost:8787` (dev) or `https://your-worker.workers.dev` (production)

```bash
# Latest heartbeats
curl http://localhost:8787/api/heartbeats | jq '.[-10:]'

# Recent alerts
curl http://localhost:8787/api/alerts | jq '.[-10:]'

# Position history (if synced)
curl http://localhost:8787/api/positions | jq '.[-10:]'

# EL conversion log
curl http://localhost:8787/api/admin/el-conversion-log | jq '.[-10:]'

# Conversion stats
curl http://localhost:8787/api/admin/el-conversion-stats | jq '.'
```

### Common PowerShell Helpers

```powershell
# Query with formatted output
function Query-SupervisorDB {
    param([string]$Query)
    sqlite3 src/TradingSupervisorService/data/supervisor.db -header -column $Query
}

# Example usage
Query-SupervisorDB "SELECT * FROM service_heartbeats ORDER BY updated_at DESC LIMIT 1"

# Check recent alerts
Query-SupervisorDB "SELECT alert_type, severity, message FROM alert_history ORDER BY created_at DESC LIMIT 5"
```

---

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

## 6. CAMPAIGN LIFECYCLE (OptionsExecutionService)

Campaign lifecycle follows 3 states: **Open** (waiting for entry) → **Active** (positions open) → **Closed** (final P&L).

### 6.1 Campaign Creation (Open State)
**Objective**: Verify campaign created correctly in Open state  
**Trigger**: Strategy creates new campaign via CampaignManager.CreateAsync()  
**Database Table**: `campaigns` in `options-execution.db`

**Test Steps**:
- [ ] 1. Create test campaign via API or direct DB insert:
  ```sql
  INSERT INTO campaigns (campaign_id, strategy_name, status, created_at, strategy_definition_json, updated_at)
  VALUES ('test-camp-001', 'IronCondor', 'Open', datetime('now'), '{"entry":"test"}', datetime('now'));
  ```
- [ ] 2. Verify database record created:
  ```sql
  SELECT campaign_id, strategy_name, status, activated_at, closed_at 
  FROM campaigns WHERE campaign_id = 'test-camp-001';
  ```
- [ ] 3. Check CampaignMonitorWorker logs for campaign detection
- [ ] 4. Verify state transitions NOT triggered (campaign waiting for entry conditions)

**Expected Results**:
- ✅ Campaign status = "Open"
- ✅ activated_at = NULL (not yet activated)
- ✅ closed_at = NULL
- ✅ strategy_definition_json populated with strategy config
- ✅ No positions created yet (positions table empty for this campaign_id)

**Verification Commands**:
```sql
-- Campaign state
SELECT campaign_id, status, activated_at, closed_at, created_at 
FROM campaigns WHERE campaign_id = 'test-camp-001';

-- Positions (should be empty)
SELECT COUNT(*) FROM positions WHERE campaign_id = 'test-camp-001';
-- Expected: 0
```

**Cleanup**:
```sql
DELETE FROM campaigns WHERE campaign_id = 'test-camp-001';
```

---

### 6.2 Campaign Activation (Open → Active Transition)
**Objective**: Verify campaign transitions to Active when entry conditions met  
**Trigger**: Entry signal met → Strategy calls CampaignManager.ActivateAsync()  
**Frequency**: Variable (depends on strategy entry conditions)

**Test Steps**:
- [ ] 1. Create campaign in Open state (from 6.1)
- [ ] 2. Trigger entry conditions:
  - Manual: Call API endpoint to activate campaign
  - Automated: Modify strategy entry conditions to trigger immediately
- [ ] 3. Wait for order placement (5-10 seconds in paper trading)
- [ ] 4. Verify campaign state updated:
  ```sql
  SELECT status, activated_at FROM campaigns WHERE campaign_id = 'test-camp-001';
  ```
- [ ] 5. Verify positions created for all strategy legs:
  ```sql
  SELECT position_id, symbol, contract_symbol, quantity, entry_price, opened_at
  FROM positions WHERE campaign_id = 'test-camp-001';
  ```
- [ ] 6. Verify execution log records:
  ```sql
  SELECT execution_id, order_id, side, quantity, fill_price, executed_at
  FROM execution_log WHERE campaign_id = 'test-camp-001';
  ```
- [ ] 7. Check order_tracking table (if orders tracked):
  ```sql
  SELECT order_id, status, filled_quantity, avg_fill_price
  FROM order_tracking WHERE campaign_id = 'test-camp-001';
  ```

**Expected Results**:
- ✅ Campaign status = "Active"
- ✅ activated_at populated with ISO8601 timestamp
- ✅ Positions table has records for each strategy leg (e.g., 4 positions for Iron Condor)
- ✅ Each position has entry_price > 0, opened_at timestamp
- ✅ Execution log shows all filled orders
- ✅ Order statuses = "Filled" (or "PartiallyFilled" if large orders)

**Common Issues**:
- **Campaign stays Open**: Entry orders rejected by IBKR → Check execution_log for errors → Verify IBKR connection
- **Partial activation**: Only some legs filled → Check order_tracking for rejected orders → May need to cancel campaign
- **Duplicate positions**: Strategy re-triggered activation → Check CampaignManager logic for idempotency

**Timeline**: Entry → Activation → All positions filled: typically < 30 seconds in paper trading

---

### 6.3 Profit Target Closure (Active → Closed)
**Objective**: Verify campaign closes when unrealized P&L exceeds profit target  
**Trigger**: CampaignMonitorWorker detects `unrealized_pnl > profit_target`  
**Frequency**: CampaignMonitor checks every 60 seconds (default)

**Test Steps**:
- [ ] 1. Create Active campaign (from 6.2) with positions
- [ ] 2. Set low profit target for testing:
  ```sql
  UPDATE campaigns SET strategy_definition_json = json_set(strategy_definition_json, '$.profitTarget', 10.0)
  WHERE campaign_id = 'test-camp-001';
  ```
- [ ] 3. Simulate price movement to trigger profit (or wait for natural movement)
- [ ] 4. Wait for CampaignMonitorWorker cycle (60s)
- [ ] 5. Verify campaign closed:
  ```sql
  SELECT status, closed_at, close_reason, realized_pnl 
  FROM campaigns WHERE campaign_id = 'test-camp-001';
  ```
- [ ] 6. Verify exit orders placed:
  ```sql
  SELECT order_id, side, status FROM order_tracking 
  WHERE campaign_id = 'test-camp-001' AND side = 'Sell';  -- Assuming long positions
  ```
- [ ] 7. Verify positions removed from active table:
  ```sql
  SELECT COUNT(*) FROM positions WHERE campaign_id = 'test-camp-001';
  -- Expected: 0 (moved to position_history)
  ```
- [ ] 8. Verify position_history records:
  ```sql
  SELECT position_id, status, exit_price, realized_pnl, closed_at
  FROM position_history WHERE campaign_id = 'test-camp-001';
  ```

**Expected Results**:
- ✅ Campaign status = "Closed"
- ✅ close_reason = "profit_target"
- ✅ realized_pnl > 0 (profit)
- ✅ closed_at populated
- ✅ All positions closed (exit orders filled)
- ✅ position_history has records with status="closed"

**Verification Query**:
```sql
-- Campaign closure summary
SELECT 
  campaign_id, 
  status, 
  close_reason, 
  realized_pnl, 
  activated_at, 
  closed_at,
  julianday(closed_at) - julianday(activated_at) as days_held
FROM campaigns WHERE campaign_id = 'test-camp-001';
```

---

### 6.4 Stop Loss Closure (Active → Closed)
**Objective**: Verify campaign closes when unrealized P&L falls below stop loss  
**Trigger**: CampaignMonitorWorker detects `unrealized_pnl < stop_loss`  
**Frequency**: CampaignMonitor checks every 60 seconds (default)

**Test Steps**:
- [ ] 1. Create Active campaign with positions
- [ ] 2. Set shallow stop loss for testing:
  ```sql
  UPDATE campaigns SET strategy_definition_json = json_set(strategy_definition_json, '$.stopLoss', -5.0)
  WHERE campaign_id = 'test-camp-002';
  ```
- [ ] 3. Simulate adverse price movement (or wait for natural movement)
- [ ] 4. Wait for CampaignMonitorWorker cycle (60s)
- [ ] 5. Verify campaign closed with stop loss reason:
  ```sql
  SELECT status, close_reason, realized_pnl FROM campaigns WHERE campaign_id = 'test-camp-002';
  ```
- [ ] 6. Verify exit orders executed
- [ ] 7. Check position_history for closed positions

**Expected Results**:
- ✅ Campaign status = "Closed"
- ✅ close_reason = "stop_loss"
- ✅ realized_pnl < 0 (loss contained to stop loss level)
- ✅ All positions closed

**Common Issues**:
- **Slippage**: realized_pnl slightly worse than stop_loss → Normal in fast markets
- **Exit order rejection**: IBKR rejects exit order → Campaign may stay Active → Check logs for retry logic

---

### 6.5 Time Exit Closure (Active → Closed)
**Objective**: Verify campaign closes at specified exit time  
**Trigger**: Current time >= ExitTimeOfDay from strategy definition  
**Frequency**: CampaignMonitor checks every 60 seconds

**Test Steps**:
- [ ] 1. Create Active campaign with positions
- [ ] 2. Set near-future exit time (e.g., 2 minutes from now):
  ```sql
  UPDATE campaigns SET strategy_definition_json = json_set(
    strategy_definition_json, 
    '$.exitTimeOfDay', 
    strftime('%H:%M', datetime('now', '+2 minutes'))
  ) WHERE campaign_id = 'test-camp-003';
  ```
- [ ] 3. Wait for time to pass (2+ minutes)
- [ ] 4. Verify campaign closed:
  ```sql
  SELECT status, close_reason, realized_pnl FROM campaigns WHERE campaign_id = 'test-camp-003';
  ```
- [ ] 5. Verify exit orders placed regardless of P&L

**Expected Results**:
- ✅ Campaign status = "Closed"
- ✅ close_reason = "time_exit"
- ✅ realized_pnl = whatever current P&L was (can be positive, negative, or zero)
- ✅ Closure happens within 60s of exit time (monitor cycle frequency)

**Timeline**: ExitTimeOfDay reached → Next monitor cycle → Exit orders placed → Campaign closed (< 90 seconds total)

---

### 6.6 Manual Closure (Active → Closed)
**Objective**: Verify campaign can be manually closed via API  
**Trigger**: User calls API endpoint to close campaign  
**Use Case**: Risk management override, strategy disabled, emergency closure

**Test Steps**:
- [ ] 1. Create Active campaign with positions
- [ ] 2. Call manual closure API (implementation depends on API design):
  ```bash
  # Example API call
  curl -X POST http://localhost:5001/api/campaigns/test-camp-004/close \
    -H "Content-Type: application/json" \
    -d '{"reason": "manual", "immediate": true}'
  ```
  OR update database directly:
  ```sql
  UPDATE campaigns SET status = 'Closed', closed_at = datetime('now'), close_reason = 'manual'
  WHERE campaign_id = 'test-camp-004';
  ```
- [ ] 3. Verify CampaignMonitor detects closure
- [ ] 4. Verify exit orders placed
- [ ] 5. Check campaign closed state:
  ```sql
  SELECT status, close_reason, realized_pnl FROM campaigns WHERE campaign_id = 'test-camp-004';
  ```

**Expected Results**:
- ✅ Campaign status = "Closed"
- ✅ close_reason = "manual"
- ✅ Exit orders placed immediately (no waiting for monitor cycle)
- ✅ Positions closed

**Common Use Cases**:
- Emergency closure due to news event
- Strategy bug detected, need to exit all positions
- Risk limit breached
- Manual override of automated strategy

---

### 6.7 Error Handling
**Objective**: Verify campaign handles errors gracefully without corrupting state  
**Scenarios**: Entry order rejection, exit order rejection, IBKR disconnect

#### 6.7a Entry Order Rejection
**Test Steps**:
- [ ] 1. Create campaign in Open state
- [ ] 2. Trigger activation with invalid contract (e.g., expired option, wrong symbol)
- [ ] 3. Verify campaign remains in Open state (not partially activated):
  ```sql
  SELECT status, activated_at FROM campaigns WHERE campaign_id = 'test-camp-005';
  -- Expected: status="Open", activated_at=NULL
  ```
- [ ] 4. Check execution_log for rejection details
- [ ] 5. Verify NO positions created (all-or-nothing entry)

**Expected Results**:
- ✅ Campaign stays Open (activation failed)
- ✅ No positions in positions table
- ✅ execution_log shows order rejections
- ✅ Error logged in service logs

#### 6.7b Exit Order Rejection
**Test Steps**:
- [ ] 1. Create Active campaign with positions
- [ ] 2. Trigger closure (any reason)
- [ ] 3. Simulate IBKR rejection (e.g., insufficient shares to cover short)
- [ ] 4. Verify retry logic:
  - Campaign stays Active until all positions closed
  - Exit orders re-submitted with exponential backoff
  - Error alerts sent to Telegram/dashboard

**Expected Results**:
- ✅ Campaign eventually closes (after retries succeed)
- ✅ Retry count logged in execution_log or order_tracking
- ✅ Error alerts generated

#### 6.7c IBKR Disconnect During Campaign
**Test Steps**:
- [ ] 1. Create Active campaign with positions
- [ ] 2. Stop TWS or disconnect network
- [ ] 3. Trigger closure event (profit target, stop loss, etc.)
- [ ] 4. Verify campaign state preserved:
  ```sql
  SELECT status, updated_at FROM campaigns WHERE campaign_id = 'test-camp-006';
  ```
- [ ] 5. Reconnect IBKR
- [ ] 6. Verify closure resumes automatically

**Expected Results**:
- ✅ Campaign state persisted correctly in SQLite
- ✅ No data loss during disconnect
- ✅ Exit orders placed after reconnection
- ✅ heartbeat shows IBKRConnected=false during disconnect

**Error Recovery Pattern**:
```
1. Detect IBKR disconnect → Set IBKRConnected=false
2. Pause all order submissions
3. Wait for reconnection (auto-retry every 30s)
4. On reconnect → Resume pending operations
5. Check campaign states → Re-submit any failed exit orders
```

---

### 6.8 Campaign Database Schema Reference

**campaigns Table**:
```sql
CREATE TABLE campaigns (
    campaign_id       TEXT PRIMARY KEY,       -- GUID
    strategy_name     TEXT NOT NULL,          -- Strategy identifier
    status            TEXT NOT NULL,          -- "Open", "Active", "Closed"
    created_at        TEXT NOT NULL,          -- ISO8601
    activated_at      TEXT,                   -- When positions opened
    closed_at         TEXT,                   -- When closed
    close_reason      TEXT,                   -- "profit_target", "stop_loss", "time_exit", "manual"
    realized_pnl      REAL,                   -- Final P&L (if Closed)
    strategy_definition_json TEXT NOT NULL,   -- Strategy config
    state_json        TEXT,                   -- Arbitrary strategy state
    updated_at        TEXT NOT NULL
);
```

**positions Table**:
```sql
CREATE TABLE positions (
    position_id       TEXT PRIMARY KEY,
    campaign_id       TEXT NOT NULL,
    symbol            TEXT NOT NULL,          -- "SPY", "SPX", etc.
    contract_symbol   TEXT NOT NULL,          -- OCC format
    contract_id       INTEGER NOT NULL,       -- IBKR ID
    strategy_name     TEXT NOT NULL,
    quantity          INTEGER NOT NULL,       -- Positive=long, negative=short
    entry_price       REAL NOT NULL,
    current_price     REAL,
    unrealized_pnl    REAL,
    stop_loss         REAL,
    take_profit       REAL,
    opened_at         TEXT NOT NULL,
    updated_at        TEXT NOT NULL,
    metadata_json     TEXT,
    FOREIGN KEY (campaign_id) REFERENCES campaigns(campaign_id)
);
```

**Common Queries**:
```sql
-- Active campaigns summary
SELECT campaign_id, strategy_name, status, 
       COUNT(position_id) as open_positions,
       SUM(unrealized_pnl) as total_pnl
FROM campaigns 
LEFT JOIN positions USING(campaign_id)
WHERE status = 'Active'
GROUP BY campaign_id;

-- Campaign performance (closed campaigns)
SELECT strategy_name, 
       COUNT(*) as total_campaigns,
       AVG(realized_pnl) as avg_pnl,
       SUM(realized_pnl) as total_pnl,
       SUM(CASE WHEN realized_pnl > 0 THEN 1 ELSE 0 END) as winners
FROM campaigns
WHERE status = 'Closed'
GROUP BY strategy_name;
```

---

## 7. ORDER LIFECYCLE & TRACKING (OptionsExecutionService)

Order lifecycle follows 9 statuses: **ValidationFailed** → **PendingSubmit** → **Submitted** → **Active** → **PartiallyFilled** / **Filled** / **Cancelled** / **Rejected** / **Failed**

### 7.1 Market Order Happy Path
**Objective**: Verify market order completes successfully from placement to fill  
**Trigger**: Strategy calls OrderPlacer.PlaceOrderAsync() with OrderType.Market  
**Timeline**: Typically < 10 seconds in paper trading

**Test Steps**:
- [ ] 1. Place market order via API or direct method call:
  ```bash
  curl -X POST http://localhost:5001/api/orders \
    -H "Content-Type: application/json" \
    -d '{
      "campaign_id": "test-camp-001",
      "symbol": "SPY",
      "contract_symbol": "SPY   260416C00500000",
      "side": "Buy",
      "type": "Market",
      "quantity": 2,
      "time_in_force": "DAY"
    }'
  ```
- [ ] 2. Verify order created with PendingSubmit status:
  ```sql
  SELECT order_id, status, created_at FROM order_tracking 
  WHERE campaign_id = 'test-camp-001' ORDER BY created_at DESC LIMIT 1;
  -- Expected: status="PendingSubmit"
  ```
- [ ] 3. Wait 2-3 seconds for IBKR submission
- [ ] 4. Verify status progression: Submitted → Active
  ```sql
  SELECT order_id, status, ibkr_order_id, submitted_at FROM order_tracking 
  WHERE campaign_id = 'test-camp-001' ORDER BY created_at DESC LIMIT 1;
  -- Expected: status="Active", ibkr_order_id populated
  ```
- [ ] 5. Wait for fill (typically 3-5 seconds in paper trading)
- [ ] 6. Verify final status = Filled:
  ```sql
  SELECT order_id, status, filled_quantity, avg_fill_price, completed_at 
  FROM order_tracking WHERE campaign_id = 'test-camp-001';
  ```
- [ ] 7. Verify execution log entry:
  ```sql
  SELECT execution_id, order_id, quantity, fill_price, commission, executed_at
  FROM execution_log WHERE campaign_id = 'test-camp-001';
  ```
- [ ] 8. Verify position created:
  ```sql
  SELECT position_id, quantity, entry_price FROM positions WHERE campaign_id = 'test-camp-001';
  ```

**Expected Results**:
- ✅ Order status transitions: PendingSubmit → Submitted → Active → Filled
- ✅ filled_quantity = requested quantity (2 contracts)
- ✅ avg_fill_price > 0
- ✅ execution_log has matching entry
- ✅ Position created with correct quantity and entry_price

**Status Timeline**:
```
T+0s:  PendingSubmit (order created locally)
T+2s:  Submitted (sent to IBKR)
T+3s:  Active (IBKR acknowledged)
T+7s:  Filled (execution confirmed)
```

---

### 7.2 Limit Order
**Objective**: Verify limit order placement and conditional fill  
**Trigger**: Strategy calls OrderPlacer.PlaceOrderAsync() with OrderType.Limit  
**Difference from Market**: Order may stay Active longer, only fills at limit price or better

**Test Steps**:
- [ ] 1. Get current market price for contract
- [ ] 2. Place limit order below/above market (for Buy/Sell):
  ```bash
  curl -X POST http://localhost:5001/api/orders \
    -H "Content-Type: application/json" \
    -d '{
      "campaign_id": "test-camp-002",
      "symbol": "SPY",
      "contract_symbol": "SPY   260416C00500000",
      "side": "Buy",
      "type": "Limit",
      "quantity": 1,
      "limit_price": 2.50,
      "time_in_force": "DAY"
    }'
  ```
- [ ] 3. Verify order reaches Active status (not immediately Filled)
- [ ] 4. Wait for market price to reach limit (or modify limit to current market)
- [ ] 5. Verify fill when price condition met

**Expected Results**:
- ✅ Order stays Active until limit price reached
- ✅ Fill price <= limit_price (for Buy) or >= limit_price (for Sell)
- ✅ Order may stay Active for extended period (normal behavior)

**Common Scenarios**:
- Limit never reached → Order stays Active until DAY end → Status: Cancelled (time expired)
- Partial fill → Status: PartiallyFilled → Remains Active for remaining quantity

---

### 7.3 Safety Check Failures
**Objective**: Verify all safety checks prevent invalid orders from reaching IBKR  
**Trigger**: Order violates safety rules defined in appsettings.json Safety section

#### 7.3a: Quantity Exceeds MaxPositionSize
**Test Steps**:
- [ ] 1. Check current MaxPositionSize setting:
  ```json
  // appsettings.json → Safety:MaxPositionSize (default: 10)
  ```
- [ ] 2. Place order with quantity > MaxPositionSize:
  ```bash
  curl -X POST http://localhost:5001/api/orders \
    -d '{"quantity": 15, ...}'  # Exceeds limit of 10
  ```
- [ ] 3. Verify order rejected before IBKR submission:
  ```sql
  SELECT order_id, status, validation_error FROM order_tracking 
  WHERE campaign_id = 'test-camp-003';
  -- Expected: status="ValidationFailed", validation_error contains "MaxPositionSize"
  ```
- [ ] 4. Verify NO ibkr_order_id assigned (order never sent to IBKR)

**Expected Results**:
- ✅ Status = "ValidationFailed"
- ✅ validation_error = "Quantity 15 exceeds MaxPositionSize 10"
- ✅ ibkr_order_id = NULL
- ✅ Order NOT visible in TWS

#### 7.3b: Insufficient Account Balance
**Test Steps**:
- [ ] 1. Query current account balance from IBKR
- [ ] 2. Calculate order cost: quantity × price × multiplier (100 for options)
- [ ] 3. Place order exceeding available cash
- [ ] 4. Verify rejection with balance error

**Expected Results**:
- ✅ Status = "ValidationFailed"
- ✅ validation_error contains "insufficient balance" or "margin"
- ✅ Account balance unchanged

**Note**: This check depends on AccountInfoProvider integration with IBKR.

#### 7.3c: Exceeds MaxPositionPctOfAccount
**Test Steps**:
- [ ] 1. Check MaxPositionPctOfAccount setting (default: 20%)
- [ ] 2. Place order costing > 20% of account value
- [ ] 3. Verify rejection:
  ```sql
  SELECT status, validation_error FROM order_tracking WHERE order_id = '{id}';
  -- Expected: validation_error contains "MaxPositionPctOfAccount"
  ```

**Expected Results**:
- ✅ Order rejected if cost > 20% of total account value
- ✅ Prevents over-concentration in single position

#### 7.3d: Circuit Breaker Open
**Test Steps**:
- [ ] 1. Check circuit breaker settings:
  ```json
  // appsettings.json → Safety
  {
    "CircuitBreakerFailureThreshold": 3,  // 3 failures
    "CircuitBreakerWindowMinutes": 60,    // within 60 minutes
    "CircuitBreakerResetMinutes": 120     // opens for 120 minutes
  }
  ```
- [ ] 2. Trigger 3 consecutive order failures (use invalid contracts or rejection scenarios)
- [ ] 3. Verify circuit breaker opens:
  ```bash
  curl http://localhost:5001/api/orders/circuit-breaker-status
  # Expected: {"open": true, "tripped_at": "2026-04-20T10:30:00Z"}
  ```
- [ ] 4. Attempt new order placement
- [ ] 5. Verify immediate rejection:
  ```sql
  SELECT status, validation_error FROM order_tracking WHERE order_id = '{id}';
  -- Expected: status="ValidationFailed", validation_error="Circuit breaker open"
  ```
- [ ] 6. Wait for reset period (or manually reset)
- [ ] 7. Verify orders allowed again

**Expected Results**:
- ✅ After 3 failures in 60 min → Circuit breaker opens
- ✅ All subsequent orders rejected for 120 minutes
- ✅ Protects against cascading failures
- ✅ Manual reset available via API: POST /api/orders/reset-circuit-breaker

**Circuit Breaker Query**:
```sql
-- Count recent failures
SELECT COUNT(*) as failure_count 
FROM order_tracking 
WHERE status IN ('Rejected', 'Failed') 
  AND created_at > datetime('now', '-60 minutes');
-- If count >= 3 → Circuit breaker should be open
```

---

### 7.4 IBKR Rejection
**Objective**: Verify order rejected by IBKR is handled correctly  
**Trigger**: Order submitted with invalid contract symbol, margin issues, etc.  
**Status Flow**: Submitted → Rejected

**Test Steps**:
- [ ] 1. Place order with invalid contract symbol:
  ```bash
  curl -X POST http://localhost:5001/api/orders \
    -d '{
      "contract_symbol": "INVALID_SYMBOL_999",  # Non-existent contract
      "quantity": 1,
      ...
    }'
  ```
- [ ] 2. Verify order passes local validation (status = PendingSubmit)
- [ ] 3. Verify submission to IBKR (status = Submitted)
- [ ] 4. Wait for IBKR rejection response (typically < 5 seconds)
- [ ] 5. Verify status = Rejected:
  ```sql
  SELECT order_id, status, rejection_reason, ibkr_order_id 
  FROM order_tracking WHERE order_id = '{id}';
  -- Expected: status="Rejected", rejection_reason from IBKR error message
  ```
- [ ] 6. Verify NO position created
- [ ] 7. Check logs for rejection details

**Expected Results**:
- ✅ Status = "Rejected"
- ✅ rejection_reason populated with IBKR error message (e.g., "Unknown contract")
- ✅ ibkr_order_id populated (order WAS submitted, but rejected)
- ✅ No position created
- ✅ Error logged with full IBKR response

**Common Rejection Reasons**:
- Invalid contract symbol / expired option
- Insufficient margin
- Trading permissions not enabled for contract type
- Market closed / outside trading hours
- Contract not tradeable (suspended, halted)

**Rejection Handling**:
```
1. Receive IBKR rejection → Update status to Rejected
2. Log rejection_reason from IBKR error message
3. Send alert to Telegram (if configured)
4. Increment circuit breaker failure count
5. DO NOT retry (rejection is permanent for this order)
```

---

### 7.5 Order Cancellation
**Objective**: Verify order can be cancelled before fill  
**Trigger**: User or system calls CancelOrderAsync()  
**Status Flow**: Active → Cancelled

**Test Steps**:
- [ ] 1. Place limit order with price far from market (stays Active):
  ```bash
  curl -X POST http://localhost:5001/api/orders \
    -d '{
      "type": "Limit",
      "limit_price": 0.01,  # Very low, won't fill
      "quantity": 1,
      ...
    }'
  ```
- [ ] 2. Verify order reaches Active status
- [ ] 3. Cancel order via API:
  ```bash
  curl -X DELETE http://localhost:5001/api/orders/{order_id}
  ```
- [ ] 4. Verify cancellation request sent to IBKR
- [ ] 5. Verify status updated:
  ```sql
  SELECT order_id, status, cancelled_at FROM order_tracking WHERE order_id = '{id}';
  -- Expected: status="Cancelled", cancelled_at populated
  ```
- [ ] 6. Verify NO position created
- [ ] 7. Verify order removed from IBKR TWS

**Expected Results**:
- ✅ Status = "Cancelled"
- ✅ cancelled_at timestamp populated
- ✅ No position created
- ✅ Order no longer visible in TWS

**Edge Cases**:
- **Cancel while filling**: Race condition → Order may become PartiallyFilled or Filled before cancellation processed
- **Cancel already filled**: Cancellation request ignored, status remains Filled
- **Cancel already cancelled**: Idempotent, no error

---

### 7.6 Partial Fill
**Objective**: Verify large orders handle partial fills correctly  
**Trigger**: Large order quantity, illiquid contract, or limit order  
**Status Flow**: Active → PartiallyFilled (may transition to Filled eventually)

**Test Steps**:
- [ ] 1. Place large market order (quantity > typical market depth):
  ```bash
  curl -X POST http://localhost:5001/api/orders \
    -d '{"quantity": 50, "type": "Market", ...}'  # Large for testing
  ```
- [ ] 2. Monitor order status for partial fills
- [ ] 3. Verify status = PartiallyFilled:
  ```sql
  SELECT order_id, status, quantity, filled_quantity, avg_fill_price 
  FROM order_tracking WHERE order_id = '{id}';
  -- Expected: status="PartiallyFilled", filled_quantity < quantity
  ```
- [ ] 4. Verify position created with filled quantity:
  ```sql
  SELECT position_id, quantity FROM positions WHERE order_id = '{id}';
  -- Expected: quantity = filled_quantity (not total requested quantity)
  ```
- [ ] 5. Verify execution log shows multiple fills:
  ```sql
  SELECT execution_id, quantity, fill_price, executed_at 
  FROM execution_log WHERE order_id = '{id}' ORDER BY executed_at;
  -- Expected: Multiple rows, SUM(quantity) = filled_quantity
  ```
- [ ] 6. Wait for remaining fills or cancellation

**Expected Results**:
- ✅ Status = "PartiallyFilled" while some quantity unfilled
- ✅ filled_quantity updated incrementally with each execution
- ✅ avg_fill_price calculated as weighted average of all fills
- ✅ Position quantity matches filled_quantity (not requested quantity)
- ✅ Order remains Active until fully filled or cancelled

**Average Fill Price Calculation**:
```
avg_fill_price = SUM(fill_price × fill_quantity) / SUM(fill_quantity)

Example:
  Fill 1: 10 contracts @ $2.50 = $25.00
  Fill 2: 15 contracts @ $2.55 = $38.25
  Total: 25 contracts, avg = $63.25 / 25 = $2.53
```

---

### 7.7 Connection Lost (Order in Flight)
**Objective**: Verify order state preserved when IBKR connection drops  
**Trigger**: Network disconnect, TWS crash, or intentional disconnect during order  
**Status Flow**: Variable (depends on when disconnect occurs)

**Test Steps**:
- [ ] 1. Place market order
- [ ] 2. Immediately stop TWS or disconnect network (before fill confirmation)
- [ ] 3. Check order status in database:
  ```sql
  SELECT order_id, status, ibkr_order_id FROM order_tracking WHERE order_id = '{id}';
  ```
- [ ] 4. Check service logs for disconnect detection:
  ```bash
  grep "IBKR connection lost" logs/options-execution-*.log
  ```
- [ ] 5. Restart TWS and reconnect
- [ ] 6. Verify order state reconciliation:
  - If order was filled before disconnect → Status updated to Filled
  - If order still active → Status remains Active, continues monitoring
  - If order failed to submit → Status updated to Failed

**Expected Results**:
- ✅ Order state persisted in SQLite (not lost during disconnect)
- ✅ Status = "Failed" if submission never reached IBKR
- ✅ On reconnect, service queries IBKR for order status
- ✅ Database synchronized with IBKR state after reconnect
- ✅ No duplicate orders created

**Reconnection Logic**:
```
1. Detect disconnect → Set IBKRConnected=false
2. Pause order submissions (queue new orders)
3. Auto-reconnect every 30 seconds
4. On reconnect → Query IBKR for all "in-flight" orders (status=Submitted/Active)
5. Reconcile database with IBKR reality:
   - IBKR says Filled → Update local DB to Filled
   - IBKR says Cancelled → Update local DB to Cancelled
   - IBKR doesn't know order → Update local DB to Failed
6. Resume order submissions
```

**Recovery Queries**:
```sql
-- Orders "in flight" when disconnect occurred
SELECT order_id, status, ibkr_order_id, submitted_at 
FROM order_tracking 
WHERE status IN ('Submitted', 'Active', 'PendingSubmit')
  AND created_at > datetime('now', '-1 hour');

-- Orders needing reconciliation
SELECT order_id, status, ibkr_order_id 
FROM order_tracking 
WHERE status IN ('Submitted', 'Active') 
  AND updated_at < datetime('now', '-5 minutes');
-- If updated_at is stale, order may be stuck → reconcile with IBKR
```

---

### 7.8 Order Tracking Schema Reference

**order_tracking Table** (if exists, may be in migrations):
```sql
CREATE TABLE order_tracking (
    order_id          TEXT PRIMARY KEY,       -- Local UUID
    campaign_id       TEXT NOT NULL,
    ibkr_order_id     INTEGER,                -- IBKR's order ID (after submission)
    symbol            TEXT NOT NULL,
    contract_symbol   TEXT NOT NULL,
    side              TEXT NOT NULL,          -- "Buy" or "Sell"
    type              TEXT NOT NULL,          -- "Market" or "Limit"
    quantity          INTEGER NOT NULL,       -- Requested quantity
    filled_quantity   INTEGER DEFAULT 0,      -- Filled so far
    limit_price       REAL,                   -- For limit orders
    avg_fill_price    REAL,                   -- Weighted average fill price
    time_in_force     TEXT NOT NULL,          -- "DAY", "GTC", etc.
    status            TEXT NOT NULL,          -- OrderStatus enum as string
    validation_error  TEXT,                   -- If ValidationFailed
    rejection_reason  TEXT,                   -- If Rejected by IBKR
    created_at        TEXT NOT NULL,
    submitted_at      TEXT,                   -- When sent to IBKR
    completed_at      TEXT,                   -- When reached terminal status
    cancelled_at      TEXT,                   -- If cancelled
    metadata_json     TEXT
);
```

**Common Queries**:
```sql
-- Recent orders by status
SELECT status, COUNT(*) as count, 
       AVG(julianday(completed_at) - julianday(created_at)) * 24 * 60 as avg_duration_minutes
FROM order_tracking 
WHERE created_at > datetime('now', '-24 hours')
GROUP BY status;

-- Orders stuck in non-terminal status (potential issues)
SELECT order_id, status, created_at, 
       julianday('now') - julianday(created_at) as hours_old
FROM order_tracking 
WHERE status IN ('PendingSubmit', 'Submitted', 'Active')
  AND created_at < datetime('now', '-1 hour');

-- Campaign order summary
SELECT campaign_id, 
       COUNT(*) as total_orders,
       SUM(CASE WHEN status='Filled' THEN 1 ELSE 0 END) as filled,
       SUM(CASE WHEN status='Rejected' THEN 1 ELSE 0 END) as rejected,
       SUM(filled_quantity) as total_contracts
FROM order_tracking
GROUP BY campaign_id;
```

**Order API Endpoints** (typical structure):
```bash
# Place order
POST /api/orders
Body: { campaign_id, symbol, contract_symbol, side, type, quantity, limit_price?, time_in_force }

# Get order status
GET /api/orders/{order_id}

# Cancel order
DELETE /api/orders/{order_id}

# Circuit breaker status
GET /api/orders/circuit-breaker-status

# Reset circuit breaker (admin only)
POST /api/orders/reset-circuit-breaker
```

---

## 8. TELEGRAM ALERTS (TelegramWorker)

**Status**: Currently DISABLED (Enabled: false in appsettings.json)  
**Frequency**: Every 5 seconds (ProcessIntervalSeconds)  
**Architecture**: Queue-based async delivery with retry logic and rate limiting

### Overview

TelegramAlerter provides two delivery modes:
1. **Async Queue** (`QueueAlertAsync`): Non-blocking, FIFO queue, processed by TelegramWorker
2. **Immediate Send** (`SendImmediateAsync`): Blocking, bypasses queue, for critical alerts

Configuration sections: `Telegram:*` settings in appsettings.json

---

### Category 1: Configuration

#### 8.1a: Telegram Disabled
**Objective**: Verify alerts logged locally when Telegram disabled  
**Configuration**: `Telegram:Enabled: false`

**Test Steps**:
- [ ] 1. Verify appsettings.json has `Telegram:Enabled: false`
- [ ] 2. Start TradingSupervisorService
- [ ] 3. Check logs for "Telegram alerting is disabled"
- [ ] 4. Trigger alert (e.g., Greeks threshold breach)
- [ ] 5. Verify alert logged to file:
  ```bash
  grep "Telegram disabled" logs/service.log
  # Expected: "Telegram disabled. Alert queued to logs only: [message]"
  ```
- [ ] 6. Verify NO Telegram message sent (check Telegram chat)

**Expected Results**:
- ✅ TelegramAlerter initializes with Enabled=false
- ✅ Alerts logged locally but NOT sent to Telegram
- ✅ No API calls to Telegram Bot API
- ✅ Worker runs but processes 0 alerts

---

#### 8.1b: Missing Bot Token
**Objective**: Verify graceful degradation when BotToken missing  
**Configuration**: `Telegram:Enabled: true`, `Telegram:BotToken: ""`

**Test Steps**:
- [ ] 1. Set Enabled=true, BotToken="" in appsettings.json
- [ ] 2. Start service
- [ ] 3. Check logs for validation error:
  ```bash
  grep "Telegram configuration invalid" logs/service.log
  # Expected: "Telegram:BotToken is required when Telegram:Enabled is true"
  ```
- [ ] 4. Verify Telegram auto-disabled (Enabled set to false internally)
- [ ] 5. Trigger alert → Verify NOT sent to Telegram

**Expected Results**:
- ✅ Validation fails on startup
- ✅ Service disables Telegram automatically (does NOT crash)
- ✅ Warning logged
- ✅ Alerts logged locally only

---

#### 8.1c: Invalid ChatId
**Objective**: Verify validation for ChatId=0  
**Configuration**: `Telegram:Enabled: true`, `Telegram:ChatId: 0`

**Test Steps**:
- [ ] 1. Set Enabled=true, valid BotToken, ChatId=0
- [ ] 2. Start service
- [ ] 3. Verify validation error logged:
  ```bash
  grep "Telegram:ChatId is required" logs/service.log
  ```
- [ ] 4. Verify Telegram auto-disabled

**Expected Results**:
- ✅ Validation fails for ChatId=0
- ✅ Service continues (does NOT crash)
- ✅ Alerts logged locally

---

#### 8.1d: Valid Configuration
**Objective**: Verify successful initialization with valid config  
**Configuration**: All Telegram:* settings valid

**Test Steps**:
- [ ] 1. Set valid config:
  ```json
  "Telegram": {
    "Enabled": true,
    "BotToken": "1234567890:ABCdefGHIjklMNOpqrsTUVwxyz",  # From @BotFather
    "ChatId": 123456789,  # Your user ID
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 5,
    "MaxMessagesPerMinute": 20,
    "ProcessIntervalSeconds": 5
  }
  ```
- [ ] 2. Start service
- [ ] 3. Verify successful initialization:
  ```bash
  grep "TelegramAlerter initialized" logs/service.log
  # Expected: "TelegramAlerter initialized. ChatId=123456789, MaxRetries=3"
  ```
- [ ] 4. Trigger test alert
- [ ] 5. Verify message received in Telegram chat within 10 seconds

**Expected Results**:
- ✅ TelegramBotClient created successfully
- ✅ No validation errors
- ✅ Worker processes alerts
- ✅ Messages delivered to configured ChatId

---

### Category 2: Queue Operations

#### 8.2a: QueueAlertAsync (Async Mode)
**Objective**: Verify async queue mode delivers alerts  
**Mode**: Non-blocking, FIFO queue

**Test Steps**:
- [ ] 1. Enable Telegram (valid config)
- [ ] 2. Queue alert programmatically:
  ```csharp
  var alert = new TelegramAlert
  {
      AlertId = Guid.NewGuid().ToString(),
      Message = "Test alert - async queue mode",
      Severity = "info",
      Timestamp = DateTime.UtcNow
  };
  await telegramAlerter.QueueAlertAsync(alert);
  ```
- [ ] 3. Verify alert queued (returns immediately):
  ```bash
  grep "Alert queued for Telegram" logs/service.log
  # Expected: "Alert queued for Telegram. QueueSize=1, AlertId={guid}"
  ```
- [ ] 4. Wait up to 10 seconds (ProcessIntervalSeconds=5s + send time)
- [ ] 5. Verify message received in Telegram

**Expected Results**:
- ✅ QueueAlertAsync returns immediately (does NOT block)
- ✅ Alert added to ConcurrentQueue
- ✅ TelegramWorker picks up alert on next cycle (5s interval)
- ✅ Message delivered successfully

**Timeline**: Queue → 0-5s wait → Worker cycle → Send → 2-3s total latency

---

#### 8.2b: SendImmediateAsync (Blocking Mode)
**Objective**: Verify immediate send mode bypasses queue  
**Mode**: Blocking, bypasses queue, waits for delivery confirmation

**Test Steps**:
- [ ] 1. Call SendImmediateAsync:
  ```csharp
  var alert = new TelegramAlert
  {
      Message = "CRITICAL: Immediate alert test",
      Severity = "critical"
  };
  bool success = await telegramAlerter.SendImmediateAsync(alert, CancellationToken.None);
  ```
- [ ] 2. Verify method blocks until sent (check execution time > 1s)
- [ ] 3. Verify return value = true (success)
- [ ] 4. Check Telegram for immediate delivery (< 3 seconds from call)

**Expected Results**:
- ✅ SendImmediateAsync blocks until delivery confirmed
- ✅ Does NOT use queue (no "Alert queued" log)
- ✅ Returns true on success, false on failure
- ✅ Latency < 3 seconds (no 5s worker delay)

**Use Cases**: Critical alerts requiring immediate notification (e.g., IBKR disconnect, account balance warning)

---

#### 8.2c: FIFO Queue Order
**Objective**: Verify alerts sent in First-In-First-Out order  
**Test**: Queue multiple alerts, verify delivery order

**Test Steps**:
- [ ] 1. Queue 5 alerts rapidly:
  ```csharp
  await QueueAlertAsync(new TelegramAlert { Message = "Alert 1" });
  await QueueAlertAsync(new TelegramAlert { Message = "Alert 2" });
  await QueueAlertAsync(new TelegramAlert { Message = "Alert 3" });
  await QueueAlertAsync(new TelegramAlert { Message = "Alert 4" });
  await QueueAlertAsync(new TelegramAlert { Message = "Alert 5" });
  ```
- [ ] 2. Wait for all to process (30 seconds max)
- [ ] 3. Verify Telegram message order: 1 → 2 → 3 → 4 → 5

**Expected Results**:
- ✅ Messages received in same order as queued
- ✅ ConcurrentQueue maintains FIFO semantics
- ✅ No message reordering

---

#### 8.2d: Queue Overflow Handling
**Objective**: Verify behavior when queue grows very large  
**Scenario**: Alert spam, many workers queuing simultaneously

**Test Steps**:
- [ ] 1. Disable Telegram temporarily (Enabled=false)
- [ ] 2. Generate 1000 alerts (will queue but not send)
- [ ] 3. Check queue size:
  ```csharp
  int pending = telegramAlerter.GetPendingCount();
  // Expected: 1000
  ```
- [ ] 4. Re-enable Telegram
- [ ] 5. Monitor processing (20 msg/min rate limit = 50 minutes for 1000 alerts)
- [ ] 6. Verify all 1000 eventually delivered

**Expected Results**:
- ✅ ConcurrentQueue handles large sizes (no size limit)
- ✅ Worker processes alerts at rate-limited pace (20/min)
- ✅ No alerts lost
- ✅ Memory usage reasonable (< 10MB for 1000 alerts)

**Warning**: In production, implement queue size limit or alert suppression to prevent unbounded growth

---

### Category 3: Message Formatting

#### 8.3a: Severity Emojis
**Objective**: Verify alerts formatted with severity-specific emojis  
**Implementation**: Message prefix based on alert severity

**Test Steps**:
- [ ] 1. Send alerts with each severity level:
  - info → ℹ️ prefix
  - warning → ⚠️ prefix
  - error → ❌ prefix
  - critical → 🚨 prefix
- [ ] 2. Verify Telegram messages have correct emoji prefix

**Expected Format**:
```
ℹ️ INFO: System heartbeat OK
⚠️ WARNING: Greeks delta 0.75 exceeds threshold 0.70
❌ ERROR: Log parsing failed for file X
🚨 CRITICAL: IBKR connection lost
```

---

#### 8.3b: Message Truncation
**Objective**: Verify long messages truncated to Telegram limit  
**Telegram Limit**: 4096 characters per message

**Test Steps**:
- [ ] 1. Generate alert with 5000-character message
- [ ] 2. Send via Telegram
- [ ] 3. Verify message truncated to 4096 chars
- [ ] 4. Verify truncation indicator added: `... [truncated]`

**Expected Results**:
- ✅ Message length <= 4096 characters
- ✅ Truncation graceful (no mid-word cuts)
- ✅ Indicator shows message was truncated

---

#### 8.3c: Special Character Escaping
**Objective**: Verify Markdown special characters escaped correctly  
**Telegram ParseMode**: Markdown (if used) or HTML

**Test Steps**:
- [ ] 1. Send alert with special chars: `*`, `_`, `[`, `]`, `(`, `)`, `~`, `` ` ``, `>`, `#`, `+`, `-`, `=`, `|`, `{`, `}`, `.`, `!`
- [ ] 2. Verify message displays correctly (chars not interpreted as Markdown)

**Example**:
```
Message: "Position SPX_230421C04500 has delta 0.85 > threshold"
Expected: Displays exactly as written (underscores NOT italic)
```

---

#### 8.3d: Multiline Message Support
**Objective**: Verify alerts with newlines formatted correctly

**Test Steps**:
- [ ] 1. Send alert with multiline message:
  ```
  CAMPAIGN CLOSED
  Campaign: iron-condor-001
  Reason: profit_target
  P&L: $125.50
  Duration: 2.5 hours
  ```
- [ ] 2. Verify Telegram preserves line breaks

**Expected**: Message displayed with all line breaks intact

---

### Category 4: Rate Limiting

#### 8.4a: Within Rate Limit
**Objective**: Verify normal operation within rate limit  
**Limit**: 20 messages per minute (MaxMessagesPerMinute)

**Test Steps**:
- [ ] 1. Queue 15 alerts (below limit)
- [ ] 2. Verify all sent within 60 seconds
- [ ] 3. Check no rate limit warnings in logs

**Expected Results**:
- ✅ All 15 alerts delivered
- ✅ No delays beyond ProcessIntervalSeconds (5s)
- ✅ Average throughput: 15 alerts in ~60 seconds

---

#### 8.4b: Exceeding Rate Limit
**Objective**: Verify rate limiting delays messages when limit hit  
**Scenario**: 30 alerts queued (exceeds 20/min limit)

**Test Steps**:
- [ ] 1. Queue 30 alerts rapidly
- [ ] 2. Monitor delivery timestamps in Telegram
- [ ] 3. Verify first 20 sent within 1 minute
- [ ] 4. Verify next 10 sent in following minute (rate limit applied)
- [ ] 5. Check logs for rate limit wait messages

**Expected Results**:
- ✅ First 20 alerts sent in minute 1
- ✅ Remaining 10 alerts sent in minute 2
- ✅ Rate limiting prevents Telegram API errors
- ✅ Log shows "Rate limit reached, waiting..."

**Rate Limit Logic**:
```csharp
// TelegramAlerter tracks last 60s of message timestamps
// Before sending: check if count in last 60s < MaxMessagesPerMinute
// If at limit: wait until oldest timestamp > 60s old
```

---

#### 8.4c: Bypass Rate Limit for Critical Alerts
**Objective**: Verify SendImmediateAsync respects but doesn't bypass rate limit  
**Note**: Current implementation respects rate limit for all alerts (no bypass)

**Test Steps**:
- [ ] 1. Saturate rate limit (send 20 alerts in 1 minute)
- [ ] 2. Call SendImmediateAsync with critical alert
- [ ] 3. Verify alert waits for rate limit window (does NOT bypass)

**Expected Results**:
- ✅ Immediate send still respects rate limit
- ✅ Alert waits until rate limit window allows
- ✅ Does NOT bypass rate limit (prevents Telegram API ban)

**Future Enhancement**: Add BypassRateLimit flag for true emergencies

---

### Category 5: Retry Logic

#### 8.5a: Transient Failure Retry
**Objective**: Verify retries on transient network errors  
**Scenario**: Temporary network issue, Telegram API timeout

**Test Steps**:
- [ ] 1. Simulate network issue (disconnect briefly or use mock HTTP client)
- [ ] 2. Queue alert
- [ ] 3. Verify first send fails
- [ ] 4. Verify automatic retry after 5 seconds (RetryDelaySeconds)
- [ ] 5. Restore network
- [ ] 6. Verify alert eventually delivered (within 3 retries)

**Expected Results**:
- ✅ First attempt fails with network error
- ✅ Retry #1 after 5 seconds
- ✅ Retry #2 after 10 seconds (exponential backoff: 5 × 2)
- ✅ Retry #3 after 20 seconds (exponential backoff: 10 × 2)
- ✅ Alert delivered on successful retry

**Retry Delay Formula**:
```
Attempt 1: RetryDelaySeconds × 1 = 5s
Attempt 2: RetryDelaySeconds × 2 = 10s
Attempt 3: RetryDelaySeconds × 4 = 20s
```

---

#### 8.5b: Permanent Failure (Max Retries)
**Objective**: Verify alert dropped after MaxRetryAttempts  
**Scenario**: Invalid BotToken, ChatId blocked bot, etc.

**Test Steps**:
- [ ] 1. Configure invalid BotToken (causes permanent API error)
- [ ] 2. Queue alert
- [ ] 3. Monitor retry attempts (should be 3: MaxRetryAttempts)
- [ ] 4. Verify alert dropped after 3 failures:
  ```bash
  grep "Alert dropped after" logs/service.log
  # Expected: "Alert dropped after 3 retry attempts: {alertId}"
  ```
- [ ] 5. Verify alert NOT delivered to Telegram

**Expected Results**:
- ✅ 3 retry attempts made
- ✅ Alert dropped (removed from queue)
- ✅ Error logged with retry count
- ✅ Worker continues (does NOT crash)

**Failure Scenarios**:
- Invalid BotToken → 401 Unauthorized
- Bot blocked by user → 403 Forbidden
- ChatId doesn't exist → 400 Bad Request
- Telegram API down → 503 Service Unavailable (should retry)

---

#### 8.5c: Timeout Handling
**Objective**: Verify timeout on slow Telegram API response  
**Timeout**: Default HTTP client timeout (typically 100 seconds)

**Test Steps**:
- [ ] 1. Simulate slow API (mock HTTP client with 120s delay)
- [ ] 2. Send alert
- [ ] 3. Verify timeout after ~100 seconds
- [ ] 4. Verify retry triggered (treat timeout as transient failure)

**Expected Results**:
- ✅ Request times out after 100 seconds
- ✅ Logged as transient failure
- ✅ Retry logic kicks in

---

#### 8.5d: Exponential Backoff Verification
**Objective**: Verify retry delays increase exponentially  
**Formula**: `delay = RetryDelaySeconds × 2^(attempt - 1)`

**Test Steps**:
- [ ] 1. Force failures (invalid config)
- [ ] 2. Measure time between retry attempts:
  ```bash
  grep "Retry attempt" logs/service.log
  # Expected timestamps:
  # T+0s:  Attempt 1 fails
  # T+5s:  Retry attempt 1
  # T+15s: Retry attempt 2 (5s + 10s)
  # T+35s: Retry attempt 3 (15s + 20s)
  ```
- [ ] 3. Verify exponential growth: 5s, 10s, 20s

**Expected Results**:
- ✅ Delay doubles on each retry
- ✅ Total retry time: ~35 seconds for 3 attempts
- ✅ Prevents API hammering on persistent failures

---

### Category 6: Worker Lifecycle

#### 8.6a: Worker Start
**Objective**: Verify TelegramWorker starts correctly

**Test Steps**:
- [ ] 1. Start TradingSupervisorService
- [ ] 2. Check logs for worker startup:
  ```bash
  grep "TelegramWorker started" logs/service.log
  # Expected: "TelegramWorker started. ProcessInterval=5s"
  ```
- [ ] 3. Verify 2-second startup delay (allows other services to initialize)
- [ ] 4. Queue alert immediately after start
- [ ] 5. Verify processed within 7 seconds (2s delay + 5s interval)

**Expected Results**:
- ✅ Worker starts successfully
- ✅ 2-second delay before first cycle
- ✅ Begins processing after delay

---

#### 8.6b: Graceful Shutdown
**Objective**: Verify worker processes remaining alerts on shutdown

**Test Steps**:
- [ ] 1. Queue 10 alerts
- [ ] 2. Stop service immediately (Ctrl+C or systemctl stop)
- [ ] 3. Check logs for graceful shutdown:
  ```bash
  grep "shutting down. Processing remaining alerts" logs/service.log
  ```
- [ ] 4. Verify all 10 alerts sent before service exits
- [ ] 5. Check Telegram for all 10 messages

**Expected Results**:
- ✅ Shutdown detected (OperationCanceledException)
- ✅ Worker runs final cycle with CancellationToken.None
- ✅ All queued alerts processed before exit
- ✅ No alerts lost

**Shutdown Logic**:
```csharp
catch (OperationCanceledException)
{
    _logger.LogInformation("TelegramWorker shutting down. Processing remaining alerts...");
    await RunCycleAsync(CancellationToken.None);  // Final cycle, no timeout
    break;
}
```

---

#### 8.6c: Crash Recovery
**Objective**: Verify worker survives errors and continues processing

**Test Steps**:
- [ ] 1. Queue alert that causes exception (e.g., null reference, invalid data)
- [ ] 2. Verify error logged:
  ```bash
  grep "TelegramWorker cycle failed" logs/service.log
  ```
- [ ] 3. Queue another valid alert
- [ ] 4. Verify worker continues processing (didn't crash)
- [ ] 5. Verify valid alert delivered successfully

**Expected Results**:
- ✅ Exception caught and logged
- ✅ Worker does NOT crash (exception not rethrown)
- ✅ Next cycle runs normally
- ✅ Subsequent alerts processed

**Error Handling**:
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "TelegramWorker cycle failed. Retry in {Interval}s", _processIntervalSeconds);
    // Do NOT rethrow - worker must survive errors
}
```

---

#### 8.6d: Process Interval Tuning
**Objective**: Verify ProcessIntervalSeconds configurable

**Test Steps**:
- [ ] 1. Set ProcessIntervalSeconds=10 (slower processing)
- [ ] 2. Restart service
- [ ] 3. Queue alert
- [ ] 4. Measure time until sent: should be 10-12 seconds (10s interval + send time)
- [ ] 5. Change to ProcessIntervalSeconds=1 (faster processing)
- [ ] 6. Restart and re-test: should be 1-3 seconds

**Expected Results**:
- ✅ Worker respects configured interval
- ✅ Higher interval = lower CPU, slower alerts
- ✅ Lower interval = faster alerts, higher CPU

**Tuning Guide**:
- 1s: Real-time alerts, higher CPU
- 5s: Default, good balance
- 10s: Low-priority alerts, minimal CPU

---

### Category 7: Integration with Other Workers

#### 8.7a: Heartbeat Alert Integration
**Objective**: Verify HeartbeatWorker can trigger Telegram alerts

**Test Steps**:
- [ ] 1. Configure HeartbeatWorker to send alerts on threshold breach (if implemented)
- [ ] 2. Trigger threshold (e.g., CPU > 80%)
- [ ] 3. Verify HeartbeatWorker creates alert
- [ ] 4. Verify TelegramWorker picks up and sends alert
- [ ] 5. Check Telegram for message

**Expected Flow**:
```
HeartbeatWorker → Detects high CPU
  ↓
Creates AlertRecord in alert_history table
  ↓
(Optional) Calls TelegramAlerter.QueueAlertAsync()
  ↓
TelegramWorker processes queue
  ↓
Message delivered to Telegram
```

---

#### 8.7b: Log Error Alert Integration
**Objective**: Verify LogReaderWorker triggers Telegram alerts on log errors

**Test Steps**:
- [ ] 1. Append ERROR line to monitored log file:
  ```bash
  echo "[2026-04-20 12:00:00 ERR] Test error for Telegram integration" >> logs/options-execution-*.log
  ```
- [ ] 2. Wait 30 seconds (LogReader interval)
- [ ] 3. Verify LogReaderWorker detects error
- [ ] 4. Verify Telegram alert sent
- [ ] 5. Check Telegram message contains log line

**Expected Message**:
```
❌ ERROR: Log error detected
File: options-execution-20260420.log
Message: Test error for Telegram integration
```

---

#### 8.7c: Campaign Alert Integration
**Objective**: Verify CampaignMonitorWorker sends Telegram alerts

**Test Steps**:
- [ ] 1. Create active campaign with positions
- [ ] 2. Trigger closure (profit target, stop loss, etc.)
- [ ] 3. Verify Telegram alert sent with campaign summary
- [ ] 4. Check message contains: campaign_id, close_reason, realized_pnl

**Expected Message**:
```
💰 Campaign Closed
ID: iron-condor-001
Reason: profit_target
P&L: $125.50
Duration: 2.5 hours
```

---

#### 8.7d: Order Rejection Alert Integration
**Objective**: Verify OrderPlacer sends Telegram alert on IBKR rejection

**Test Steps**:
- [ ] 1. Place order with invalid contract (force rejection)
- [ ] 2. Verify OrderPlacer detects rejection
- [ ] 3. Verify Telegram alert sent
- [ ] 4. Check message contains: order_id, symbol, rejection_reason

**Expected Message**:
```
❌ Order Rejected
Order: ord-12345
Symbol: SPX
Reason: Unknown contract
```

---

### Category 8: Configuration Edge Cases

#### 8.8a: MaxRetryAttempts Boundary Values
**Test Values**: 0, 1, 3 (default), 10 (max)

**Test Steps for each value**:
- [ ] 1. Set MaxRetryAttempts={value}
- [ ] 2. Force failures (invalid config)
- [ ] 3. Verify retry count matches configured value
- [ ] 4. Verify alert dropped after exact retry count

**Expected Results**:
- 0 retries: Alert dropped immediately after first failure
- 1 retry: 1 attempt, then dropped
- 3 retries: Default behavior
- 10 retries: Maximum allowed

**Invalid Values**: < 0 or > 10 → Validation fails, Telegram disabled

---

#### 8.8b: RetryDelaySeconds Boundary Values
**Test Values**: 1 (min), 5 (default), 60, 300 (max)

**Test Steps**:
- [ ] 1. Set RetryDelaySeconds={value}
- [ ] 2. Force failure
- [ ] 3. Measure time between retry attempts
- [ ] 4. Verify exponential backoff: value × 1, value × 2, value × 4

**Expected Retry Timings**:
- RetryDelaySeconds=1: 1s, 2s, 4s
- RetryDelaySeconds=5: 5s, 10s, 20s
- RetryDelaySeconds=60: 60s, 120s, 240s

---

#### 8.8c: MaxMessagesPerMinute Boundary Values
**Test Values**: 1 (very slow), 20 (default), 30 (Telegram limit)

**Test Steps**:
- [ ] 1. Set MaxMessagesPerMinute={value}
- [ ] 2. Queue 50 alerts
- [ ] 3. Measure delivery rate
- [ ] 4. Verify rate does NOT exceed configured limit

**Expected Delivery Times**:
- 1 msg/min: 50 alerts in 50 minutes
- 20 msg/min: 50 alerts in 2.5 minutes
- 30 msg/min: 50 alerts in ~1.7 minutes

**Warning**: Setting > 30 may trigger Telegram API rate limit errors (30 msg/sec globally)

---

#### 8.8d: ProcessIntervalSeconds Boundary Values
**Test Values**: 1 (real-time), 5 (default), 60 (low-priority)

**Test Steps**:
- [ ] 1. Set ProcessIntervalSeconds={value}
- [ ] 2. Queue alert
- [ ] 3. Measure latency (time from queue to delivery)

**Expected Latencies**:
- 1s interval: 1-3 seconds
- 5s interval: 5-7 seconds
- 60s interval: 60-62 seconds

---

### Category 9: Error Scenarios

#### 8.9a: Network Failure During Send
**Scenario**: Network disconnects while sending message

**Test Steps**:
- [ ] 1. Queue alert
- [ ] 2. Disconnect network during send (timing critical)
- [ ] 3. Verify exception caught:
  ```bash
  grep "HttpRequestException\|SocketException" logs/service.log
  ```
- [ ] 4. Reconnect network
- [ ] 5. Verify retry succeeds

---

#### 8.9b: Telegram API Error (429 Too Many Requests)
**Scenario**: Exceed Telegram global rate limit (rare)

**Test Steps**:
- [ ] 1. Send alerts very rapidly (> 30/second from multiple sources)
- [ ] 2. Verify 429 error received
- [ ] 3. Check logs for rate limit error
- [ ] 4. Verify automatic retry after backoff

---

#### 8.9c: Bot Blocked by User
**Scenario**: User blocks bot in Telegram

**Test Steps**:
- [ ] 1. Block bot in Telegram settings
- [ ] 2. Queue alert
- [ ] 3. Verify 403 Forbidden error
- [ ] 4. Verify alert dropped after retries (permanent failure)
- [ ] 5. Unblock bot and re-test

---

#### 8.9d: Invalid Message Content
**Scenario**: Message contains characters Telegram API rejects

**Test Steps**:
- [ ] 1. Send alert with problematic content (e.g., control characters, very long URLs)
- [ ] 2. Verify send fails with 400 Bad Request
- [ ] 3. Verify error logged with message preview

---

### Category 10: Concurrent Operations

#### 8.10a: Thread Safety - Concurrent Queueing
**Scenario**: Multiple workers queue alerts simultaneously

**Test Steps**:
- [ ] 1. Start 5 threads
- [ ] 2. Each thread queues 100 alerts (500 total)
- [ ] 3. Verify all 500 queued successfully (no race conditions)
- [ ] 4. Verify all 500 eventually delivered
- [ ] 5. Check for duplicate deliveries (should be 0)

**Expected Results**:
- ✅ ConcurrentQueue is thread-safe
- ✅ No alerts lost
- ✅ No duplicates
- ✅ All 500 delivered

---

#### 8.10b: Concurrent Processing
**Scenario**: TelegramWorker processes queue while new alerts added

**Test Steps**:
- [ ] 1. Queue 50 alerts
- [ ] 2. While processing, queue 50 more alerts concurrently
- [ ] 3. Verify all 100 delivered
- [ ] 4. Verify FIFO order maintained

---

### Category 11: Production Scenarios

#### 8.11a: Rate Limit Hit in Production
**Scenario**: Many alerts generated during market event

**Test Steps**:
- [ ] 1. Simulate high-volume alert scenario (100+ alerts in 1 minute)
- [ ] 2. Monitor rate limiting behavior
- [ ] 3. Verify alerts queued (not dropped)
- [ ] 4. Verify gradual delivery over time (respecting 20/min limit)
- [ ] 5. Check queue size over time (should decrease steadily)

**Expected Behavior**:
- First minute: 20 alerts sent
- Subsequent minutes: 20 alerts/min until queue empty
- Total time for 100 alerts: ~5 minutes

---

#### 8.11b: Bot Token Revoked (Security Incident)
**Scenario**: BotToken compromised and regenerated

**Test Steps**:
- [ ] 1. Generate new BotToken via @BotFather
- [ ] 2. Update appsettings.json
- [ ] 3. Restart service (or implement hot reload)
- [ ] 4. Verify alerts resume sending with new token

---

#### 8.11c: Chat Migrated (Group → Supergroup)
**Scenario**: Telegram group upgraded to supergroup (ChatId changes)

**Test Steps**:
- [ ] 1. Migrate group to supergroup
- [ ] 2. Update ChatId in config
- [ ] 3. Restart service
- [ ] 4. Verify alerts sent to new ChatId

---

#### 8.11d: Long-Running Service (Memory Leak Check)
**Scenario**: Service runs for 7+ days

**Test Steps**:
- [ ] 1. Run service for 1 week with periodic alerts
- [ ] 2. Monitor memory usage daily
- [ ] 3. Verify no memory growth (ConcurrentQueue properly dequeued)
- [ ] 4. Check _messageTimestamps queue pruned (old timestamps removed)

**Expected Results**:
- ✅ Memory stable (< 100MB for TelegramAlerter)
- ✅ No memory leaks
- ✅ Rate limit queue pruned (only last 60s of timestamps kept)

---

### Configuration Reference

**appsettings.json - Telegram Section**:
```json
"Telegram": {
  "Enabled": false,                    // Master switch
  "BotToken": "",                      // From @BotFather
  "ChatId": 0,                         // User ID or group chat ID
  "MaxRetryAttempts": 3,               // 0-10
  "RetryDelaySeconds": 5,              // 1-300
  "MaxMessagesPerMinute": 20,          // 1-30 (Telegram limit is 30/sec globally)
  "ProcessIntervalSeconds": 5          // Worker cycle frequency
}
```

**Alert Severity Levels**:
- `info`: Informational (ℹ️)
- `warning`: Warning condition (⚠️)
- `error`: Error occurred (❌)
- `critical`: Critical issue requiring immediate attention (🚨)

**Common Commands**:
```bash
# Check Telegram worker status
grep "TelegramWorker" logs/service.log | tail -20

# Check pending alerts
grep "QueueSize=" logs/service.log | tail -10

# Check delivery success rate
grep "processed.*alerts" logs/service.log | tail -20

# Check retry attempts
grep "Retry attempt" logs/service.log

# Check rate limiting
grep "Rate limit" logs/service.log
```

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

## 10. DASHBOARD AI CONVERTER (EasyLanguage → SDF v1)

**Component**: Cloudflare Worker + Claude API  
**Endpoint**: `POST /api/v1/strategies/convert-el`  
**Purpose**: Convert EasyLanguage trading strategies to SDF v1 format using Claude AI  
**Dashboard Page**: `/strategies/convert`

### Overview

The EL Converter allows users to paste EasyLanguage code and receive a structured SDF v1 JSON format suitable for the trading system. Uses Claude Sonnet 4.5 API with specialized system prompt.

**Conversion Flow**:
1. User pastes EL code in dashboard
2. Frontend calls Worker API: `POST /api/v1/strategies/convert-el`
3. Worker forwards to Claude API with system prompt
4. Claude analyzes code and returns JSON response
5. Worker logs conversion to D1 database
6. Frontend displays result with "Apply to Wizard" button

---

### 10.1 Happy Path - Full Conversion
**Objective**: Verify successful conversion of standard EasyLanguage code  
**Example**: Iron Condor strategy with clear entry/exit rules

**Test Steps**:
- [ ] 1. Open dashboard: `http://localhost:3000/strategies/convert`
- [ ] 2. Click "Incolla esempio" (Paste example) button
- [ ] 3. Verify Iron Condor example code appears in textarea:
  ```easylanguage
  inputs:
      EntryTimeOfDay(0930),
      ExitTimeOfDay(1530),
      ProfitTarget(100),
      StopLoss(-50),
      DaysToExpiration(7);
  
  // Iron Condor entry logic
  if Time = EntryTimeOfDay and ...
  ```
- [ ] 4. Click "Converti con AI" (Convert with AI) button
- [ ] 5. Verify loading state (spinner shown, button disabled)
- [ ] 6. Wait 5-15 seconds for Claude API response
- [ ] 7. Verify result panel appears with:
  - Badge: "Convertibile" (green) or "Convertibile parzialmente" (yellow)
  - Confidence: > 80% (e.g., "Confidenza: 92%")
  - Issues count: 0 or low number
  - JSON preview visible (expandable)
- [ ] 8. Expand JSON preview, verify structure:
  ```json
  {
    "strategy_type": "iron_condor",
    "entry_conditions": { ... },
    "exit_conditions": { ... },
    "risk_management": {
      "profit_target": 100,
      "stop_loss": -50
    },
    ...
  }
  ```
- [ ] 9. Click "Applica al wizard" (Apply to wizard) button
- [ ] 10. Verify navigation to `/strategies/wizard?step=1`
- [ ] 11. Verify wizard fields pre-populated with converted values

**Expected Results**:
- ✅ convertible = true
- ✅ confidence > 0.8
- ✅ issues.length = 0 (or minor warnings only)
- ✅ result_json contains all required SDF v1 fields
- ✅ Wizard receives converted data correctly

**Verification (Backend)**:
```bash
# Check D1 conversion log
curl http://localhost:8787/api/admin/el-conversion-log | jq '.[-1]'
# Expected: convertible="true", confidence>0.8, elapsed_ms<30000
```

**Timeline**: Paste code → Convert → Result displayed: typically 10-20 seconds

---

### 10.2 Partial Conversion
**Objective**: Verify handling of code with unsupported constructs  
**Scenario**: EL code uses features not supported in SDF v1

**Test Steps**:
- [ ] 1. Paste EL code with unsupported features:
  ```easylanguage
  // Code using Print, FileAppend, or external DLL calls
  Print("Debug message");
  FileAppend("log.txt", "trade data");
  ExternalDLL.CallFunction();
  ```
- [ ] 2. Click "Converti con AI"
- [ ] 3. Wait for response
- [ ] 4. Verify result panel shows:
  - Badge: "Convertibile parzialmente" (yellow/orange)
  - Confidence: 50-80%
  - Issues list populated:
    ```json
    {
      "type": "not_supported",
      "el_construct": "Print statement",
      "description": "Print() not supported in SDF v1",
      "suggestion": "Remove debug statements or use logging"
    }
    ```
- [ ] 5. Verify result_json still generated (with partial conversion)
- [ ] 6. Check warnings array populated

**Expected Results**:
- ✅ convertible = "partial"
- ✅ confidence between 0.5 and 0.8
- ✅ issues.length > 0 (specific unsupported features listed)
- ✅ result_json contains converted parts
- ✅ warnings explain manual steps needed

**User Action**: Review issues, manually adjust result_json, then apply to wizard

---

### 10.3 Non-Convertible Code
**Objective**: Verify rejection of non-EL code or incompatible logic  
**Scenario**: Code is Python, C++, or completely unrelated

**Test Steps**:
- [ ] 1. Paste non-EasyLanguage code:
  ```python
  def my_strategy():
      print("This is Python, not EasyLanguage")
      return True
  ```
- [ ] 2. Click "Converti con AI"
- [ ] 3. Wait for response
- [ ] 4. Verify result panel shows:
  - Badge: "Non convertibile" (red)
  - Confidence: < 50%
  - Issues: Major incompatibilities listed
  - result_json = null (no conversion possible)
- [ ] 5. Verify "Applica al wizard" button disabled or hidden

**Expected Results**:
- ✅ convertible = false
- ✅ confidence < 0.5
- ✅ result_json = null
- ✅ issues explain why conversion impossible
- ✅ User cannot proceed to wizard

---

### 10.4 Missing API Key
**Objective**: Verify graceful error when ANTHROPIC_API_KEY not configured  
**Scenario**: Worker deployed without API key in secrets

**Test Steps**:
- [ ] 1. Remove ANTHROPIC_API_KEY from `.dev.vars` (or set to empty string)
- [ ] 2. Restart Worker: `bunx wrangler dev`
- [ ] 3. Open dashboard converter page
- [ ] 4. Paste any EL code
- [ ] 5. Click "Converti con AI"
- [ ] 6. Verify error response:
  ```json
  {
    "error": "AI conversion not available",
    "message": "Anthropic API key not configured. Feature disabled."
  }
  ```
- [ ] 7. Verify dashboard shows error message:
  - "Conversione AI non disponibile. Contattare amministratore."
- [ ] 8. Verify error logged in Worker console:
  ```
  ANTHROPIC_API_KEY not configured
  ```

**Expected Results**:
- ✅ HTTP 503 Service Unavailable
- ✅ Graceful error message (not crash)
- ✅ Dashboard shows user-friendly error
- ✅ Worker continues running (feature disabled)

**Recovery**:
- [ ] 1. Add ANTHROPIC_API_KEY to `.dev.vars`
- [ ] 2. Restart Worker
- [ ] 3. Re-test conversion → Should work

---

### 10.5 API Timeout
**Objective**: Verify handling of Claude API timeout  
**Scenario**: Very large EL code (near 50k limit) causes slow response

**Test Steps**:
- [ ] 1. Generate large EL code (40k-50k characters)
  - Repeat Iron Condor logic 100+ times with variations
  - Or paste complex multi-strategy code
- [ ] 2. Paste in converter
- [ ] 3. Click "Converti con AI"
- [ ] 4. Monitor response time (should timeout after ~30 seconds)
- [ ] 5. Verify timeout error:
  ```json
  {
    "error": "timeout",
    "message": "Conversion timed out after 30 seconds"
  }
  ```
- [ ] 6. Check Worker logs for timeout

**Expected Results**:
- ✅ Request times out after 30 seconds (Cloudflare Worker CPU limit)
- ✅ HTTP 504 Gateway Timeout or 500 Internal Server Error
- ✅ Dashboard shows timeout error: "Conversione troppo lenta. Ridurre dimensione codice."
- ✅ User can retry with smaller code

**Cloudflare Worker Limits**:
- CPU Time: 30 seconds (cannot exceed)
- Memory: 128 MB
- Request Size: 100 MB

---

### 10.6 Invalid JSON Response
**Objective**: Verify error handling when Claude returns unparseable JSON  
**Scenario**: Claude API returns text that's not valid JSON (rare but possible)

**Test Steps**:
- [ ] 1. (Mock test) Modify Worker code to inject invalid JSON response
  - OR: Use very ambiguous EL code that confuses Claude
- [ ] 2. Trigger conversion
- [ ] 3. Verify error handling:
  ```json
  {
    "error": "unparseable_response",
    "message": "Claude API response not parseable as JSON"
  }
  ```
- [ ] 4. Check Worker logs for full Claude response (for debugging)

**Expected Results**:
- ✅ HTTP 500 Internal Server Error
- ✅ Error logged with Claude's actual response
- ✅ Dashboard shows generic error: "Errore di conversione. Riprovare."
- ✅ Worker does NOT crash (error caught)

**Recovery**: User retries or contacts support with request ID

---

### 10.7 Code Size Limit Exceeded
**Objective**: Verify rejection of code > 50,000 characters  
**Limit**: Hardcoded in Worker (50k chars)

**Test Steps**:
- [ ] 1. Generate 55,000-character EL code (paste same strategy 200 times)
- [ ] 2. Paste in converter
- [ ] 3. Click "Converti con AI"
- [ ] 4. Verify immediate rejection (before Claude API call):
  ```json
  {
    "error": "Code too large (max 50,000 chars)"
  }
  ```
- [ ] 5. Verify HTTP 413 Payload Too Large

**Expected Results**:
- ✅ Request rejected before API call (no wasted Claude API credits)
- ✅ HTTP 413 status code
- ✅ Dashboard shows error: "Codice troppo lungo. Massimo 50.000 caratteri."
- ✅ User sees character count indicator (if implemented)

**Frontend Enhancement** (future):
```tsx
// Show character count in real-time
<textarea maxLength={50000} />
<div>Caratteri: {code.length} / 50,000</div>
```

---

### 10.8 D1 Logging Failure
**Objective**: Verify conversion succeeds even if D1 logging fails  
**Scenario**: D1 database unavailable or INSERT fails

**Test Steps**:
- [ ] 1. (Mock test) Disable D1 database temporarily
  - OR: Corrupt el_conversion_log table
- [ ] 2. Paste valid EL code
- [ ] 3. Click "Converti con AI"
- [ ] 4. Verify conversion SUCCEEDS (Claude API called, result returned)
- [ ] 5. Check Worker logs for D1 error:
  ```
  Failed to log conversion to D1: [error details]
  ```
- [ ] 6. Verify user receives conversion result (logging failure is silent)

**Expected Results**:
- ✅ Conversion succeeds (D1 logging is non-critical)
- ✅ Result returned to dashboard
- ✅ D1 error logged to console (not shown to user)
- ✅ User experience unaffected

**Rationale**: Logging is for analytics, not critical path. Conversion should work even if logging fails.

---

### 10.9 UI Error States
**Objective**: Verify all error states displayed correctly in dashboard

#### 10.9a: Empty Code Submission
**Test Steps**:
- [ ] 1. Leave code textarea empty
- [ ] 2. Verify "Converti con AI" button disabled OR
- [ ] 3. Click button → Verify frontend validation error:
  - "Inserire codice EasyLanguage"

**Expected**: Button disabled or frontend validation prevents empty submission

---

#### 10.9b: Network Error
**Test Steps**:
- [ ] 1. Disconnect network or stop Worker
- [ ] 2. Paste EL code, click "Converti con AI"
- [ ] 3. Verify network error shown:
  - "Connessione fallita. Verificare rete."

**Expected**: Fetch error caught, user-friendly message shown

---

#### 10.9c: Loading State
**Test Steps**:
- [ ] 1. Paste code, click "Converti con AI"
- [ ] 2. During API call (5-15s), verify:
  - Spinner/loading indicator visible
  - "Converti con AI" button disabled
  - Code textarea disabled (prevent edits during conversion)
- [ ] 3. After response, verify loading state removed

**Expected**: Clear loading feedback, no double-submit possible

---

### 10.10 End-to-End Integration
**Objective**: Verify complete flow from EL paste to campaign creation  
**Timeline**: Full user journey test

**Test Steps**:
- [ ] 1. Open dashboard converter: `/strategies/convert`
- [ ] 2. Click "Incolla esempio" → Iron Condor code loaded
- [ ] 3. Click "Converti con AI"
- [ ] 4. Wait for conversion (10-15s)
- [ ] 5. Verify result: convertible=true, confidence>80%
- [ ] 6. Click "Applica al wizard"
- [ ] 7. Navigate to wizard: `/strategies/wizard?step=1`
- [ ] 8. Verify pre-populated fields:
  - Strategy Type: "Iron Condor"
  - Entry Time: "09:30"
  - Exit Time: "15:30"
  - Profit Target: 100
  - Stop Loss: -50
  - Days to Expiration: 7
- [ ] 9. Complete wizard (fill remaining fields)
- [ ] 10. Click "Salva strategia"
- [ ] 11. Verify strategy saved to database
- [ ] 12. Navigate to strategies list: `/strategies`
- [ ] 13. Verify new strategy appears
- [ ] 14. Click "Crea campagna"
- [ ] 15. Verify campaign created with strategy definition
- [ ] 16. Check database:
  ```sql
  SELECT campaign_id, strategy_name, strategy_definition_json 
  FROM campaigns WHERE strategy_name = 'Iron Condor' 
  ORDER BY created_at DESC LIMIT 1;
  ```

**Expected Results**:
- ✅ Seamless flow: Convert → Wizard → Save → Campaign
- ✅ No data loss during navigation
- ✅ Strategy definition correctly stored
- ✅ Campaign uses converted strategy
- ✅ Total time: < 5 minutes

**Verification Commands**:
```bash
# Check D1 conversion log
curl http://localhost:8787/api/admin/el-conversion-log | jq '.[-1]'

# Check saved strategy (if dashboard stores locally)
# Browser DevTools → Application → IndexedDB → strategies

# Check campaign in SQLite
sqlite3 src/OptionsExecutionService/data/options-execution.db \
  "SELECT * FROM campaigns WHERE strategy_name LIKE '%Iron%' ORDER BY created_at DESC LIMIT 1;"
```

---

### Configuration & Limits Reference

**API Endpoint**: `POST /api/v1/strategies/convert-el`

**Request Body**:
```json
{
  "easylanguage_code": "string (required, max 50k chars)",
  "user_notes": "string (optional)"
}
```

**Response Body** (Success):
```json
{
  "convertible": true | false | "partial",
  "confidence": 0.0 - 1.0,
  "result_json": {
    "strategy_type": "iron_condor",
    "entry_conditions": { ... },
    "exit_conditions": { ... },
    "risk_management": { ... },
    ...
  } | null,
  "issues": [
    {
      "type": "not_supported" | "ambiguous" | "manual_required",
      "el_construct": "string (e.g., 'Print statement')",
      "description": "string",
      "suggestion": "string"
    }
  ],
  "warnings": ["string"],
  "notes": "string"
}
```

**Response Body** (Error):
```json
{
  "error": "string (error_code)",
  "message": "string (user-friendly description)"
}
```

**HTTP Status Codes**:
- 200 OK: Conversion succeeded (check convertible field for actual result)
- 400 Bad Request: Invalid request (missing easylanguage_code, empty code)
- 413 Payload Too Large: Code > 50,000 characters
- 500 Internal Server Error: Claude API error, unparseable response
- 503 Service Unavailable: ANTHROPIC_API_KEY not configured

**Limits**:
- Code Size: 50,000 characters
- Timeout: 30 seconds (Cloudflare Worker CPU limit)
- Claude API: max_tokens=4096, model=claude-sonnet-4-5

**D1 Logging Table** (`el_conversion_log`):
```sql
CREATE TABLE el_conversion_log (
  id TEXT PRIMARY KEY,
  easylanguage_code TEXT,        -- Max 10k chars stored
  convertible TEXT,               -- "true", "false", "partial"
  confidence REAL,                -- 0.0 - 1.0
  result_json TEXT,               -- Full JSON result
  issues_count INTEGER,           -- Number of issues found
  elapsed_ms INTEGER,             -- API response time
  created_at TEXT                 -- ISO8601 timestamp
);
```

**Query D1 Log**:
```bash
# Recent conversions
curl http://localhost:8787/api/admin/el-conversion-log | jq '.[-10:]'

# Success rate
curl http://localhost:8787/api/admin/el-conversion-stats | jq '
  {
    total: .total,
    convertible: .convertible,
    partial: .partial,
    failed: .failed,
    avg_confidence: .avg_confidence
  }
'
```

**Frontend Routes**:
- `/strategies/convert` - EL Converter page
- `/strategies/wizard?step=1` - Strategy creation wizard
- `/strategies` - Strategy list
- `/campaigns` - Campaign list

**Common Issues**:
- **Slow conversion**: Large code → Reduce size or split into multiple strategies
- **Low confidence**: Ambiguous EL code → Add user_notes with clarifications
- **Non-convertible**: Not EasyLanguage → Verify code is valid EL syntax
- **Timeout**: Code too complex → Simplify logic or contact support

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

## Final Checklist

### Core Functionality
- [ ] **Heartbeat flow** (Local DB → Outbox → Worker → D1 → Dashboard)
  - Verify: HeartbeatWorker runs every 60s
  - Verify: OutboxSyncWorker syncs every 30s
  - Verify: D1 database updated
  - Verify: Dashboard displays metrics
- [ ] **Greeks monitoring** (all 4 metrics)
  - [ ] Delta alerts (threshold: 0.70)
  - [ ] Gamma alerts (threshold: 0.05)
  - [ ] Theta alerts (threshold: $50)
  - [ ] Vega alerts (threshold: $100)
- [ ] **Campaign lifecycle** (all 3 states)
  - [ ] Open → Active transition
  - [ ] Active → Closed (profit target, stop loss, time exit, manual)
  - [ ] Error handling (order rejection, IBKR disconnect)
- [ ] **Order lifecycle** (all 9 statuses)
  - [ ] Market order: PendingSubmit → Submitted → Active → Filled
  - [ ] Safety checks: MaxPositionSize, Balance, MaxPositionPct, CircuitBreaker
  - [ ] Error handling: Rejection, Cancellation, PartialFill, ConnectionLost
- [ ] **Telegram alerts** (all 11 categories)
  - [ ] Configuration, Queue Operations, Message Formatting
  - [ ] Rate Limiting, Retry Logic, Worker Lifecycle
  - [ ] Integration with other workers
- [ ] **Dashboard AI converter** (all 10 scenarios)
  - [ ] Happy path (full conversion)
  - [ ] Partial conversion, Non-convertible
  - [ ] Error handling (no API key, timeout, invalid JSON, code size limit)
  - [ ] End-to-end: Convert → Wizard → Save → Campaign

### Integration Tests
- [ ] **Campaign → Telegram alert**
  - Create active campaign → Trigger closure → Verify Telegram notification
- [ ] **Order rejection → Telegram notification**
  - Place invalid order → Verify IBKR rejection → Verify Telegram alert
- [ ] **Log error → Alert → Worker sync**
  - Append ERROR to log → LogReaderWorker detects → Alert created → Synced to Worker
- [ ] **Greeks threshold → Alert → Dashboard**
  - Position exceeds delta threshold → GreeksMonitorWorker creates alert → Dashboard displays
- [ ] **IBKR disconnect → Reconnect → Resume operations**
  - Stop TWS → Service detects disconnect → Restart TWS → Service reconnects → Orders resume

### Error Handling
- [ ] **IBKR disconnect → Graceful recovery**
  - heartbeat shows IBKRConnected=false
  - Orders paused during disconnect
  - Auto-reconnect after TWS restart
  - In-flight orders reconciled with IBKR state
- [ ] **Worker unavailable → Outbox retries**
  - Stop Cloudflare Worker → Generate events → Verify outbox status='pending'
  - Start Worker → Verify outbox retries → Status='sent'
- [ ] **Telegram API timeout → Exponential backoff**
  - Simulate timeout → Verify retry delays: 5s, 10s, 20s (exponential)
  - Verify alert dropped after MaxRetryAttempts (3)
- [ ] **Claude API error → Graceful degradation**
  - Invalid API key → 503 Service Unavailable
  - Timeout → 504 Gateway Timeout
  - Invalid JSON → 500 Internal Server Error (logged, not crash)

### Performance Metrics
- [ ] **Heartbeat latency** < 2 seconds (local save → outbox entry)
- [ ] **Order placement** < 5 seconds (PendingSubmit → Submitted to IBKR)
- [ ] **Campaign activation** < 10 seconds (Open → Active with all positions filled)
- [ ] **Dashboard load** < 3 seconds (initial page load with data)
- [ ] **AI conversion** < 20 seconds (EL code → Claude response)
- [ ] **Outbox sync** < 35 seconds (pending → sent, including retry wait)

### Data Integrity
- [ ] **No duplicate events** in D1 database (DedupeKey working)
- [ ] **No data loss** during IBKR disconnect (SQLite WAL persists state)
- [ ] **Campaign state consistency** (status matches positions and orders)
- [ ] **Order reconciliation** after reconnect (database matches IBKR reality)

### Security & Safety
- [ ] **TradingMode** = "paper" (CRITICAL: verify before ANY testing)
- [ ] **Circuit breaker** activates after 3 order failures in 60 min
- [ ] **MaxPositionSize** enforced (default: 10 contracts)
- [ ] **MaxPositionPctOfAccount** enforced (default: 20%)
- [ ] **No secrets in logs** (BotToken, API keys redacted)
- [ ] **Cloudflare Worker** requires X-Api-Key header (authentication working)

### Documentation
- [ ] **All issues documented** with severity (CRITICAL, HIGH, MEDIUM, LOW)
- [ ] **Session log complete** (Session Template filled out)
- [ ] **Logs archived** to `logs/test-session-YYYYMMDD/`
- [ ] **Known issues list** updated (if applicable)
- [ ] **Performance metrics** recorded

### Sign-Off
**Tested by**: {Your Name}  
**Date**: {YYYY-MM-DD}  
**Session Duration**: {X hours}  
**Tests Executed**: {X} / {Y total}  
**Tests Passed**: {Z}  
**Critical Issues**: {N} (must be 0 for PASS)

**Final Result**: {PASS | FAIL}

**Notes**: {Any additional observations, recommendations for future testing, system improvements}

---

## Appendix: Troubleshooting

### Issue: "Cannot connect to IBKR"
**Symptoms**: IBKRConnected=false in heartbeat, orders fail with "connection error"

**Checks**:
- [ ] TWS running? → Check Task Manager (Windows) or `ps aux | grep tws` (Linux)
- [ ] Port correct? → 7496 (live) or 7497 (paper) in appsettings.json
- [ ] API enabled in TWS? → TWS: Edit → Global Configuration → API → Settings → "Enable ActiveX and Socket Clients"
- [ ] Read-only API disabled? → TWS API Settings: Uncheck "Read-Only API"

**Fix**:
```powershell
# 1. Restart TWS
# 2. Enable API in TWS settings
# 3. Restart TradingSupervisorService
Stop-Service TradingSupervisorService
Start-Service TradingSupervisorService

# 4. Verify connection
grep "IBKR connection" logs/service.log
# Expected: "IBKR connection established"
```

---

### Issue: "SQLite database locked"
**Symptoms**: Queries fail with "database is locked", WAL checkpoint errors

**Cause**: WAL mode + external query holding lock while service writes

**Fix**:
```powershell
# Method 1: Stop services, then query (PREFERRED)
Stop-Service TradingSupervisorService
Stop-Service OptionsExecutionService

# Query database (lock released)
sqlite3 src/TradingSupervisorService/data/supervisor.db "SELECT * FROM service_heartbeats LIMIT 1"

# Restart services
Start-Service TradingSupervisorService
Start-Service OptionsExecutionService

# Method 2: Use Python script (respects WAL)
python3 scripts/query-db.py

# Method 3: Increase busy timeout in queries
sqlite3 src/TradingSupervisorService/data/supervisor.db \
  "PRAGMA busy_timeout=5000; SELECT * FROM service_heartbeats LIMIT 1"
```

**Prevention**: NEVER run long-running queries (e.g., SELECT with large results) while services are running. Use Read-only WAL mode or stop services first.

---

### Issue: "Worker returns 401 Unauthorized"
**Symptoms**: Outbox entries stuck in 'pending' status, Worker logs show "invalid_api_key"

**Cause**: API_KEY mismatch between TradingSupervisorService and Cloudflare Worker

**Fix**:
```powershell
# 1. Check local service config
cat src/TradingSupervisorService/appsettings.Local.json | grep ApiKey
# Example: "ApiKey": "20c98b3f05c7a06a2fcca3168aeeb7df5d8401cc70d007bde589cead6ea95792"

# 2. Check Worker config
cat infra/cloudflare/worker/.dev.vars | grep API_KEY
# Example: API_KEY=20c98b3f05c7a06a2fcca3168aeeb7df5d8401cc70d007bde589cead6ea95792

# 3. If mismatch, update to match
# Edit .dev.vars and set API_KEY to match appsettings.Local.json

# 4. Restart Worker
cd infra/cloudflare/worker
bunx wrangler dev --port 8787

# 5. Verify sync
grep "Outbox sync cycle completed" logs/service.log
# Expected: "1 sent, 0 failed"
```

---

### Issue: "Telegram not sending"
**Symptoms**: Alerts queued, but no Telegram messages received

**Checks**:
- [ ] **Telegram Enabled**? → `appsettings.json: Telegram:Enabled: true`
- [ ] **Bot token valid**? → Test with:
  ```bash
  curl https://api.telegram.org/bot{YOUR_BOT_TOKEN}/getMe
  # Expected: {"ok":true, "result":{"id":...}}
  ```
- [ ] **Chat ID correct**? → Send test message:
  ```bash
  curl -X POST https://api.telegram.org/bot{YOUR_BOT_TOKEN}/sendMessage \
    -d "chat_id={YOUR_CHAT_ID}&text=Test message"
  ```
- [ ] **Rate limit hit**? → Check logs:
  ```bash
  grep "Rate limit" logs/service.log
  # If found: Wait for rate limit window to expire (1 minute)
  ```
- [ ] **Worker processing**? → Check queue size:
  ```bash
  grep "QueueSize=" logs/service.log | tail -10
  # If queue not decreasing: TelegramWorker may be stuck
  ```

**Fix**:
```powershell
# 1. Verify configuration
cat src/TradingSupervisorService/appsettings.json | grep -A 7 "Telegram"

# 2. Test bot manually (outside service)
curl -X POST https://api.telegram.org/bot{BOT_TOKEN}/sendMessage `
  -d "chat_id={CHAT_ID}&text=Manual test"

# 3. Restart TradingSupervisorService
Restart-Service TradingSupervisorService

# 4. Trigger test alert (e.g., Greeks threshold)
# Verify message received within 10 seconds
```

---

### Issue: "Dashboard shows old data"
**Symptoms**: Dashboard metrics outdated, don't match database queries

**Cause**: React Query cache not invalidated, or auto-refresh interval too long

**Fix**:
```bash
# Method 1: Hard refresh browser
# Windows: Ctrl+Shift+R
# Mac: Cmd+Shift+R

# Method 2: Clear React Query cache
# Browser DevTools → Console:
queryClient.clear()

# Method 3: Check auto-refresh interval
# dashboard/src/hooks/useHeartbeat.ts or similar
# Look for: refetchInterval: 60000  (60 seconds)
# Reduce for faster updates (e.g., 10000 = 10 seconds)

# Method 4: Verify Worker API returns fresh data
curl http://localhost:8787/api/heartbeats | jq '.[-1].updated_at'
# Compare timestamp to current time
```

---

### Issue: "AI Converter timeout"
**Symptoms**: Dashboard shows "Conversione troppo lenta", Worker returns 504 Timeout

**Cause**: EL code too large or complex, Claude API slow

**Fix**:
```bash
# 1. Reduce code size
# - Remove comments
# - Simplify logic
# - Split into multiple strategies

# 2. Check code size
echo -n "{YOUR_EL_CODE}" | wc -c
# Limit: 50,000 characters

# 3. Verify ANTHROPIC_API_KEY valid
curl http://localhost:8787/api/v1/strategies/convert-el \
  -X POST \
  -H "Content-Type: application/json" \
  -d '{"easylanguage_code": "// Minimal test code"}'
# If 503: API key issue
# If 504: Timeout (code too complex)

# 4. Retry with simpler code
# Paste only essential logic (entry/exit conditions)
```

---

### Issue: "Orders stuck in PendingSubmit"
**Symptoms**: Orders created but never submitted to IBKR, status stays "PendingSubmit"

**Cause**: IBKR disconnect, OrderPlacer service not running, or circuit breaker open

**Checks**:
- [ ] **IBKR connected**? → Check heartbeat: `IBKRConnected=true`
- [ ] **Circuit breaker open**? → Check logs:
  ```bash
  grep "Circuit breaker" logs/service.log
  # If open: Reset via API or wait for cooldown (120 min)
  ```
- [ ] **OrderPlacer running**? → Check service status

**Fix**:
```powershell
# 1. Verify IBKR connection
curl http://localhost:8787/api/heartbeats | jq '.[-1].ibkr_connected'
# Expected: true

# 2. Check circuit breaker
curl http://localhost:5001/api/orders/circuit-breaker-status
# If open=true: Reset it
curl -X POST http://localhost:5001/api/orders/reset-circuit-breaker

# 3. Restart OptionsExecutionService
Restart-Service OptionsExecutionService

# 4. Monitor order status
sqlite3 src/OptionsExecutionService/data/options-execution.db \
  "SELECT order_id, status, updated_at FROM order_tracking ORDER BY created_at DESC LIMIT 5"
```

---

### Issue: "High CPU usage"
**Symptoms**: Service consumes > 50% CPU continuously

**Potential Causes**:
- HeartbeatWorker interval too short (< 10s)
- LogReaderWorker scanning large log files
- Tight loop in worker code (bug)
- IBKR API polling too frequently

**Diagnosis**:
```powershell
# 1. Check process CPU
Get-Process -Name TradingSupervisorService | Select-Object CPU, WorkingSet

# 2. Check worker intervals in appsettings.json
cat src/TradingSupervisorService/appsettings.json | grep IntervalSeconds

# 3. Review recent logs for tight loops
grep "Worker cycle" logs/service.log | tail -20
# If cycles running faster than configured interval: loop bug
```

**Fix**:
- Increase worker intervals (e.g., HeartbeatWorker: 60s → 120s)
- Optimize LogReaderWorker (reduce log file size, use log rotation)
- Profile code with dotnet-trace if issue persists

---

### Issue: "Memory leak (service grows over time)"
**Symptoms**: Service memory usage increases steadily, doesn't stabilize

**Diagnosis**:
```powershell
# 1. Monitor memory over 1 hour
while ($true) {
  Get-Process TradingSupervisorService | Select-Object WorkingSet64
  Start-Sleep 60
}

# 2. Check for unbounded collections
# - Telegram: _alertQueue (should be dequeueing)
# - Telegram: _messageTimestamps (should be pruned to last 60s)
# - Outbox: Should be clearing 'sent' entries eventually

# 3. Force garbage collection (temporary test)
[System.GC]::Collect()
# If memory drops significantly: GC issue, increase GC frequency
```

**Fix**:
- Review worker code for unbounded queues/lists
- Add periodic cleanup of old data (e.g., DELETE old outbox entries)
- Restart service weekly as workaround (if issue rare)

---

### Issue: "Tests pass locally but fail in CI"
**Potential Differences**:
- Time zone (CI uses UTC, local uses local timezone)
- Culture (decimal separator: `.` vs `,`)
- File paths (Windows `\` vs Linux `/`)
- Environment variables (missing in CI)

**Fix**:
```csharp
// Use CultureInfo.InvariantCulture for all formatting
string.Format(CultureInfo.InvariantCulture, "Delta: {0:F2}", position.Delta)

// Use Path.Combine for cross-platform paths
Path.Combine("logs", "service.log")  // NOT "logs\\service.log"

// Verify environment variables in CI
Console.WriteLine($"IBKR_PORT: {Environment.GetEnvironmentVariable("IBKR_PORT")}");
```

---

### General Debugging Commands

```powershell
# View all service logs (recent 100 lines)
Get-Content logs/service.log -Tail 100

# Follow logs in real-time (like tail -f)
Get-Content logs/service.log -Tail 10 -Wait

# Search logs for errors
Select-String -Path logs/service.log -Pattern "ERROR|FATAL" | Select-Object -Last 20

# Check all services status
Get-Service | Where-Object { $_.Name -like "*Trading*" } | Select-Object Name, Status

# Restart all services
Restart-Service TradingSupervisorService, OptionsExecutionService

# Check disk space (SQLite database growth)
Get-ChildItem src/*/data/*.db | Select-Object Name, Length
```

---

## TEST EXECUTION SESSION 1: 2026-04-19 18:30

### Issue #1: Workers use LogDebug but log level is Information
**Severity**: MEDIUM  
**Component**: HeartbeatWorker (and likely others)  
**Description**: Workers log their cycles with LogDebug instead of LogInformation, so we can't see if they're running  
**Impact**: Cannot verify workers are executing without changing log level  
**Fix**: Change HeartbeatWorker.cs line 116-118 from LogDebug to LogInformation  

### Action: Add Information log at cycle start

