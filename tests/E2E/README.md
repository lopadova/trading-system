# End-to-End Test Suite

> Comprehensive E2E testing scenarios for Trading System
> Last updated: 2026-04-05

---

## Overview

This directory contains **10 manual E2E test checklists** and **automated verification scripts** for validating the Trading System in a realistic environment.

### Test Categories

| Test ID | Name | IBKR Required | Duration | Risk Level |
|---------|------|---------------|----------|------------|
| E2E-01 | Startup and IBKR Connection | Yes | 5 min | Low |
| E2E-02 | Sync to Cloudflare | Partial | 5 min | Low |
| E2E-03 | IVTS Monitoring | Yes | 15-30 min | Low |
| E2E-04 | Campaign Open | Yes | 5-15 min | Medium |
| E2E-05 | Greeks Calculation | Yes | 10-15 min | Low |
| E2E-06 | Profit Target Hit | Yes | 10-30 min | Medium |
| E2E-07 | IBKR Disconnect | Yes | 10-15 min | Low |
| E2E-08 | Cloudflare Down | No | 10-15 min | Low |
| E2E-09 | Service Restart | Yes | 5-10 min | Low |
| E2E-10 | Emergency Stop | Yes | 5-10 min | High |

---

## File Structure

```
tests/E2E/
├── README.md                    ← This file
├── E2E-01-Startup.md            ← Manual test checklist
├── E2E-02-Sync.md
├── E2E-03-Ivts.md
├── E2E-04-CampaignOpen.md
├── E2E-05-Greeks.md
├── E2E-06-TargetHit.md
├── E2E-07-IbkrDisconnect.md
├── E2E-08-CfDown.md
├── E2E-09-ServiceRestart.md
├── E2E-10-HardStop.md
├── Automated/                   ← Automated test implementations
│   ├── DatabaseSchemaTests.cs   ← Schema verification
│   ├── ConfigValidationTests.cs ← Config validation
│   ├── MockIbkrTests.cs         ← IBKR mock scenarios
│   └── ApiContractTests.cs      ← API contract tests
└── Scripts/                     ← Bash/PowerShell scripts
    ├── verify-e2e.sh
    ├── verify-e2e.ps1
    └── run-all-e2e.sh
```

---

## Prerequisites

### Required for ALL Tests
- Windows Server 2019+ or Windows 10/11 Pro
- .NET 8 SDK installed
- Trading System built successfully (`dotnet build`)
- Configuration files created (supervisor.json, options.json)

### Required for IBKR Tests (E2E-01, 03, 04, 05, 06, 07, 09, 10)
- Interactive Brokers Paper Trading account
- TWS or IB Gateway running (Paper mode, port 4002)
- TWS API settings configured (Enable ActiveX, Socket Clients, Trusted IP: 127.0.0.1)

### Required for Cloudflare Tests (E2E-02, 08)
- Cloudflare Worker deployed
- D1 database initialized
- Worker URL configured in supervisor.json

---

## Running Manual E2E Tests

### Recommended Sequence

**Phase 1: Basic Functionality** (No trading required)
1. E2E-01: Startup and IBKR Connection
2. E2E-02: Sync to Cloudflare
3. E2E-08: Cloudflare Down (resilience)

**Phase 2: Monitoring** (Read-only IBKR operations)
4. E2E-03: IVTS Monitoring
5. E2E-05: Greeks Calculation (requires active campaign)

**Phase 3: Trading Workflow** (Paper trading required)
6. E2E-04: Campaign Open
7. E2E-06: Profit Target Hit
8. E2E-09: Service Restart (state persistence)

**Phase 4: Resilience** (Advanced scenarios)
9. E2E-07: IBKR Disconnect
10. E2E-10: Emergency Stop (LAST — closes all positions!)

### Individual Test Execution

1. Open test markdown file (e.g., `E2E-01-Startup.md`)
2. Verify prerequisites checklist
3. Follow test steps sequentially
4. Mark each expected outcome with ✅ or ❌
5. Document any deviations in test notes
6. Capture logs and database queries for evidence

