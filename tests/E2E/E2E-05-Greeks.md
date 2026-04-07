# E2E-05: Greeks Calculation, Real-time Updates, and Risk Alerts

> Manual test checklist for position Greeks monitoring and risk management
> REQUIRES_PAPER: Yes — Active campaign with positions required

---

## Prerequisites

- [ ] E2E-04 (Campaign Open) completed successfully
- [ ] Active campaign with 4 positions (Iron Condor)
- [ ] OptionsExecutionService running with market data connection
- [ ] GreeksMonitorWorker enabled in supervisor.json
- [ ] Market hours for real-time Greeks updates

**Configuration Check** (`supervisor.json`):
```json
{
  "GreeksMonitor": {
    "Enabled": true,
    "MonitoringIntervalSeconds": 60,
    "DeltaAlertThreshold": 0.5,
    "GammaAlertThreshold": 0.1,
    "VegaAlertThreshold": 100.0
  }
}
```

---

## Test Steps

### 1. Verify Initial Greeks Calculation

**Action**: Query positions table immediately after campaign entry

```sql
SELECT 
  contract_symbol,
  quantity,
  delta,
  gamma,
  theta,
  vega,
  updated_at
FROM positions 
WHERE campaign_id = '[campaign_id]'
ORDER BY contract_symbol;
```

**Expected**:
- [ ] 4 positions (2 PUTs, 2 CALLs)
- [ ] Delta values calculated for each leg
  - Short PUT: delta ≈ -0.16 (per contract) → position delta ≈ +16 (qty = -1)
  - Long PUT: delta ≈ -0.11 → position delta ≈ -11 (qty = +1)
  - Short CALL: delta ≈ +0.16 → position delta ≈ -16 (qty = -1)
  - Long CALL: delta ≈ +0.11 → position delta ≈ +11 (qty = +1)
- [ ] Gamma, theta, vega populated for each leg
- [ ] updated_at is recent timestamp

**Net Greeks** (for Iron Condor):
- [ ] Net delta ≈ 0 (±5) — delta-neutral strategy
- [ ] Net gamma < 0 — negative gamma (sold options)
- [ ] Net theta > 0 — positive theta decay
- [ ] Net vega < 0 — short volatility

---

### 2. Test Greeks Aggregation (Portfolio Level)

**Action**: Query aggregated Greeks across all positions

```sql
SELECT 
  campaign_id,
  SUM(delta) AS net_delta,
  SUM(gamma) AS net_gamma,
  SUM(theta) AS net_theta,
  SUM(vega) AS net_vega
FROM positions
WHERE campaign_id = '[campaign_id]'
GROUP BY campaign_id;
```

**Expected**:
- [ ] net_delta close to 0 (delta-neutral)
- [ ] net_gamma negative (short options)
- [ ] net_theta positive (time decay benefit)
- [ ] net_vega negative (short volatility)

**Verification in logs**:
- [ ] Log: "GreeksMonitorWorker: Calculating portfolio Greeks"
- [ ] Log: "Campaign [id]: Net delta=[value], gamma=[value], theta=[value], vega=[value]"

---

### 3. Monitor Real-time Greeks Updates

**Action**: Wait for market data updates (underlying price movement)

**Expected in logs** (every 60 seconds or on price change):
- [ ] Log: "Market data update: SPY price=[price]"
- [ ] Log: "Recalculating Greeks for 4 positions"
- [ ] Log: "Position [id] Greeks updated: delta=[new], gamma=[new], theta=[new], vega=[new]"

**Verification Query** (after 5 minutes):
```sql
SELECT 
  contract_symbol,
  delta,
  gamma,
  updated_at
FROM positions 
WHERE campaign_id = '[campaign_id]'
ORDER BY updated_at DESC;
```

**Expected**:
- [ ] updated_at timestamps are recent (within last minute)
- [ ] Delta values change as underlying price moves
- [ ] Gamma remains relatively stable (unless near expiration)

---

### 4. Test Delta Spike Alert

**Scenario**: Underlying price moves significantly, causing delta spike

**Action**: Monitor positions during volatile market or simulate price spike

**Alternative** (manual simulation):
```sql
-- Manually update delta to trigger alert (for testing only)
UPDATE positions 
SET delta = 60  -- Exceeds threshold of 50 (0.5 * 100)
WHERE campaign_id = '[campaign_id]' 
  AND contract_symbol LIKE '%PUT%' 
  AND quantity = -1
LIMIT 1;
```

**Expected in logs** (on next GreeksMonitorWorker cycle):
- [ ] Log: "GreeksMonitorWorker: Checking delta thresholds"
- [ ] Log: "ALERT: Position [id] delta spike detected: |60| > threshold 50"
- [ ] Log: "Creating alert: GREEKS_DELTA_SPIKE"

**Verification Query**:
```sql
SELECT * FROM alert_history WHERE alert_type = 'GREEKS_DELTA_SPIKE' ORDER BY created_at DESC LIMIT 1;
-- Should show:
--   - alert_type: 'GREEKS_DELTA_SPIKE'
--   - severity: 'warning'
--   - message: Contains delta value and position details
--   - metadata: JSON with position_id, campaign_id, delta, threshold
```

**Expected**:
- [ ] Alert created when |delta| > threshold
- [ ] Alert contains position details
- [ ] Telegram notification sent (if enabled)

---

### 5. Test Gamma Risk Alert

**Action**: Monitor near-expiration positions (high gamma risk)

