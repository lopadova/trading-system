---
title: "E2E-04: Campaign Creation and Entry Order Placement"
tags: ["dev", "testing", "ibkr"]
status: reference
audience: ["developer"]
last-reviewed: "2026-04-21"
---

# E2E-04: Campaign Creation and Entry Order Placement

> Manual test checklist for loading strategies, creating campaigns, and placing entry orders
> REQUIRES_PAPER: Yes — IBKR Paper Trading account required for order submission

---

## Prerequisites

- [ ] E2E-01 (Startup) completed successfully
- [ ] OptionsExecutionService running with IBKR connection active
- [ ] Strategy file created in `strategies/` directory
- [ ] IBKR Paper Trading account with sufficient margin ($10,000+ recommended)
- [ ] Market hours (Mon-Fri 9:30-16:00 ET) for live order placement

---

## Test Steps

### 1. Create Test Strategy File

**Action**: Create `strategies/TEST_IRON_CONDOR.json`

```json
{
  "strategyId": "TEST_IRON_CONDOR_001",
  "name": "Test Iron Condor on SPY",
  "description": "End-to-end test strategy for paper trading",
  "symbol": "SPY",
  "strategyType": "iron_condor",
  "enabled": true,
  "entryRules": {
    "minDaysToExpiration": 30,
    "maxDaysToExpiration": 45,
    "targetDelta": 0.16,
    "deltaTolerance": 0.05,
    "minIVRank": 0.3,
    "maxOpenCampaigns": 2
  },
  "legs": [
    {
      "action": "SELL",
      "optionType": "PUT",
      "strikeSelection": "delta",
      "strikeValue": -0.16,
      "quantity": 1
    },
    {
      "action": "BUY",
      "optionType": "PUT",
      "strikeSelection": "width",
      "strikeValue": 5,
      "quantity": 1
    },
    {
      "action": "SELL",
      "optionType": "CALL",
      "strikeSelection": "delta",
      "strikeValue": 0.16,
      "quantity": 1
    },
    {
      "action": "BUY",
      "optionType": "CALL",
      "strikeSelection": "width",
      "strikeValue": 5,
      "quantity": 1
    }
  ],
  "exitRules": {
    "profitTargetPercent": 50.0,
    "stopLossPercent": 200.0,
    "maxDaysInTrade": 21,
    "deltaSpikeThreshold": 0.30
  },
  "riskManagement": {
    "maxPositionSize": 10,
    "maxPortfolioRisk": 0.05,
    "requireApproval": false
  }
}
```

**Expected**:
- [ ] File saved to `strategies/TEST_IRON_CONDOR.json`
- [ ] JSON is valid (use linter/validator)

---

### 2. Verify Strategy Loading

**Action**: Restart OptionsExecutionService (or wait for next strategy scan interval)

**Expected in logs**:
- [ ] Log: "StrategyLoader: Scanning strategies directory"
- [ ] Log: "Found strategy file: TEST_IRON_CONDOR.json"
- [ ] Log: "Strategy validation: TEST_IRON_CONDOR_001 is VALID"
- [ ] Log: "Strategy loaded: TEST_IRON_CONDOR_001 (enabled=true)"
- [ ] Log: "Total strategies loaded: 1 (enabled: 1)"

**Verification Query**:
```sql
-- In options.db
SELECT * FROM strategy_cache WHERE strategy_id = 'TEST_IRON_CONDOR_001';
-- Should show:
--   - strategy_id: 'TEST_IRON_CONDOR_001'
--   - name: 'Test Iron Condor on SPY'
--   - strategy_json: (full JSON)
--   - is_enabled: 1
--   - last_loaded_at: recent timestamp
```

**Expected**:
- [ ] Strategy cached in database
- [ ] is_enabled = 1
- [ ] JSON stored correctly

---

### 3. Trigger Campaign Creation (Manual or Automatic)

**Manual Trigger** (if implemented):
```powershell
# Use admin API or CLI tool
curl -X POST http://localhost:5000/admin/campaigns/create -H "Content-Type: application/json" -d '{
  "strategyId": "TEST_IRON_CONDOR_001"
}'
```

**Automatic Trigger**:
- Wait for CampaignMonitorWorker to evaluate entry rules
- Requires market conditions to match entry rules (IVR > 0.3, DTE 30-45, etc.)