### Test Evidence Collection

For each test, collect:
- [ ] Terminal logs (copy/paste or screenshot)
- [ ] Database query results (SQL output)
- [ ] IBKR TWS screenshots (position, orders)
- [ ] Cloudflare Worker logs (if applicable)
- [ ] Alert history (Telegram messages)

Save evidence in: `tests/E2E/evidence/E2E-[NN]-[date]/`

---

## Running Automated Tests

### Automated Test Suite

Tests that **do not require IBKR** connection:

```bash
# Run all automated E2E tests
cd tests/E2E/Automated
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~DatabaseSchemaTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Verification Script

Comprehensive pre-deployment verification:

```bash
# Linux/Mac/Git Bash
cd scripts
./verify-e2e.sh

# PowerShell (Windows)
cd scripts
.\verify-e2e.ps1
```

**Script checks**:
- Database schema integrity (tables, indexes, constraints)
- Configuration file structure
- Service startup (without IBKR)
- Log file creation
- Gitignore rules (secrets protection)

**Output**: `logs/e2e-verification-report.md`

---

## Test Environments

### Local Development
- **Purpose**: Feature development and debugging
- **IBKR**: Paper Trading account
- **Data**: Local SQLite databases (`C:\ProgramData\TradingSystem\`)
- **Worker**: Local dev Worker (`wrangler dev`)

### Staging (Pre-Production)
- **Purpose**: Integration testing and validation
- **IBKR**: Paper Trading account
- **Data**: Separate SQLite databases (staging directory)
- **Worker**: Deployed Worker (staging environment)

### Production
- **Purpose**: Live trading (authorized users only)
- **IBKR**: Live Trading account (DANGER!)
- **Data**: Production SQLite databases (with backups)
- **Worker**: Production Worker (Cloudflare)

⚠️ **NEVER run E2E tests in production environment!**

---

## Pass/Fail Criteria

### Individual Test

**PASS** if:
- All prerequisites met
- All expected outcomes achieved
- No critical errors in logs
- Database state consistent
- Performance within benchmarks

**FAIL** if:
- Any prerequisite missing
- Any expected outcome not achieved
- Critical errors in logs
- Database corruption detected
- Performance degraded beyond threshold

### Full E2E Suite

**PASS** if:
- 10/10 tests PASS
- All automated tests PASS
- Verification script reports READY

**PARTIAL PASS** if:
- 8-9/10 tests PASS (minor issues documented)
- Known issues with workarounds

**FAIL** if:
- < 8/10 tests PASS
- Any critical functionality broken
- Data integrity issues

---

## Common Issues and Solutions

### IBKR Connection Fails
**Symptoms**: E2E-01 fails, "Connection refused" errors

**Solutions**:
1. Verify TWS running on correct port (4002 for Paper)
2. Check TWS API settings: Configuration → API → Settings
3. Enable "ActiveX and Socket Clients"
4. Add 127.0.0.1 to Trusted IPs
5. Restart TWS after changing settings

### Database Lock Errors
**Symptoms**: "Database is locked" errors during tests

**Solutions**:
1. Stop all services before running tests
2. Close DB Browser for SQLite (if open)
3. Kill zombie processes: `Get-Process dotnet | Stop-Process`
4. Check WAL checkpoint: `PRAGMA wal_checkpoint(FULL);`

### Cloudflare Worker Unreachable
**Symptoms**: E2E-02 fails, HTTP errors

**Solutions**:
1. Verify Worker deployed: `bunx wrangler deployments list`
2. Check Worker URL in supervisor.json
3. Test Worker directly: `curl https://[worker-url]/health`
4. Check D1 bindings in wrangler.toml

### Strategy File Validation Fails
**Symptoms**: E2E-04 fails, "Invalid strategy" errors

