---
title: "E2E-03: IVTS Monitoring, IVR Calculation, and Alerts"
tags: ["dev", "testing", "ibkr"]
status: reference
audience: ["developer"]
last-reviewed: "2026-04-21"
---

# E2E-03: IVTS Monitoring, IVR Calculation, and Alerts

> Manual test checklist for Implied Volatility Term Structure monitoring
> REQUIRES_PAPER: Yes — IBKR Paper Trading with SPX options data access required

---

## Prerequisites

- [ ] E2E-01 (Startup) completed successfully
- [ ] TradingSupervisorService running
- [ ] IBKR Paper Trading connection active
- [ ] IvtsMonitorWorker enabled in supervisor.json
- [ ] SPX options market data subscription active in IBKR Paper account

**Configuration Check** (`supervisor.json`):
```json
{
  "IvtsMonitor": {
    "Enabled": true,
    "Symbol": "SPX",
    "MonitoringIntervalSeconds": 300,
    "IvrLookbackDays": 252,
    "IvrAlertThreshold": 0.9
  }
}
```

---

## Test Steps

### 1. Verify IVTS Table Schema

**Action**: Query supervisor.db

```sql
SELECT name FROM sqlite_master WHERE type='table' AND name='ivts_snapshots';
-- Should return 1 row

PRAGMA table_info(ivts_snapshots);
-- Should show columns:
--   - snapshot_id (PRIMARY KEY)
--   - symbol (TEXT)
--   - snapshot_time (TEXT)
--   - iv_30d (REAL)
--   - iv_60d (REAL)
--   - iv_90d (REAL)
--   - iv_180d (REAL)
--   - ivr_value (REAL)
--   - term_structure_slope (REAL)
```

**Expected**:
- [ ] Table `ivts_snapshots` exists
- [ ] All required columns present
- [ ] Initial row count is 0

---

### 2. Enable IVTS Monitor and Wait for First Snapshot

**Action**: Ensure IvtsMonitorWorker is enabled, wait 5-10 minutes (market hours required)

**Expected in logs** (within monitoring interval):
- [ ] Log: "IvtsMonitorWorker: Starting IVTS monitoring for SPX"
- [ ] Log: "Requesting implied volatility for SPX 30-day options"
- [ ] Log: "Requesting implied volatility for SPX 60-day options"
- [ ] Log: "Requesting implied volatility for SPX 90-day options"
- [ ] Log: "Requesting implied volatility for SPX 180-day options"
- [ ] Log: "IVTS snapshot captured: IV30=[value], IV60=[value], IV90=[value], IV180=[value]"
- [ ] Log: "IVR calculated: [value] (percentile rank over 252 days)"

**Note**: If running outside market hours (Mon-Fri 9:30-16:00 ET), IBKR may return stale/delayed data or errors.

**Verification Query**:
```sql
SELECT * FROM ivts_snapshots ORDER BY snapshot_time DESC LIMIT 1;
-- Should show:
--   - symbol: 'SPX'
--   - snapshot_time: recent timestamp (ISO 8601)
--   - iv_30d: value > 0.0 (e.g., 0.15 = 15% IV)
--   - iv_60d: value > 0.0
--   - iv_90d: value > 0.0
--   - iv_180d: value > 0.0
--   - ivr_value: 0.0 - 1.0 (percentile rank, or NULL if insufficient history)
--   - term_structure_slope: positive/negative (IV90 - IV30)
```

**Expected**:
- [ ] At least 1 IVTS snapshot recorded
- [ ] All IV values are non-negative
- [ ] Term structure slope calculated (IV90 - IV30)
- [ ] IVR is NULL initially (requires 252 days of history)

---

### 3. Monitor Term Structure Shape

**Action**: Wait for 3-5 snapshots (15-25 minutes with 5-minute interval)

