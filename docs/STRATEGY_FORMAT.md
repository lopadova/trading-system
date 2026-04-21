---
title: "Strategy File Format"
tags: ["reference", "dev", "ibkr"]
aliases: ["Strategy Format", "SDF"]
status: current
audience: ["developer", "operator"]
last-reviewed: "2026-04-21"
related:
  - "[[Trading System Architecture|ARCHITECTURE]]"
  - "[[Configuration Reference|CONFIGURATION]]"
  - "[[Getting Started with Trading System|GETTING_STARTED]]"
---

# Strategy File Format

> Complete reference for strategy JSON files
> Last updated: 2026-04-05

---

## Table of Contents

- [Overview](#overview)
- [File Structure](#file-structure)
- [Strategy Metadata](#strategy-metadata)
- [Underlying Configuration](#underlying-configuration)
- [Entry Rules](#entry-rules)
- [Position Configuration](#position-configuration)
- [Exit Rules](#exit-rules)
- [Risk Management](#risk-management)
- [Strategy Examples](#strategy-examples)
- [Validation Rules](#validation-rules)

---

## Overview

Strategy files define automated trading rules in JSON format. The system loads, validates, and executes strategies according to the rules specified.

### File Location

```
strategies/
├── examples/                     # Example strategies (committed to git)
│   ├── example-put-spread.json
│   └── example-iron-condor.json
└── private/                      # Your strategies (git-ignored)
    └── my-strategy.json
```

**IMPORTANT**: Files in `strategies/private/` are automatically git-ignored. Never commit real trading strategies.

### File Naming

- Use descriptive names: `spy-bull-put-30-delta.json`
- Lowercase with hyphens recommended
- No spaces in filenames

---

## File Structure

### Complete Example

```json
{
  "strategyName": "SPY Bull Put Spread",
  "description": "30-delta bull put spread on SPY, 30-45 DTE",
  "tradingMode": "paper",
  "underlying": {
    "symbol": "SPY",
    "exchange": "SMART",
    "currency": "USD"
  },
  "entryRules": {
    "marketConditions": {
      "minDaysToExpiration": 30,
      "maxDaysToExpiration": 45,
      "ivRankMin": 30,
      "ivRankMax": 70
    },
    "timing": {
      "entryTimeStart": "09:35:00",
      "entryTimeEnd": "15:30:00",
      "daysOfWeek": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
    }
  },
  "position": {
    "type": "BullPutSpread",
    "legs": [
      {
        "action": "SELL",
        "right": "PUT",
        "strikeSelectionMethod": "DELTA",
        "strikeValue": -0.30,
        "quantity": 1
      },
      {
        "action": "BUY",
        "right": "PUT",
        "strikeSelectionMethod": "OFFSET",
        "strikeOffset": -5,
        "quantity": 1
      }
    ],
    "maxPositions": 3,
    "capitalPerPosition": 1000
  },
  "exitRules": {
    "profitTarget": 0.50,
    "stopLoss": 2.00,
    "maxDaysInTrade": 21,
    "exitTimeOfDay": "15:45:00"
  },
  "riskManagement": {
    "maxTotalCapitalAtRisk": 5000,
    "maxDrawdownPercent": 10.0,
    "maxDailyLoss": 500
  }
}
```

---

## Strategy Metadata

### strategyName

**Type**: `string` (required)

**Description**: Human-readable name for the strategy

**Rules**:
- 1-200 characters
- Unique identifier for this strategy
- Used in logs, alerts, and dashboard

**Example**:
```json
"strategyName": "SPY Iron Condor - High IV"
```

---

### description

**Type**: `string` (required)

**Description**: Detailed explanation of strategy logic and goals

**Rules**:
- 1-1000 characters
- Document assumptions, market conditions, and expected behavior

**Example**:
```json
"description": "Sell iron condor on SPY when IV rank is high (>50). Target 50% profit in 21 days or less."
```

---

### tradingMode

**Type**: `string` (required)

**Description**: Execution mode for this strategy

**Values**:
- `"paper"` - Paper trading only (safe)
- `"live"` - Live trading (DANGEROUS, requires validator modification)

**Rules**:
- Must match service configuration `TradingMode`
- Default validator REJECTS `live` mode (safety feature)

**Example**:
```json
"tradingMode": "paper"
```

---

## Underlying Configuration

### underlying

**Type**: `object` (required)

**Description**: Defines the underlying security for options

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `symbol` | string | Yes | Ticker symbol (e.g., "SPY", "SPX", "QQQ") |
| `exchange` | string | Yes | Exchange routing (use "SMART" for auto-routing) |
| `currency` | string | Yes | Currency code (typically "USD") |

**Example**:
```json
"underlying": {
  "symbol": "SPY",
  "exchange": "SMART",
  "currency": "USD"
}
```

**Supported Exchanges**:
- `"SMART"` - IBKR smart routing (recommended)
- `"CBOE"` - Chicago Board Options Exchange
- `"ISE"` - International Securities Exchange
- Others per IBKR documentation

---

## Entry Rules

### entryRules

**Type**: `object` (required)

**Description**: Conditions that must be met to enter a trade

---

#### marketConditions

**Type**: `object` (required)

**Description**: Market environment filters

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `minDaysToExpiration` | int | Yes | Minimum days until expiration (DTE) |
| `maxDaysToExpiration` | int | Yes | Maximum DTE |
| `ivRankMin` | decimal | Yes | Minimum IV Rank (0-100) |
| `ivRankMax` | decimal | Yes | Maximum IV Rank (0-100) |

**Example**:
```json
"marketConditions": {
  "minDaysToExpiration": 30,
  "maxDaysToExpiration": 45,
  "ivRankMin": 30,
  "ivRankMax": 70
}
```

**Notes**:
- **DTE**: Days until option expiration. 30-45 is common for monthly strategies.
- **IV Rank**: Implied Volatility Rank (0-100). 50+ is considered "high IV".
- Strategy only enters when IV is between min and max (e.g., avoid extreme low/high IV)

---

#### timing

**Type**: `object` (required)

**Description**: Time-based entry restrictions

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `entryTimeStart` | string | Yes | Earliest entry time (HH:MM:SS, market time) |
| `entryTimeEnd` | string | Yes | Latest entry time (HH:MM:SS, market time) |
| `daysOfWeek` | array | Yes | Allowed days ("Monday", "Tuesday", ..., "Friday") |

**Example**:
```json
"timing": {
  "entryTimeStart": "09:35:00",
  "entryTimeEnd": "15:30:00",
  "daysOfWeek": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
}
```

**Notes**:
- `09:35:00` - Wait 5 minutes after market open (avoid volatility)
- `15:30:00` - Stop entries 1 hour before close
- Use empty array `[]` for "any day" (not recommended)

---

## Position Configuration

### position

**Type**: `object` (required)

**Description**: Defines the options position structure

---

#### type

**Type**: `string` (required)

**Description**: Position type identifier

**Values**:
- `"BullPutSpread"` - Credit spread (bullish)
- `"BearCallSpread"` - Credit spread (bearish)
- `"IronCondor"` - 4-leg neutral strategy
- `"ButterflySpread"` - 4-leg neutral strategy
- `"Calendar"` - Different expirations
- `"Straddle"` - ATM call + put
- `"Strangle"` - OTM call + put
- `"Custom"` - User-defined

**Example**:
```json
"type": "BullPutSpread"
```

---

#### legs

**Type**: `array` (required)

**Description**: Array of option legs defining the position

**Leg Object**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `action` | string | Yes | "BUY" or "SELL" |
| `right` | string | Yes | "CALL" or "PUT" |
| `strikeSelectionMethod` | string | Yes | "DELTA", "OFFSET", "PERCENT", "FIXED" |
| `strikeValue` | decimal | Conditional | For DELTA (-1.0 to 1.0) or PERCENT (0-100) |
| `strikeOffset` | decimal | Conditional | For OFFSET (relative to reference leg) |
| `strikePrice` | decimal | Conditional | For FIXED (exact strike price) |
| `quantity` | int | Yes | Number of contracts (positive integer) |

**Strike Selection Methods**:

1. **DELTA**: Select strike by option delta
   ```json
   {
     "strikeSelectionMethod": "DELTA",
     "strikeValue": -0.30  // 30-delta put (negative for puts)
   }
   ```

2. **OFFSET**: Relative to another leg's strike
   ```json
   {
     "strikeSelectionMethod": "OFFSET",
     "strikeOffset": -5  // $5 below reference strike
   }
   ```

3. **PERCENT**: Strike as % of underlying price
   ```json
   {
     "strikeSelectionMethod": "PERCENT",
     "strikeValue": 95  // 95% of current price (5% OTM put)
   }
   ```

4. **FIXED**: Exact strike price
   ```json
   {
     "strikeSelectionMethod": "FIXED",
     "strikePrice": 450.0  // Exactly $450 strike
   }
   ```

**Example - Bull Put Spread**:
```json
"legs": [
  {
    "action": "SELL",
    "right": "PUT",
    "strikeSelectionMethod": "DELTA",
    "strikeValue": -0.30,
    "quantity": 1
  },
  {
    "action": "BUY",
    "right": "PUT",
    "strikeSelectionMethod": "OFFSET",
    "strikeOffset": -5,
    "quantity": 1
  }
]
```

**Example - Iron Condor**:
```json
"legs": [
  {
    "action": "SELL",
    "right": "PUT",
    "strikeSelectionMethod": "DELTA",
    "strikeValue": -0.20,
    "quantity": 1
  },
  {
    "action": "BUY",
    "right": "PUT",
    "strikeSelectionMethod": "OFFSET",
    "strikeOffset": -5,
    "quantity": 1
  },
  {
    "action": "SELL",
    "right": "CALL",
    "strikeSelectionMethod": "DELTA",
    "strikeValue": 0.20,
    "quantity": 1
  },
  {
    "action": "BUY",
    "right": "CALL",
    "strikeSelectionMethod": "OFFSET",
    "strikeOffset": 5,
    "quantity": 1
  }
]
```

---

#### maxPositions

**Type**: `int` (required)

**Description**: Maximum concurrent positions for this strategy

**Range**: 1-100

**Example**:
```json
"maxPositions": 3
```

**Notes**:
- Limits risk exposure
- Strategy won't enter new trades if max reached
- Consider capital requirements (3 positions × capital per position)

---

#### capitalPerPosition

**Type**: `decimal` (required)

**Description**: Maximum capital allocated per position (USD)

**Range**: 100-1000000

**Example**:
```json
"capitalPerPosition": 1000
```

**Notes**:
- Used for position sizing
- Actual capital used may be less (based on strategy credit/debit)
- Total capital at risk = `maxPositions × capitalPerPosition`

---

## Exit Rules

### exitRules

**Type**: `object` (required)

**Description**: Conditions for closing positions

---

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `profitTarget` | decimal | Yes | Close when position value / max risk >= this ratio (0.0-1.0 = 0-100%) |
| `stopLoss` | decimal | Yes | Close when position value / max risk >= this ratio (>1.0 = loss threshold) |
| `maxDaysInTrade` | int | Yes | Maximum days to hold position (DTE protection) |
| `exitTimeOfDay` | string | Yes | Close all positions by this time (HH:MM:SS) |

**Example**:
```json
"exitRules": {
  "profitTarget": 0.50,
  "stopLoss": 2.00,
  "maxDaysInTrade": 21,
  "exitTimeOfDay": "15:45:00"
}
```

**Exit Logic**:

1. **Profit Target**: Close when profit >= 50% of max profit
   - For credit spread with $100 credit: close when can buy back for $50 (50% profit)

2. **Stop Loss**: Close when loss >= 200% of max profit (2× initial credit)
   - For $100 credit spread: close if loss reaches $200

3. **Max Days in Trade**: Close if position held for 21+ days
   - Prevents assignment risk near expiration
   - Typical: 21 days for 45 DTE entry (exit at ~24 DTE)

4. **Exit Time of Day**: Market-on-close order at 15:45
   - Ensures all positions closed before expiration Friday

**Multiple Exit Conditions**: First condition met triggers exit (OR logic).

---

## Risk Management

### riskManagement

**Type**: `object` (required)

**Description**: Portfolio-level risk controls

---

**Fields**:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `maxTotalCapitalAtRisk` | decimal | Yes | Total capital across all positions (USD) |
| `maxDrawdownPercent` | decimal | Yes | Max allowed drawdown from peak (0-100) |
| `maxDailyLoss` | decimal | Yes | Max loss per day (USD, stops new entries) |

**Example**:
```json
"riskManagement": {
  "maxTotalCapitalAtRisk": 5000,
  "maxDrawdownPercent": 10.0,
  "maxDailyLoss": 500
}
```

**Risk Checks**:

1. **maxTotalCapitalAtRisk**:
   - Sum of all position capital <= this value
   - Prevents over-leveraging account

2. **maxDrawdownPercent**:
   - If account drops 10% from peak, stop all new entries
   - Existing positions remain open (exit per exitRules)

3. **maxDailyLoss**:
   - If daily loss >= $500, no new entries today
   - Resets at midnight (market time)

**All conditions must pass for new entry to be allowed.**

---

## Strategy Examples

### Example 1: Conservative Bull Put Spread

```json
{
  "strategyName": "SPY Conservative Bull Put",
  "description": "Low-risk bull put spread with 20-delta short put",
  "tradingMode": "paper",
  "underlying": {
    "symbol": "SPY",
    "exchange": "SMART",
    "currency": "USD"
  },
  "entryRules": {
    "marketConditions": {
      "minDaysToExpiration": 35,
      "maxDaysToExpiration": 50,
      "ivRankMin": 40,
      "ivRankMax": 80
    },
    "timing": {
      "entryTimeStart": "10:00:00",
      "entryTimeEnd": "14:00:00",
      "daysOfWeek": ["Monday", "Wednesday"]
    }
  },
  "position": {
    "type": "BullPutSpread",
    "legs": [
      {
        "action": "SELL",
        "right": "PUT",
        "strikeSelectionMethod": "DELTA",
        "strikeValue": -0.20,
        "quantity": 1
      },
      {
        "action": "BUY",
        "right": "PUT",
        "strikeSelectionMethod": "OFFSET",
        "strikeOffset": -10,
        "quantity": 1
      }
    ],
    "maxPositions": 2,
    "capitalPerPosition": 2000
  },
  "exitRules": {
    "profitTarget": 0.60,
    "stopLoss": 1.50,
    "maxDaysInTrade": 28,
    "exitTimeOfDay": "15:50:00"
  },
  "riskManagement": {
    "maxTotalCapitalAtRisk": 4000,
    "maxDrawdownPercent": 8.0,
    "maxDailyLoss": 300
  }
}
```

---

### Example 2: Iron Condor

```json
{
  "strategyName": "SPX Iron Condor - Narrow",
  "description": "High-probability iron condor on SPX, 10-delta wings",
  "tradingMode": "paper",
  "underlying": {
    "symbol": "SPX",
    "exchange": "CBOE",
    "currency": "USD"
  },
  "entryRules": {
    "marketConditions": {
      "minDaysToExpiration": 30,
      "maxDaysToExpiration": 45,
      "ivRankMin": 50,
      "ivRankMax": 90
    },
    "timing": {
      "entryTimeStart": "09:45:00",
      "entryTimeEnd": "15:00:00",
      "daysOfWeek": ["Tuesday", "Thursday"]
    }
  },
  "position": {
    "type": "IronCondor",
    "legs": [
      {
        "action": "SELL",
        "right": "PUT",
        "strikeSelectionMethod": "DELTA",
        "strikeValue": -0.10,
        "quantity": 1
      },
      {
        "action": "BUY",
        "right": "PUT",
        "strikeSelectionMethod": "OFFSET",
        "strikeOffset": -25,
        "quantity": 1
      },
      {
        "action": "SELL",
        "right": "CALL",
        "strikeSelectionMethod": "DELTA",
        "strikeValue": 0.10,
        "quantity": 1
      },
      {
        "action": "BUY",
        "right": "CALL",
        "strikeSelectionMethod": "OFFSET",
        "strikeOffset": 25,
        "quantity": 1
      }
    ],
    "maxPositions": 5,
    "capitalPerPosition": 5000
  },
  "exitRules": {
    "profitTarget": 0.50,
    "stopLoss": 2.50,
    "maxDaysInTrade": 21,
    "exitTimeOfDay": "15:45:00"
  },
  "riskManagement": {
    "maxTotalCapitalAtRisk": 25000,
    "maxDrawdownPercent": 12.0,
    "maxDailyLoss": 1000
  }
}
```

---

## Validation Rules

Strategy files are validated at load time. Invalid strategies are rejected with detailed error messages.

### Required Fields

All fields marked **Required** must be present. Missing fields → validation fails.

### Data Type Validation

- Strings must be non-empty (except where noted)
- Numbers must be in valid ranges
- Booleans must be `true` or `false`
- Arrays must contain valid elements

### Business Logic Validation

| Rule | Error Message |
|------|---------------|
| `strategyName` must be 1-200 chars | "Strategy name too long" |
| `minDaysToExpiration` <= `maxDaysToExpiration` | "DTE range invalid" |
| `ivRankMin` <= `ivRankMax` | "IV rank range invalid" |
| `ivRankMin` and `ivRankMax` in 0-100 | "IV rank out of range" |
| `entryTimeStart` < `entryTimeEnd` | "Entry time window invalid" |
| `profitTarget` in 0.0-1.0 | "Profit target must be 0-100%" |
| `stopLoss` >= 1.0 | "Stop loss must be >= 100% (loss threshold)" |
| `maxPositions` >= 1 | "Must allow at least 1 position" |
| `capitalPerPosition` > 0 | "Capital must be positive" |
| Legs array not empty | "Position must have at least 1 leg" |
| All legs have required fields | "Leg missing required field" |
| `strikeSelectionMethod` matches required fields | "DELTA requires strikeValue" |
| `quantity` > 0 | "Quantity must be positive" |

### Cross-Field Validation

| Rule | Error Message |
|------|---------------|
| `maxTotalCapitalAtRisk` >= `maxPositions` × `capitalPerPosition` | "Total capital insufficient for max positions" |
| `tradingMode` matches service config | "Strategy mode mismatch with service" |
| OFFSET legs reference valid preceding leg | "Invalid strike offset reference" |

### Validation Errors

Validation returns `ValidationResult`:

```csharp
public record ValidationResult(bool IsValid, string[] Errors);
```

**Example Error**:
```json
{
  "IsValid": false,
  "Errors": [
    "minDaysToExpiration (50) cannot be greater than maxDaysToExpiration (30)",
    "profitTarget (1.5) must be between 0.0 and 1.0",
    "Leg 1: strikeSelectionMethod is DELTA but strikeValue is missing"
  ]
}
```

Service logs all errors and rejects strategy.

---

## Testing Strategies

### 1. Use Example Strategies First

Start with `strategies/examples/*.json` to learn the format. These are tested and safe.

### 2. Validate JSON Syntax

Use a JSON validator before loading:
- https://jsonlint.com/
- VS Code JSON schema validation

### 3. Test in Paper Mode

**ALWAYS** test new strategies in paper trading:
1. Set `"tradingMode": "paper"` in strategy file
2. Verify service config has `"TradingMode": "paper"`
3. Run for several days to observe behavior
4. Check logs for entry/exit decisions

### 4. Monitor Campaign State

```powershell
sqlite3 data/options.db "SELECT strategy_name, state, created_at FROM campaigns ORDER BY created_at DESC LIMIT 10;"
```

Check campaigns transition through states: Pending → Active → Closed

### 5. Review Position Tracking

```powershell
sqlite3 data/options.db "SELECT symbol, quantity, entry_price, current_price, unrealized_pnl FROM positions;"
```

Verify positions match strategy rules.

---

## Common Mistakes

### 1. DTE Range Too Narrow

**Problem**: `minDaysToExpiration: 30, maxDaysToExpiration: 32`

**Issue**: Only 2-day window, may miss entries

**Fix**: Use wider range (e.g., 30-45)

---

### 2. IV Rank Impossible Range

**Problem**: `ivRankMin: 80, ivRankMax: 70`

**Issue**: Min > Max, always fails

**Fix**: Swap values or adjust to sensible range

---

### 3. Profit Target > 100%

**Problem**: `profitTarget: 1.5`

**Issue**: Can't capture 150% of max profit

**Fix**: Use 0.0-1.0 (0-100%)

---

### 4. Stop Loss < 100%

**Problem**: `stopLoss: 0.5`

**Issue**: Triggers at 50% profit (inverted logic)

**Fix**: Use >= 1.0 (e.g., 2.0 = 200% of credit = 100% loss)

---

### 5. OFFSET Without Reference Leg

**Problem**: First leg uses OFFSET

**Issue**: No preceding leg to reference

**Fix**: First leg must use DELTA, PERCENT, or FIXED

---

## Advanced Topics

### Custom Strategy Types

To add new strategy types, modify `StrategyValidator.cs`:

```csharp
private static readonly string[] ValidTypes = {
    "BullPutSpread",
    "BearCallSpread",
    "IronCondor",
    "MyCustomType"  // Add here
};
```

### Dynamic Position Sizing

Future enhancement: Calculate `capitalPerPosition` dynamically based on account balance.

**Placeholder** (not yet implemented):
```json
"position": {
  "capitalAllocationMethod": "FIXED",  // or "PERCENT_OF_ACCOUNT"
  "capitalPerPosition": 1000,
  "capitalPercent": 2.0  // Use 2% of account per position
}
```

### Greeks-Based Entries

Future enhancement: Enter based on portfolio Greeks.

**Placeholder** (not yet implemented):
```json
"entryRules": {
  "greeksConditions": {
    "maxPortfolioDelta": 0.50,
    "maxPortfolioTheta": 100.0
  }
}
```

---

## References

- [Configuration Reference](./CONFIGURATION.md) - Service configuration
- [Architecture](./ARCHITECTURE.md) - How strategies are executed
- [Getting Started](./GETTING_STARTED.md) - How to deploy strategies

---

*Last updated: 2026-04-05 | Trading System v1.0*