**Solutions**:
1. Validate JSON syntax: `cat strategies/TEST_IRON_CONDOR.json | jq .`
2. Check required fields: strategyId, name, symbol, legs, exitRules
3. Verify leg structure: action, optionType, strikeSelection
4. Check strategy enabled: `"enabled": true`

### Campaign Not Closing (E2E-06)
**Symptoms**: Profit target hit but campaign remains open

**Solutions**:
1. Check CampaignMonitorWorker is running
2. Verify exit rules in strategy config
3. Check logs for P&L calculation errors
4. Manually verify current_price updates from market data

---

## Test Data Management

### Before Test Suite
```sql
-- Backup existing databases
Copy-Item "C:\ProgramData\TradingSystem\*.db*" "C:\Backup\before-e2e-$(Get-Date -Format 'yyyyMMdd-HHmmss')\"
```

### After Test Suite
```sql
-- Clean test data (optional)
DELETE FROM campaigns WHERE strategy_id LIKE 'TEST_%';
DELETE FROM positions WHERE campaign_id IN (SELECT campaign_id FROM campaigns WHERE strategy_id LIKE 'TEST_%');
DELETE FROM orders WHERE campaign_id IN (SELECT campaign_id FROM campaigns WHERE strategy_id LIKE 'TEST_%');
DELETE FROM alert_history WHERE created_at < datetime('now', '-1 day');
```

### Reset to Clean State
```bash
# Stop services
# Delete all databases
Remove-Item "C:\ProgramData\TradingSystem\*.db*" -Force
# Restart services (migrations will recreate schema)
```

---

## Performance Baselines

| Operation | Expected Duration | Threshold |
|-----------|-------------------|-----------|
| Service startup | < 10 seconds | 15 seconds |
| IBKR connection | < 5 seconds | 10 seconds |
| Database migration | < 1 second | 2 seconds |
| Strategy loading | < 1 second | 2 seconds |
| Campaign creation | < 100ms | 500ms |
| Order submission | < 2 seconds | 5 seconds |
| Order fill (paper) | < 5 seconds | 10 seconds |
| Greeks calculation | < 50ms/position | 200ms |
| Outbox sync | < 1 second/event | 5 seconds |
| Service shutdown | < 5 seconds | 10 seconds |

---

## Reporting Test Results

### Test Summary Format

```
E2E Test Suite Execution Report
Date: 2026-04-05
Tester: [Name]
Environment: [Local/Staging/Production]

Test Results:
✅ E2E-01: Startup (PASS) - 5 minutes
✅ E2E-02: Sync (PASS) - 5 minutes
✅ E2E-03: IVTS (PASS) - 20 minutes
✅ E2E-04: Campaign Open (PASS) - 10 minutes
✅ E2E-05: Greeks (PASS) - 12 minutes
✅ E2E-06: Profit Target (PASS) - 15 minutes
✅ E2E-07: IBKR Disconnect (PASS) - 12 minutes
✅ E2E-08: Cloudflare Down (PASS) - 10 minutes
✅ E2E-09: Service Restart (PASS) - 8 minutes
✅ E2E-10: Emergency Stop (PASS) - 7 minutes

Total Duration: 104 minutes
Pass Rate: 10/10 (100%)

Overall Assessment: READY FOR DEPLOYMENT
```

---

## Next Steps After E2E Testing

1. **Review Logs**: Check for any warnings or anomalies
2. **Performance Analysis**: Compare against baselines
3. **Update Documentation**: Note any deviations or new findings
4. **Knowledge Base**: Add lessons learned to `knowledge/lessons-learned.md`
5. **Deployment Planning**: Schedule production deployment (if all tests pass)

---

## Contact and Support

For E2E test questions or issues:
- Review test markdown file (detailed troubleshooting section)
- Check `docs/TROUBLESHOOTING.md`
- Review logs in `C:\ProgramData\TradingSystem\logs\`
- Check knowledge base: `knowledge/errors-registry.md`

---

**Last Updated**: 2026-04-05  
**Maintained By**: Trading System Development Team
