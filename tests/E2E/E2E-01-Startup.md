# E2E-01: System Startup and IBKR Connection

> Manual test checklist for system initialization and IBKR Paper Trading connection
> REQUIRES_PAPER: Yes — Interactive Brokers Paper Trading account required

---

## Prerequisites

- [ ] .NET 8 SDK installed (`dotnet --version` → 8.0.x)
- [ ] IBKR Paper Trading account active
- [ ] TWS or IB Gateway running (Paper mode) on localhost:4002
- [ ] TWS API settings: Enable ActiveX, Socket Clients enabled, Trusted IP: 127.0.0.1
- [ ] Configuration files created (supervisor.json, options.json) with paper mode
- [ ] No existing database files (or backup existing .db files)

---

## Test Steps

### 1. Database Initialization

**Action**: Run TradingSupervisorService first time

```powershell
cd src\TradingSupervisorService
dotnet run --project TradingSupervisorService.csproj
```

**Expected**:
- [ ] Console output: "Starting TradingSupervisorService"
- [ ] Migration runner executes: "Running migration SUPERVISOR-001"
- [ ] File created: `C:\ProgramData\TradingSystem\supervisor.db`
- [ ] File created: `C:\ProgramData\TradingSystem\supervisor.db-wal`
- [ ] File created: `C:\ProgramData\TradingSystem\supervisor.db-shm`
- [ ] Console output: "Migrations applied: 1"
- [ ] No errors in logs

**Verification Query**:
```sql
-- Use DB Browser for SQLite to open supervisor.db
SELECT * FROM schema_migrations;
-- Should show 1 row: Version=SUPERVISOR-001, AppliedAt=[timestamp]

SELECT name FROM sqlite_master WHERE type='table';
-- Should show: schema_migrations, service_heartbeats, sync_outbox, alert_history,
--               log_reader_state, ivts_snapshots, positions_snapshot
```

---

### 2. Options Service Database Initialization

**Action**: Stop supervisor service (Ctrl+C), then run OptionsExecutionService

```powershell
cd ..\OptionsExecutionService
dotnet run --project OptionsExecutionService.csproj
```

**Expected**:
- [ ] Console output: "Starting OptionsExecutionService"
- [ ] Migration runner executes: "Running migration OPTIONS-001"
- [ ] File created: `C:\ProgramData\TradingSystem\options.db`
- [ ] File created: `C:\ProgramData\TradingSystem\options.db-wal`
- [ ] Console output: "Migrations applied: 1"

**Verification Query**:
```sql
SELECT * FROM schema_migrations;
-- Should show 1 row: Version=OPTIONS-001, AppliedAt=[timestamp]

SELECT name FROM sqlite_master WHERE type='table';
-- Should show: schema_migrations, campaigns, positions, strategy_cache,
--               contract_cache, market_data_cache, orders
```

---

### 3. IBKR Connection Establishment

**Prerequisites**:
- [ ] TWS Paper Trading running on localhost:4002
- [ ] TWS logged in (paper account)
- [ ] Socket connection enabled in TWS API settings

**Action**: Keep OptionsExecutionService running, observe logs

**Expected within 10 seconds**:
- [ ] Log: "IbkrConnectionWorker: Starting connection attempt"
- [ ] Log: "EClientSocket: Attempting to connect to 127.0.0.1:4002"
- [ ] Log: "Connected to IBKR successfully. Server version: [number]"
- [ ] Log: "Connection status changed: Disconnected → Connecting → Connected"
- [ ] Log: "Requesting account summary..."
- [ ] Log: "Account: [DU-number] Balance: [amount] Currency: USD"
- [ ] NO log entries with "ERROR" or "Exception"

**Verification**:
```powershell
# In TWS, check API → Active Connections
# Should show connection from 127.0.0.1 with client ID 1
```

---

### 4. Supervisor Service Heartbeat

**Action**: Start TradingSupervisorService in separate terminal

```powershell
cd src\TradingSupervisorService
dotnet run --project TradingSupervisorService.csproj
```

**Expected within 60 seconds**:
- [ ] Log: "HeartbeatWorker: Recording heartbeat"
- [ ] Log: "Heartbeat recorded: Hostname=[machine], CPU=[%], RAM=[%]"
- [ ] Log: "Trading mode: Paper"

**Verification Query**:
```sql
-- In supervisor.db
SELECT * FROM service_heartbeats ORDER BY recorded_at DESC LIMIT 5;
-- Should show at least 1 row with:
--   - hostname = [your machine name]
--   - cpu_percent between 0-100
--   - ram_percent between 0-100
--   - trading_mode = 'paper'
--   - recorded_at = [recent timestamp]
```

---

### 5. Configuration Validation

**Action**: Stop both services, modify options.json to invalid trading mode

```json
{
  "TradingMode": "invalid_mode",
  ...
}
```

**Expected on service start**:
- [ ] Service fails to start
- [ ] Log: "Configuration validation failed: TradingMode must be 'paper' or 'live'"
- [ ] Process exits with non-zero exit code

**Action**: Fix configuration back to "paper"

**Expected**:
- [ ] Service starts successfully
- [ ] Log: "Configuration validated: TradingMode=paper"

---

### 6. Log File Creation

**Expected files** (after running both services for 1 minute):
- [ ] `C:\ProgramData\TradingSystem\logs\supervisor-[date].log`
- [ ] `C:\ProgramData\TradingSystem\logs\options-[date].log`
- [ ] Both files contain structured JSON log entries
- [ ] No log entries with Level="Error" (except expected test errors)

**Verification**:
```powershell
Get-Content "C:\ProgramData\TradingSystem\logs\supervisor-*.log" | Select-String "Trading mode" 
# Should show: "Trading mode: paper"

Get-Content "C:\ProgramData\TradingSystem\logs\options-*.log" | Select-String "Connected to IBKR"
# Should show connection confirmation
```

---

## Success Criteria

- [ ] Both databases created with correct schema (verified via SQL queries)
- [ ] IBKR connection established (verified in TWS and logs)
- [ ] First heartbeat recorded (verified in database)
- [ ] Both services running without errors for 60+ seconds
- [ ] Log files created with structured JSON entries
- [ ] Configuration validation prevents invalid modes
- [ ] All file paths use configured data directory (`C:\ProgramData\TradingSystem`)

---

## Cleanup

```powershell
# Stop both services (Ctrl+C in each terminal)

# Optional: Delete test data
Remove-Item "C:\ProgramData\TradingSystem\*.db*" -Force
Remove-Item "C:\ProgramData\TradingSystem\logs\*" -Force
```

---

## Troubleshooting

### IBKR Connection Fails

- Verify TWS is running on correct port (4002 for paper, 4001 for paper gateway)
- Check TWS API settings: Configuration → API → Settings → Enable ActiveX and Socket Clients
- Add 127.0.0.1 to Trusted IPs in TWS API settings
- Ensure no firewall blocking localhost:4002

### Database Lock Errors

- Stop all services before deleting database files
- Check for zombie processes: `Get-Process dotnet`
- WAL mode requires all connections closed before file deletion

### Migration Fails

- Check disk space (SQLite requires write access)
- Verify data directory exists and is writable
- Check logs for SQL syntax errors

---

**Test Duration**: ~5 minutes  
**Last Updated**: 2026-04-05  
**Dependencies**: None (first test in sequence)