**Expected when DTE < 7 days**:
- [ ] Log: "Campaign [id] approaching expiration: DTE=[days]"
- [ ] Log: "Gamma risk increasing: |gamma|=[value]"
- [ ] Log (if gamma > threshold): "ALERT: High gamma risk detected"

**Verification Query**:
```sql
SELECT * FROM alert_history WHERE alert_type = 'GREEKS_GAMMA_HIGH' ORDER BY created_at DESC LIMIT 1;
```

**Expected**:
- [ ] Alert triggered when gamma exceeds threshold
- [ ] Severity = 'warning' or 'critical' (if very close to expiration)

---

### 6. Test Vega Risk Alert (IV Expansion)

**Action**: Monitor during high IV environment or simulate

**Scenario**: Implied volatility increases significantly

**Expected when IV spike**:
- [ ] Log: "IV increase detected: [old_iv] → [new_iv]"
- [ ] Log: "Vega exposure: [value] (per 1% IV change)"
- [ ] Log: "Estimated P&L impact: $[amount]"
- [ ] Log (if vega risk high): "ALERT: High vega exposure"

**Verification**:
- [ ] Vega alert triggered when exposure > threshold
- [ ] Alert includes IV change and P&L estimate

---

### 7. Test Greeks Snapshot to Supervisor

**Action**: Verify cross-service Greeks synchronization

**Expected in logs** (TradingSupervisorService):
- [ ] Log: "Received Greeks snapshot from OptionsExecutionService"
- [ ] Log: "Updating positions_snapshot table"

**Verification Query** (in supervisor.db):
```sql
SELECT * FROM positions_snapshot WHERE campaign_id = '[campaign_id]';
-- Should show same Greeks as options.db positions table
```

**Expected**:
- [ ] Greeks data synchronized to supervisor.db
- [ ] Data matches options.db (eventual consistency)
- [ ] Updated via outbox or direct sync

---

### 8. Test Greeks After Partial Position Close

**Action**: Close one leg of the Iron Condor (e.g., buy back short PUT)

**Expected**:
- [ ] Position quantity updated (e.g., short PUT closed: qty -1 → 0)
- [ ] Greeks recalculated for remaining 3 legs
- [ ] Net delta changes (no longer delta-neutral)
- [ ] Alert triggered if delta threshold exceeded

**Verification Query**:
```sql
-- After closing short PUT
SELECT SUM(delta) AS net_delta FROM positions WHERE campaign_id = '[campaign_id]';
-- Should show net_delta ≠ 0 (no longer neutral)
```

**Expected**:
- [ ] Greeks update reflects partial close
- [ ] Alerts triggered if risk thresholds breached

---

## Success Criteria

- [ ] Greeks calculated on position entry (delta, gamma, theta, vega)
- [ ] Greeks update in real-time as underlying price moves
- [ ] Portfolio-level Greeks aggregation accurate
- [ ] Delta spike alert triggered when threshold exceeded
- [ ] Gamma risk alert triggered near expiration
- [ ] Vega risk alert triggered on IV expansion
- [ ] Greeks synchronized to supervisor service
- [ ] Partial position close recalculates Greeks correctly

---

## Performance Benchmarks

- **Greeks calculation**: < 50ms per position (Black-Scholes)
- **Portfolio aggregation**: < 10ms (SQL SUM query)
- **Real-time update frequency**: Every 60 seconds (configurable)
- **Alert latency**: < 5 seconds from threshold breach to alert creation

---

## Greeks Accuracy Verification

**Manual Comparison** (against IBKR TWS values):

1. Open TWS Portfolio → Position Details
2. Compare Greeks shown in TWS vs database values
3. Expected tolerance: ±5% (due to calculation method differences)

**Formula Reference**:
- Delta: ∂V/∂S (rate of change of option value per $1 move in underlying)
- Gamma: ∂²V/∂S² (rate of change of delta per $1 move)
- Theta: ∂V/∂t (time decay per day)
- Vega: ∂V/∂σ (change per 1% IV move)

**Black-Scholes vs IBKR Model**:
- System uses Black-Scholes-Merton (BSM) model
- IBKR may use proprietary model with adjustments
- Differences expected, especially for:
  - Near expiration (pin risk)
  - Deep ITM/OTM options
  - Dividend adjustments

---

## Cleanup

```sql
-- Reset test deltas (if manually modified)
UPDATE positions SET delta = 0.0, gamma = 0.0, theta = 0.0, vega = 0.0 
WHERE campaign_id = '[campaign_id]';

-- Delete test alerts
DELETE FROM alert_history WHERE alert_type LIKE 'GREEKS_%' AND created_at > datetime('now', '-1 hour');
```

---

## Troubleshooting

### Greeks Not Updating

- Check market data connection (IBKR)
- Verify GreeksMonitorWorker is enabled and running
- Check logs for calculation errors
- Ensure underlying price updates received

### Inaccurate Greeks Values

- Verify input parameters: strike, expiration, IV, underlying price, risk-free rate
- Check for stale market data (cached prices)
- Compare with IBKR TWS (tolerance ±5%)
- Review Black-Scholes implementation for bugs

### Alerts Not Triggering

- Verify thresholds in configuration
- Check alert_history table for existing alerts (may be deduplicated)
- Ensure GreeksMonitorWorker monitoring interval is reasonable
- Check logs for alert creation errors

---

**Test Duration**: 10-15 minutes  
**Last Updated**: 2026-04-05  
**Dependencies**: E2E-04 (Campaign Open), market hours for real-time updates