**Expected in logs** (within monitoring interval):
- [ ] Log: "CampaignMonitorWorker: Evaluating strategy TEST_IRON_CONDOR_001"
- [ ] Log: "Entry rules evaluation: checking IVR, DTE, delta, open campaigns"
- [ ] Log: "Entry conditions MET for TEST_IRON_CONDOR_001"
- [ ] Log: "Creating new campaign for strategy TEST_IRON_CONDOR_001"
- [ ] Log: "Campaign created: campaign_id=[UUID]"

**Verification Query**:
```sql
SELECT * FROM campaigns WHERE strategy_id = 'TEST_IRON_CONDOR_001' ORDER BY created_at DESC LIMIT 1;
-- Should show:
--   - campaign_id: UUID
--   - strategy_id: 'TEST_IRON_CONDOR_001'
--   - status: 'pending' (initially)
--   - created_at: recent timestamp
--   - entry_date: NULL (not entered yet)
```

**Expected**:
- [ ] Campaign record created with status 'pending'
- [ ] campaign_id is valid UUID
- [ ] strategy_id matches TEST_IRON_CONDOR_001

---

### 4. Contract Resolution and Entry Order Placement

**Expected in logs** (immediately after campaign creation):
- [ ] Log: "Resolving contracts for campaign [campaign_id]"
- [ ] Log: "Searching SPY options chain for DTE 30-45 days"
- [ ] Log: "Found expiration: [date] (DTE: [days])"
- [ ] Log: "Resolving PUT contract with delta -0.16"
- [ ] Log: "Found PUT: SPY [date] [strike]P"
- [ ] Log: "Resolving CALL contract with delta 0.16"
- [ ] Log: "Found CALL: SPY [date] [strike]C"
- [ ] Log: "All 4 legs resolved successfully"
- [ ] Log: "Building entry orders for campaign [campaign_id]"
- [ ] Log: "Order 1: SELL 1 SPY [date] [strike]P @ MKT (safety: paper mode)"
- [ ] Log: "Order 2: BUY 1 SPY [date] [strike]P @ MKT"
- [ ] Log: "Order 3: SELL 1 SPY [date] [strike]C @ MKT"
- [ ] Log: "Order 4: BUY 1 SPY [date] [strike]C @ MKT"
- [ ] Log: "Submitting 4 entry orders to IBKR"
- [ ] Log: "Order submitted: ibkr_order_id=[id], status=Submitted"

**Verification Query**:
```sql
SELECT * FROM orders WHERE campaign_id = (
  SELECT campaign_id FROM campaigns WHERE strategy_id = 'TEST_IRON_CONDOR_001' ORDER BY created_at DESC LIMIT 1
);
-- Should show 4 rows (one per leg):
--   - order_id: UUID
--   - campaign_id: matches campaign
--   - ibkr_order_id: IBKR-assigned ID (integer)
--   - action: 'SELL' or 'BUY'
--   - option_type: 'PUT' or 'CALL'
--   - strike: decimal value
--   - expiration: date
--   - quantity: 1
--   - status: 'Submitted' or 'Filled'
```

**Expected**:
- [ ] 4 orders created (2 PUTs, 2 CALLs)
- [ ] Order actions match strategy legs (SELL, BUY, SELL, BUY)
- [ ] ibkr_order_id populated (IBKR confirmation)
- [ ] Orders submitted in paper trading mode (safety validation passed)

---

### 5. Monitor Order Fill Status

**Action**: Wait for IBKR to fill orders (paper trading fills almost instantly)

**Expected in logs** (within 1-5 seconds):
- [ ] Log: "Order status update: ibkr_order_id=[id], status=Filled"
- [ ] Log: "Order filled: [action] 1 SPY [strike][type] @ [price]"
- [ ] Log: "All 4 orders filled for campaign [campaign_id]"
- [ ] Log: "Updating campaign status: pending → active"

**Verification Query**:
```sql
-- Check order status
SELECT action, option_type, strike, status, filled_price FROM orders 
WHERE campaign_id = '[campaign_id]' 
ORDER BY created_at;
-- All 4 rows should show:
--   - status: 'Filled'
--   - filled_price: NOT NULL (execution price)

-- Check campaign status
SELECT status, entry_date, entry_price FROM campaigns WHERE campaign_id = '[campaign_id]';
-- Should show:
--   - status: 'active'
--   - entry_date: today's date
--   - entry_price: net credit received (or debit paid)
```

