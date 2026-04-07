# E2E-10: Emergency Stop, Position Liquidation, and Alerts

> Manual test checklist for emergency stop functionality
> REQUIRES_PAPER: Yes — Active positions required for liquidation test
> WARNING: This test closes all positions immediately

---

## Prerequisites

- [ ] E2E-04 (Campaign Open) completed
- [ ] Active campaign(s) with open positions
- [ ] Emergency stop mechanism implemented (admin API or CLI tool)
- [ ] IBKR Paper Trading connection active

---

## Test Steps

### 1. Pre-Stop State Verification

**Query current state**:
```sql
-- Count active campaigns
SELECT COUNT(*) FROM campaigns WHERE status = 'active';

-- List all open positions
SELECT 
  campaign_id,
  contract_symbol,
  quantity,
  avg_cost,
  current_price
FROM positions 
WHERE quantity != 0
ORDER BY campaign_id;
```

**Record**:
- [ ] Active campaigns: _____________
- [ ] Total open positions: _____________
- [ ] Combined notional value: $_____________

---

### 2. Trigger Emergency Stop

**Method A: Admin API** (if implemented):
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/admin/emergency-stop" -Method POST -Headers @{
  "X-Admin-Token" = "[admin-token]"
}
```

**Method B: CLI Tool**:
```bash
cd scripts
./emergency-stop.sh --confirm
```

**Method C: Configuration Flag**:
```json
{
  "EmergencyStop": true
}
# Restart service to apply
```

**Expected in logs** (immediate):
- [ ] Log: "🚨 EMERGENCY STOP TRIGGERED"
- [ ] Log: "Reason: Manual trigger via [method]"
- [ ] Log: "Initiating immediate position liquidation"
- [ ] Log: "Suspending all campaign monitoring"

---

### 3. Campaign Suspension

**Expected in logs**:
- [ ] Log: "Suspending campaign [id_1]"
- [ ] Log: "Suspending campaign [id_2]"
- [ ] Log: "All [N] campaigns suspended"

**Verification Query**:
```sql
SELECT campaign_id, status FROM campaigns WHERE status IN ('active', 'suspended');
-- All should show status = 'suspended'
```

**Expected**:
- [ ] All active campaigns → suspended
- [ ] CampaignMonitorWorker stops evaluating exit rules
- [ ] No new orders submitted

---

### 4. Position Liquidation Orders

**Expected in logs**:
- [ ] Log: "Building liquidation orders for [N] positions"
- [ ] Log: "Liquidation order 1: [action] [qty] [symbol] @ MARKET"
- [ ] Log: "Liquidation order 2: [action] [qty] [symbol] @ MARKET"
- [ ] Log: "... (all positions)"
- [ ] Log: "Submitting [N] liquidation orders to IBKR (paper mode)"
- [ ] Log: "Order type: MARKET (immediate execution)"

**Verification Query**:
```sql
SELECT * FROM orders 
WHERE order_type = 'liquidation' 
ORDER BY created_at DESC;
-- Should show N orders (one per position)
```

**Expected**:
- [ ] Liquidation orders created for ALL positions
- [ ] Order type = 'liquidation'
- [ ] Action reverses position (BUY to close short, SELL to close long)
- [ ] Order type = MARKET (fastest execution)

---

### 5. Order Execution Monitoring

**Expected in logs** (within 10 seconds in paper trading):
- [ ] Log: "Liquidation order [id_1] filled @ $[price]"
- [ ] Log: "Liquidation order [id_2] filled @ $[price]"
- [ ] Log: "... (all orders)"
- [ ] Log: "Position liquidation complete: [N]/[N] orders filled"

**Verification Query**:
```sql
SELECT 
  order_id,
  action,
  contract_symbol,
  status,
  filled_price
FROM orders 
WHERE order_type = 'liquidation'
ORDER BY created_at DESC;
-- All should show status = 'Filled'
```

**Expected**:
- [ ] All liquidation orders filled
- [ ] Fill prices recorded
- [ ] Execution time < 10 seconds (paper trading)

---

### 6. Position Closure Verification

**Verification Query**:
```sql
-- All positions should be closed (quantity = 0)
SELECT 
  contract_symbol,
  quantity,
  realized_pnl
FROM positions
ORDER BY campaign_id;
-- quantity should be 0 for all
```

**Expected**:
- [ ] All position quantities = 0 (closed)
- [ ] realized_pnl calculated for each position
- [ ] No open positions remain

**IBKR Verification**:
- [ ] Open TWS → Portfolio
- [ ] Verify: No open positions
- [ ] Cash balance updated (premium received/paid)

---

### 7. Campaign Final P&L Calculation

**Expected in logs**:
- [ ] Log: "Calculating final P&L for suspended campaigns"
- [ ] Log: "Campaign [id_1]: Entry=$[x], Exit=$[y], P&L=$[z]"
- [ ] Log: "Campaign [id_2]: Entry=$[x], Exit=$[y], P&L=$[z]"
- [ ] Log: "Total P&L from emergency stop: $[total]"

**Verification Query**:
```sql
SELECT 
  campaign_id,
  status,
  entry_price,
  exit_price,
  realized_pnl,
  exit_reason
