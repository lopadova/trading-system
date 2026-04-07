# E2E-06: Profit Target Hit and Campaign Exit

> Manual test checklist for automatic position exit on profit target
> REQUIRES_PAPER: Yes — Active campaign required

---

## Prerequisites

- [ ] E2E-04 (Campaign Open) completed successfully
- [ ] Active campaign with filled positions
- [ ] OptionsExecutionService running with IBKR connection
- [ ] CampaignMonitorWorker enabled and monitoring exit rules
- [ ] Strategy has profit target configured (e.g., 50% of max profit)

**Strategy Configuration** (excerpt):
```json
{
  "exitRules": {
    "profitTargetPercent": 50.0,
    "stopLossPercent": 200.0,
    "maxDaysInTrade": 21
  }
}
```

---

## Test Steps

### 1. Calculate Expected Profit Target

**Action**: Query campaign entry details

```sql
SELECT 
  campaign_id,
  entry_price,
  entry_date,
  status
FROM campaigns 
WHERE campaign_id = '[campaign_id]';
```

**Manual Calculation**:
```
Max Profit (Iron Condor) = Net Credit Received at Entry
Example: Entry price = $2.50 (credit per spread)
Profit Target (50%) = $2.50 × 0.50 = $1.25
Exit Target Price = $2.50 - $1.25 = $1.25 (debit to close)
```

**Expected**:
- [ ] Campaign status = 'active'
- [ ] entry_price recorded (e.g., $250 credit for 1 contract = $2.50/share × 100)
- [ ] Profit target = entry_price × (1 - profitTargetPercent/100)

---

### 2. Monitor Current P&L

**Action**: Watch CampaignMonitorWorker logs

**Expected in logs** (every monitoring cycle):
- [ ] Log: "CampaignMonitorWorker: Monitoring active campaign [id]"
- [ ] Log: "Calculating current P&L for campaign [id]"
- [ ] Log: "Entry price: $[entry], Current value: $[current], P&L: $[pnl]"
- [ ] Log: "P&L percentage: [%] (target: 50%)"

**Verification Query**:
```sql
-- Query current position values
SELECT 
  contract_symbol,
  quantity,
  avg_cost,
  current_price,
  (current_price - avg_cost) * quantity * 100 AS position_pnl
FROM positions
WHERE campaign_id = '[campaign_id]';

-- Sum to get total P&L
SELECT 
  SUM((current_price - avg_cost) * quantity * 100) AS total_pnl
FROM positions
WHERE campaign_id = '[campaign_id]';
```

**Expected**:
- [ ] Current position values updating (from market data)
- [ ] P&L calculated: (current_value - entry_value)
- [ ] P&L percentage: (current_value - entry_value) / entry_value × 100

---

### 3. Wait for Market Movement (Profit Target Approach)

**Natural Market Movement**: Wait for option premium to decay or underlying to move favorably

**Simulated Movement** (for faster testing):
```sql
-- TESTING ONLY: Manually adjust current_price to simulate profit
-- Iron Condor becomes profitable when premium decreases
UPDATE positions 
SET current_price = avg_cost * 0.5  -- Simulate 50% decay
WHERE campaign_id = '[campaign_id]';
```

**Expected in logs** (on next monitor cycle):
- [ ] Log: "P&L update: Current value $[reduced], P&L: $[positive]"
- [ ] Log: "P&L percentage: 48% (approaching target 50%)"

---

### 4. Profit Target Hit Detection

**Action**: Continue monitoring until P&L >= 50% of max profit

**Expected in logs** (when target hit):
- [ ] Log: "✅ PROFIT TARGET HIT: Campaign [id] reached 50.0% of max profit"
- [ ] Log: "P&L: $[amount] ([%]%) - Entry: $[entry], Current: $[current]"
- [ ] Log: "Initiating exit orders for campaign [id]"
- [ ] Log: "Exit reason: ProfitTarget"

**Verification Query**:
```sql
SELECT exit_reason FROM campaigns WHERE campaign_id = '[campaign_id]';
-- Should show: exit_reason = 'ProfitTarget'
```

**Expected**:
- [ ] Exit triggered automatically
- [ ] exit_reason set to 'ProfitTarget'
- [ ] Exit orders prepared

---

### 5. Exit Order Placement

**Expected in logs** (immediately after target hit):
- [ ] Log: "Building exit orders: reversing all 4 positions"
- [ ] Log: "Exit order 1: BUY 1 SPY [strike]P (to close short PUT)"
- [ ] Log: "Exit order 2: SELL 1 SPY [strike]P (to close long PUT)"
- [ ] Log: "Exit order 3: BUY 1 SPY [strike]C (to close short CALL)"
- [ ] Log: "Exit order 4: SELL 1 SPY [strike]C (to close long CALL)"
- [ ] Log: "Submitting 4 exit orders to IBKR (paper mode)"

**Verification Query**:
```sql
SELECT * FROM orders 
WHERE campaign_id = '[campaign_id]' 
  AND order_type = 'exit'
ORDER BY created_at DESC;
-- Should show 4 exit orders (reverse of entry orders)
```

**Expected**:
- [ ] 4 exit orders created
- [ ] Actions reversed: BUY ↔ SELL
- [ ] Order type = 'exit'
- [ ] Status = 'Submitted'

---

### 6. Exit Order Fill Confirmation

**Action**: Wait for IBKR to fill exit orders (paper trading: instant)