**Verification Query**:
```sql
SELECT 
  snapshot_time,
  iv_30d,
  iv_60d,
  iv_90d,
  iv_180d,
  term_structure_slope,
  CASE 
    WHEN term_structure_slope > 0 THEN 'Normal (Contango)'
    WHEN term_structure_slope < 0 THEN 'Inverted (Backwardation)'
    ELSE 'Flat'
  END AS structure_shape
FROM ivts_snapshots
ORDER BY snapshot_time DESC
LIMIT 10;
```

**Expected**:
- [ ] Multiple snapshots captured over time
- [ ] IV values vary slightly (market-driven changes)
- [ ] Term structure slope shows normal/inverted patterns
- [ ] Normal structure: IV increases with time (IV30 < IV60 < IV90 < IV180)
- [ ] Inverted structure: IV decreases with time (IV30 > IV90, slope negative)

---

### 4. Test IVR Calculation (After 252 Days of History)

**Note**: This test requires 252 days of historical IVTS data. In a real deployment, this accumulates over ~1 year. For testing, you can:
- Insert mock historical data (see SQL below)
- Skip this step and verify in production after 1 year

**Action**: Insert mock historical data

```sql
-- Insert 252 days of mock IVTS snapshots with varying IV30 values
WITH RECURSIVE dates(n) AS (
  SELECT 0
  UNION ALL
  SELECT n + 1 FROM dates WHERE n < 251
)
INSERT INTO ivts_snapshots (snapshot_id, symbol, snapshot_time, iv_30d, iv_60d, iv_90d, iv_180d, ivr_value, term_structure_slope)
SELECT 
  hex(randomblob(16)),
  'SPX',
  datetime('now', '-' || n || ' days'),
  0.10 + (RANDOM() % 20) / 100.0,  -- IV30: 10% - 30%
  0.12 + (RANDOM() % 20) / 100.0,
  0.14 + (RANDOM() % 20) / 100.0,
  0.16 + (RANDOM() % 20) / 100.0,
  NULL,  -- IVR calculated on next snapshot
  (0.14 + (RANDOM() % 20) / 100.0) - (0.10 + (RANDOM() % 20) / 100.0)
FROM dates;
```

**Wait for next snapshot** (within monitoring interval)

**Expected in logs**:
- [ ] Log: "Calculating IVR using 252 historical snapshots"
- [ ] Log: "Current IV30: [value], Historical min: [value], Historical max: [value]"
- [ ] Log: "IVR calculated: [percentile] (e.g., 0.65 = 65th percentile)"

**Verification Query**:
```sql
SELECT ivr_value FROM ivts_snapshots WHERE ivr_value IS NOT NULL ORDER BY snapshot_time DESC LIMIT 1;
-- Should return value between 0.0 and 1.0
```

**Expected**:
- [ ] IVR value calculated (between 0.0 and 1.0)
- [ ] IVR represents percentile rank of current IV30 vs 252-day range
- [ ] High IVR (> 0.8) = IV currently high (relative to history)
- [ ] Low IVR (< 0.2) = IV currently low

---

### 5. Test High IVR Alert Trigger

**Action**: Modify threshold or wait for market conditions where IVR > threshold

**Configuration** (`supervisor.json`):
```json
{
  "IvtsMonitor": {
    "IvrAlertThreshold": 0.9
  }
}
```

**Expected when IVR > 0.9**:
- [ ] Log: "High IVR detected: [value] (threshold: 0.9)"
- [ ] Log: "Creating alert: IVTS_HIGH_IVR"
- [ ] Alert inserted into `alert_history` table

**Verification Query**:
```sql
SELECT * FROM alert_history WHERE alert_type = 'IVTS_HIGH_IVR' ORDER BY created_at DESC LIMIT 5;
-- Should show:
--   - alert_type: 'IVTS_HIGH_IVR'
--   - severity: 'warning' or 'info'
--   - message: Contains IVR value and threshold
--   - metadata: JSON with full IVTS snapshot
```

**Expected**:
- [ ] Alert created when IVR exceeds threshold
- [ ] Alert contains current IVR value
- [ ] Metadata includes IV30, IV60, IV90, IV180

---

