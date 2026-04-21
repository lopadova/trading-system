---
title: "E2E-07: IBKR Connection Loss, Reconnection, and State Recovery"
tags: ["dev", "testing", "ibkr", "incident-response"]
status: reference
audience: ["developer"]
last-reviewed: "2026-04-21"
---

# E2E-07: IBKR Connection Loss, Reconnection, and State Recovery

> Manual test checklist for connection resilience and state recovery
> REQUIRES_PAPER: Yes — IBKR Paper Trading connection required

---

## Prerequisites

- [ ] E2E-01 (Startup) completed
- [ ] OptionsExecutionService running with active IBKR connection
- [ ] Optional: Active campaign with open positions

---

## Test Steps

### 1. Establish Baseline Connection

**Verification**:
```sql
-- Query connection state
SELECT * FROM system_state WHERE key = 'ibkr_connection_status';
-- Should show: value = 'Connected'
```

**Expected in logs**:
- [ ] Log: "IBKR connection status: Connected"
- [ ] Log: "Account data: [DU-xxxxx]"

---

### 2. Simulate Connection Loss

**Action**: Close TWS/IB Gateway application (Force quit or disconnect network)

**Expected in logs** (within 30 seconds):
- [ ] Log: "IBKR connection lost: connection error detected"
- [ ] Log: "Connection status changed: Connected → Disconnected"
- [ ] Log: "IbkrConnectionWorker: Starting reconnection attempts"

**Verification Query**:
```sql
SELECT value FROM system_state WHERE key = 'ibkr_connection_status';
-- Should show: 'Disconnected'
```

**Expected**:
- [ ] Connection status updated to Disconnected
- [ ] Reconnection logic triggered

---

### 3. Monitor Reconnection Attempts

**Expected in logs** (with exponential backoff):
- [ ] Log: "Reconnection attempt 1 (delay: 5s)"
- [ ] Log: "Reconnection attempt 2 (delay: 10s)"
- [ ] Log: "Reconnection attempt 3 (delay: 20s)"
- [ ] Log: "Reconnection attempt 4 (delay: 40s)"

**Expected**:
- [ ] Delays increase exponentially (5s, 10s, 20s, 40s, 60s max)
- [ ] Max attempts not exceeded (e.g., 20 attempts)
- [ ] No order submissions during disconnection

---

### 4. Restore Connection

**Action**: Restart TWS/IB Gateway (Paper mode, port 4002)

**Expected in logs** (on next retry):
- [ ] Log: "Reconnection attempt [N] succeeded"
- [ ] Log: "Connected to IBKR: server version [version]"
- [ ] Log: "Connection status changed: Disconnected → Connected"
- [ ] Log: "Requesting account summary to verify connection"
- [ ] Log: "Account balance: [amount]"

**Verification**:
- [ ] Connection restored
- [ ] Account data received
- [ ] System ready for trading

---

### 5. State Recovery: Position Synchronization

**Expected in logs** (if positions exist):
- [ ] Log: "Synchronizing positions after reconnection"
- [ ] Log: "Requesting position updates from IBKR"
- [ ] Log: "Position sync complete: [N] positions verified"

**Verification Query**:
```sql
-- Compare position quantities before/after reconnection
SELECT contract_symbol, quantity FROM positions WHERE campaign_id = '[campaign_id]';
-- Should match IBKR account positions
```

**Expected**:
- [ ] Positions match IBKR account (no data loss)
- [ ] No duplicate position entries

---

### 6. State Recovery: Order Status Sync

**Expected in logs** (if orders exist):
- [ ] Log: "Requesting open orders from IBKR"
- [ ] Log: "Order status sync: [N] open orders"
- [ ] Log: "Updating order statuses in database"

**Verification Query**:
```sql
-- Check order statuses are current
SELECT ibkr_order_id, status FROM orders WHERE status IN ('Submitted', 'PartiallyFilled');
```

**Expected**:
- [ ] Order statuses synchronized
- [ ] Filled orders marked as Filled
- [ ] Cancelled orders marked as Cancelled

---

### 7. Test Campaign Monitoring Resume

**Expected in logs** (CampaignMonitorWorker):
- [ ] Log: "CampaignMonitorWorker: Resuming monitoring after reconnection"
- [ ] Log: "Monitoring [N] active campaigns"
- [ ] Log: "Calculating P&L for campaign [id]"

**Expected**:
- [ ] Campaign monitoring resumes automatically
- [ ] Exit rules evaluated normally
- [ ] No data loss or corruption

---

### 8. Test Alert on Prolonged Disconnection

**Action**: Keep TWS closed for > 5 minutes (configurable threshold)

**Expected**:
- [ ] Alert created: IBKR_CONNECTION_LOST
- [ ] Alert severity = 'critical'
- [ ] Telegram notification sent (if enabled)

**Verification Query**:
```sql
SELECT * FROM alert_history WHERE alert_type = 'IBKR_CONNECTION_LOST' ORDER BY created_at DESC LIMIT 1;
```

**Expected**:
- [ ] Alert created after threshold time
- [ ] Message includes: disconnection duration, reconnection attempts

---

## Success Criteria

- [ ] Connection loss detected within 30 seconds
- [ ] Reconnection attempts with exponential backoff
- [ ] Connection restored when TWS available
- [ ] Position state synchronized (no data loss)
- [ ] Order statuses synchronized
- [ ] Campaign monitoring resumes automatically
- [ ] Alert sent on prolonged disconnection
- [ ] No orders submitted while disconnected

---

## Edge Cases

### Disconnection During Order Submission

**Expected**:
- [ ] Order submission fails gracefully
- [ ] Order marked as 'Failed' with reason
- [ ] Retry on reconnection (if idempotency key preserved)

### Disconnection with Open Positions

**Expected**:
- [ ] Positions remain safe (tracked locally)
- [ ] Greeks calculations pause (or use last known values)
- [ ] Exit rules not evaluated until reconnection

---

## Cleanup

```sql
-- Delete test alerts
DELETE FROM alert_history WHERE alert_type = 'IBKR_CONNECTION_LOST' AND created_at > datetime('now', '-1 hour');
```

---

**Test Duration**: 10-15 minutes  
**Last Updated**: 2026-04-05  
**Dependencies**: E2E-01 (Startup)