**Expected in logs** (within 5 seconds):
- [ ] Log: "Order filled: [action] 1 SPY [strike][type] @ $[price]"
- [ ] Log: "Exit order 1 filled @ $[price]"
- [ ] Log: "Exit order 2 filled @ $[price]"
- [ ] Log: "Exit order 3 filled @ $[price]"
- [ ] Log: "Exit order 4 filled @ $[price]"
- [ ] Log: "All exit orders filled for campaign [id]"

**Verification Query**:
```sql
SELECT COUNT(*) FROM orders 
WHERE campaign_id = '[campaign_id]' 
  AND order_type = 'exit' 
  AND status = 'Filled';
-- Should return: 4 (all filled)
```

**Expected**:
- [ ] All 4 exit orders filled
- [ ] Fill prices recorded
- [ ] Total exit credit/debit calculated

---

### 7. Campaign Closure and Final P&L

**Expected in logs**:
- [ ] Log: "Updating campaign status: active → closed"
- [ ] Log: "Final P&L calculation: Entry=$[entry], Exit=$[exit], Profit=$[pnl]"
- [ ] Log: "Campaign [id] closed successfully"
- [ ] Log: "Exit date: [date], Days in trade: [days], ROI: [%]%"

**Verification Query**:
```sql
SELECT 
  campaign_id,
  status,
  entry_price,
  exit_price,
  realized_pnl,
  exit_date,
  exit_reason,
  JULIANDAY(exit_date) - JULIANDAY(entry_date) AS days_in_trade
FROM campaigns 
WHERE campaign_id = '[campaign_id]';
```

**Expected**:
- [ ] status = 'closed'
- [ ] exit_price populated (total exit credit/debit)
- [ ] realized_pnl = exit_price - entry_price
- [ ] exit_date = today
- [ ] exit_reason = 'ProfitTarget'
- [ ] days_in_trade < maxDaysInTrade (21)

---

### 8. Position Cleanup

**Verification Query**:
```sql
-- Positions should show zero quantity (fully closed)
SELECT 
  contract_symbol,
  quantity,
  avg_cost,
  current_price,
  realized_pnl
FROM positions 
WHERE campaign_id = '[campaign_id]';
```

**Expected (Option A: Quantity zeroed)**:
- [ ] All 4 positions show quantity = 0 (closed)
- [ ] realized_pnl calculated for each position

**Expected (Option B: Positions deleted)**:
- [ ] Positions moved to historical table or deleted
- [ ] P&L aggregated in campaign record

---

### 9. Alert and Notification

**Expected**:
- [ ] Alert created: CAMPAIGN_CLOSED_PROFIT_TARGET
- [ ] Telegram notification sent (if enabled)
- [ ] Dashboard notification (if user logged in)

**Verification Query**:
```sql
SELECT * FROM alert_history 
WHERE alert_type = 'CAMPAIGN_CLOSED_PROFIT_TARGET' 
  AND metadata LIKE '%[campaign_id]%';
```

**Expected**:
- [ ] Alert severity = 'info' (success)
- [ ] Message includes: P&L amount, ROI %, days in trade
- [ ] Metadata contains full campaign details (JSON)

---

## Success Criteria

- [ ] Profit target detection accurate (50% of max profit)
- [ ] Exit triggered automatically when target hit
- [ ] 4 exit orders submitted (reversing entry positions)
- [ ] All exit orders filled in paper trading
- [ ] Campaign status updated to 'closed'
- [ ] Final P&L calculated and recorded
- [ ] exit_reason = 'ProfitTarget'
- [ ] Positions closed (quantity = 0 or deleted)
- [ ] Alert and notification created

---

## Performance Benchmarks

- **P&L calculation**: < 50ms per campaign
- **Exit trigger latency**: < 1 monitoring cycle (e.g., 60s)
- **Exit order submission**: < 5 seconds (4 orders)
- **Order fill (paper)**: < 5 seconds total
- **Campaign closure**: < 100ms (database update)

---

## Expected P&L Scenarios

### Iron Condor Profit Target (50%)

**Entry**: Sell Iron Condor for $2.50 credit (max profit = $250 per contract)
**Target**: 50% of max profit = $125
**Exit**: Buy back for $1.25 debit
**Realized P&L**: $250 - $125 = $125 ✅

### Time Decay Scenario

- Entry Day 0: Premium = $2.50
- Day 7: Premium decays to $2.00 (20% profit) — not yet
- Day 14: Premium decays to $1.25 (50% profit) — EXIT ✅

---

## Cleanup

```sql
-- Campaign remains in database for historical record (do not delete)
-- Verify closed status
SELECT status, exit_reason FROM campaigns WHERE campaign_id = '[campaign_id]';
```

---

## Troubleshooting

### Profit Target Not Triggering

- Check current_price updates (market data)
- Verify profitTargetPercent in strategy config
- Check logs for P&L calculation errors
- Ensure CampaignMonitorWorker is running

### Exit Orders Not Submitted

- Check IBKR connection status
- Verify paper mode validation passes
- Check order submission logs for errors
- Ensure sufficient buying power (should be N/A for closing orders)

### Exit Orders Not Filled

- In paper trading, fills are instant (check if orders actually submitted)
- In live trading (DO NOT TEST!), market liquidity may delay fills
- Check IBKR TWS Order Management for order status

### Incorrect P&L Calculation

- Verify entry_price recorded correctly at campaign open
- Check current_price updates from market data
- Review P&L formula: (exit - entry) for credits, (entry - exit) for debits
- Iron Condor: Profit when premium decreases (buy back cheaper)

---

**Test Duration**: 10-30 minutes (depends on market movement or simulation)  
**Last Updated**: 2026-04-05  
**Dependencies**: E2E-04 (Campaign Open), market data for P&L calculation