### 6. Test Term Structure Inversion Alert

**Action**: Wait for market conditions where term structure inverts (IV30 > IV90)

**Alternative**: Insert mock data with inverted structure

```sql
INSERT INTO ivts_snapshots (snapshot_id, symbol, snapshot_time, iv_30d, iv_60d, iv_90d, iv_180d, ivr_value, term_structure_slope)
VALUES (
  hex(randomblob(16)),
  'SPX',
  datetime('now'),
  0.25,  -- IV30: 25%
  0.22,  -- IV60: 22%
  0.18,  -- IV90: 18% (inverted!)
  0.15,  -- IV180: 15%
  0.85,
  0.18 - 0.25  -- Negative slope
);
```

**Expected in logs** (on next monitoring cycle):
- [ ] Log: "Term structure inversion detected: slope = [negative value]"
- [ ] Log: "Creating alert: IVTS_INVERSION"

**Verification Query**:
```sql
SELECT * FROM alert_history WHERE alert_type = 'IVTS_INVERSION' ORDER BY created_at DESC LIMIT 5;
```

**Expected**:
- [ ] Alert created for term structure inversion
- [ ] Alert message explains inversion (IV30 > IV90)
- [ ] Severity = 'warning' (market stress indicator)

---

### 7. Test IVTS Disabled Mode

**Action**: Disable IVTS monitoring in configuration

```json
{
  "IvtsMonitor": {
    "Enabled": false
  }
}
```

**Restart TradingSupervisorService**

**Expected in logs**:
- [ ] Log: "IvtsMonitorWorker is disabled in configuration"
- [ ] Log: "IvtsMonitorWorker: Skipping execution (disabled)"
- [ ] NO new IVTS snapshots created

**Verification Query** (after 10 minutes):
```sql
SELECT COUNT(*) FROM ivts_snapshots WHERE snapshot_time > datetime('now', '-10 minutes');
-- Should return 0 (no new snapshots)
```

**Expected**:
- [ ] Worker skips execution when disabled
- [ ] No database writes
- [ ] No IBKR market data requests

---

## Success Criteria

- [ ] IVTS snapshots captured every monitoring interval (e.g., 5 minutes)
- [ ] All IV values (30d, 60d, 90d, 180d) populated correctly
- [ ] Term structure slope calculated (IV90 - IV30)
- [ ] IVR calculated when 252+ days of history available
- [ ] High IVR alert triggered when threshold exceeded
- [ ] Term structure inversion alert triggered when slope < 0
- [ ] Disabled mode prevents execution

---

## Performance Benchmarks

- **IVTS snapshot capture**: < 30 seconds (depends on IBKR API latency)
- **IVR calculation**: < 1 second (SQL query over 252 rows)
- **Database writes**: < 50ms per snapshot
- **Memory usage**: < 10 MB (IVTS worker)

---

## Cleanup

```sql
-- Delete test/mock data
DELETE FROM ivts_snapshots WHERE snapshot_time < datetime('now', '-1 day');
DELETE FROM alert_history WHERE alert_type LIKE 'IVTS_%';
```

---

## Troubleshooting

### No IVTS Snapshots Captured

- Verify IBKR connection is active (OptionsExecutionService connected)
- Check market hours (SPX options trade 9:30-16:00 ET Mon-Fri)
- Verify SPX market data subscription in IBKR account
- Check logs for IBKR error codes (e.g., 10197 = no market data permission)

### IV Values Are Zero

- Indicates no market data received
- Check IBKR market data subscriptions
- Verify options chain exists for requested expiration dates
- Try different symbol (e.g., SPY instead of SPX)

### IVR Always NULL

- Requires 252+ days of historical snapshots
- Check: `SELECT COUNT(*) FROM ivts_snapshots;` (should be >= 252)
- For testing, insert mock historical data

---

**Test Duration**: 15-30 minutes (depends on monitoring interval)  
**Last Updated**: 2026-04-05  
**Dependencies**: E2E-01 (Startup), IBKR SPX options data subscription