FROM campaigns
WHERE status = 'suspended' OR status = 'closed'
ORDER BY exit_date DESC;
```

**Expected**:
- [ ] Campaign status = 'closed' (if liquidation complete)
- [ ] exit_reason = 'EmergencyStop'
- [ ] realized_pnl calculated (may be negative loss)

---

### 8. Alert and Notification

**Expected in logs**:
- [ ] Log: "Creating EMERGENCY_STOP alert"
- [ ] Log: "Sending Telegram alert to all admins"
- [ ] Log: "Alert severity: CRITICAL"

**Verification Query**:
```sql
SELECT * FROM alert_history WHERE alert_type = 'EMERGENCY_STOP' ORDER BY created_at DESC LIMIT 1;
```

**Expected**:
- [ ] Alert created with severity = 'critical'
- [ ] Message includes: reason, campaign count, position count, total P&L
- [ ] Telegram notification sent immediately (bypass rate limits)
- [ ] Dashboard shows emergency banner

---

### 9. System Lock-Down Verification

**Expected behavior after emergency stop**:
- [ ] CampaignMonitorWorker: Skips all execution
- [ ] No new campaigns can be created
- [ ] No new orders submitted (except manual override)
- [ ] Strategy loading disabled

**Test**: Attempt to create new campaign (should fail)

**Expected in logs**:
- [ ] Log: "Campaign creation blocked: Emergency stop active"
- [ ] Log: "All trading operations suspended"

---

### 10. Recovery (Reset Emergency Stop)

**Action**: Reset emergency stop flag

**Method A: Admin API**:
```powershell
Invoke-RestMethod -Uri "http://localhost:5000/admin/emergency-stop/reset" -Method POST
```

**Method B: Configuration**:
```json
{
  "EmergencyStop": false
}
# Restart service
```

**Expected in logs**:
- [ ] Log: "Emergency stop reset by [user]"
- [ ] Log: "Trading operations resuming"
- [ ] Log: "CampaignMonitorWorker: Resuming monitoring"

**Verification**:
- [ ] New campaigns can be created
- [ ] System returns to normal operation
- [ ] Alert logged: EMERGENCY_STOP_RESET

---

## Success Criteria

- [ ] Emergency stop triggered via admin command
- [ ] All campaigns suspended immediately
- [ ] Liquidation orders generated for ALL positions
- [ ] All liquidation orders filled (paper trading)
- [ ] All positions closed (quantity = 0)
- [ ] Final P&L calculated for all campaigns
- [ ] CRITICAL alert created and sent
- [ ] System locked-down (no new trading)
- [ ] Manual reset restores normal operation

---

## Performance Benchmarks

- **Stop trigger latency**: < 1 second
- **Campaign suspension**: < 100ms (DB update)
- **Liquidation order generation**: < 2 seconds
- **Order execution (paper)**: < 10 seconds total
- **Alert notification**: < 5 seconds

---

## Edge Cases

### Partial Fill on Liquidation Order

**Scenario**: In live trading (DO NOT TEST!), MARKET orders may partially fill

**Expected**:
- [ ] System retries remaining quantity
- [ ] Alert updated with partial fill status
- [ ] Manual intervention may be required

### IBKR Connection Lost During Stop

**Expected**:
- [ ] Liquidation orders queued locally
- [ ] Retry on reconnection
- [ ] Alert severity escalated to CRITICAL
- [ ] Manual verification required

---

## Cleanup

```sql
-- Emergency stop campaigns remain in database for audit
-- DO NOT DELETE

-- Verify final state
SELECT COUNT(*) FROM campaigns WHERE exit_reason = 'EmergencyStop';
SELECT COUNT(*) FROM positions WHERE quantity != 0;
-- Should show: 0 open positions

-- Delete test alerts (optional)
DELETE FROM alert_history WHERE alert_type IN ('EMERGENCY_STOP', 'EMERGENCY_STOP_RESET') AND created_at < datetime('now', '-1 day');
```

---

## Troubleshooting

### Emergency Stop Not Triggering

- Check admin authentication
- Verify configuration flag applied (restart required)
- Check logs for authorization errors

### Liquidation Orders Not Submitted

- Verify IBKR connection active
- Check paper mode validation passes
- Review order submission logs
- Ensure no existing order submission locks

### Positions Not Closing

- Check order fill status in IBKR TWS
- Verify order type = MARKET (should fill instantly in paper)
- Check for IBKR error codes in logs

### System Not Resuming After Reset

- Verify emergency stop flag cleared
- Restart services to apply configuration
- Check for database locks or corruption

---

## Safety Notes

⚠️ **IMPORTANT**: Emergency stop should ONLY be used in genuine emergency situations:
- Critical bug discovered in production
- Unexpected market conditions (circuit breaker, trading halt)
- System malfunction detected
- Regulatory requirement
- Account compromise suspected

**NOT for**:
- Normal end-of-day shutdown (use graceful stop)
- Strategy underperforming (use individual campaign exit)
- Routine maintenance (schedule during off-hours)

---

**Test Duration**: 5-10 minutes  
**Last Updated**: 2026-04-05  
**Dependencies**: E2E-04 (Campaign Open), active positions required