**Expected**:
- [ ] All 4 orders filled
- [ ] Campaign status changed to 'active'
- [ ] entry_price reflects net premium (SELL premium - BUY premium)

---

### 6. Verify Position Created

**Expected in logs**:
- [ ] Log: "Position snapshot created for campaign [campaign_id]"
- [ ] Log: "Position: 4 legs, net delta=[value], net theta=[value]"

**Verification Query**:
```sql
SELECT * FROM positions WHERE campaign_id = '[campaign_id]';
-- Should show 4 rows (one per leg):
--   - position_id: UUID
--   - campaign_id: matches campaign
--   - contract_symbol: full option symbol
--   - quantity: +1 (BUY) or -1 (SELL)
--   - avg_cost: fill price
--   - current_price: updated by market data
--   - delta, gamma, theta, vega: Greeks values
```

**Expected**:
- [ ] 4 positions created (matching 4 orders)
- [ ] Quantities: -1 (SELL), +1 (BUY), -1 (SELL), +1 (BUY)
- [ ] Greeks calculated for each leg

---

### 7. Test Safety Validation (Paper Mode Enforcement)

**Action**: Attempt to modify configuration to live mode (DO NOT ACTUALLY DO THIS!)

**For testing only** (in isolated test environment):
```json
{
  "TradingMode": "live"  // DANGER: Do not use in production without authorization
}
```

**Expected**:
- [ ] Configuration validation FAILS on service start
- [ ] Log: "CRITICAL: Trading mode set to LIVE - requires explicit confirmation"
- [ ] Service refuses to start OR blocks all order submissions
- [ ] No orders submitted to IBKR in live mode

**IMPORTANT**: Immediately revert to paper mode after testing validation logic.

---

## Success Criteria

- [ ] Strategy file loaded and validated
- [ ] Campaign created when entry conditions met
- [ ] 4 contracts resolved (2 PUTs, 2 CALLs) with correct deltas/strikes
- [ ] 4 entry orders submitted to IBKR
- [ ] All 4 orders filled (paper trading)
- [ ] Campaign status changed to 'active'
- [ ] 4 positions created with Greeks calculated
- [ ] Paper mode enforcement prevents live trading
- [ ] All database records consistent (campaigns, orders, positions)

---

## Performance Benchmarks

- **Strategy loading**: < 1 second per strategy
- **Campaign creation**: < 100ms (database insert)
- **Contract resolution**: < 5 seconds (IBKR API calls)
- **Order submission**: < 2 seconds per order
- **Order fill (paper)**: < 5 seconds (IBKR simulation)
- **Position creation**: < 100ms (database inserts)

---

## Cleanup

```sql
-- Delete test campaign and related records
DELETE FROM positions WHERE campaign_id = '[campaign_id]';
DELETE FROM orders WHERE campaign_id = '[campaign_id]';
DELETE FROM campaigns WHERE campaign_id = '[campaign_id]';
DELETE FROM strategy_cache WHERE strategy_id = 'TEST_IRON_CONDOR_001';

-- Delete strategy file
Remove-Item "strategies\TEST_IRON_CONDOR.json"
```

---

## Troubleshooting

### Campaign Not Created

- Check entry rules: IVR might be too low, DTE out of range, max campaigns reached
- Verify strategy is enabled (`enabled: true`)
- Check CampaignMonitorWorker is running (logs every monitoring interval)
- Review logs for entry evaluation: "Entry conditions NOT MET: [reason]"

### Contract Resolution Fails

- Market closed: Options chains only available during market hours
- No matching contracts: DTE range too narrow, delta not available
- Data subscription: Verify SPY options data in IBKR account

### Orders Not Submitted

- IBKR connection lost: Check connection status in logs
- Safety validation failed: Verify paper mode configuration
- Margin insufficient: Check IBKR account buying power (Paper account should have $1M+)

### Orders Stuck in Submitted

- In live trading (DO NOT TEST!), orders might not fill immediately
- In paper trading, fills are instant but delays possible if IBKR API slow
- Check IBKR TWS Order Management panel for order status

---

**Test Duration**: 5-15 minutes (faster during market hours)  
**Last Updated**: 2026-04-05  
**Dependencies**: E2E-01 (Startup), market hours for contract resolution
