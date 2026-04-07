# Troubleshooting Guide

> Common issues and solutions
> Last updated: 2026-04-05

---

## Table of Contents

- [Service Issues](#service-issues)
- [IBKR Connection Issues](#ibkr-connection-issues)
- [Database Issues](#database-issues)
- [Build and Test Issues](#build-and-test-issues)
- [Configuration Issues](#configuration-issues)
- [Deployment Issues](#deployment-issues)
- [Dashboard Issues](#dashboard-issues)
- [Cloudflare Worker Issues](#cloudflare-worker-issues)
- [Logs and Diagnostics](#logs-and-diagnostics)

---

## Service Issues

### Service Won't Start

**Symptom**: Service starts then immediately stops, or fails to start

**Possible Causes**:

1. **Database file locked**
   ```powershell
   # Solution: Close any SQLite browser/viewer
   # Then restart service
   Restart-Service TradingSupervisorService
   ```

2. **Invalid configuration**
   ```powershell
   # Check logs for validation errors
   Get-Content logs/supervisor-*.log -Tail 50 | Select-String "ERROR"
   
   # Common errors:
   # - Missing required config fields
   # - Invalid port numbers
   # - File paths that don't exist
   ```

3. **Missing dependencies**
   ```powershell
   # Verify .NET runtime is installed
   dotnet --version
   # Should output 10.0.x
   
   # If missing, install .NET 10 SDK
   # https://dotnet.microsoft.com/download/dotnet/10.0
   ```

4. **Port conflict**
   ```powershell
   # Check if port is already in use
   netstat -ano | findstr :7497
   
   # If another process is using the port:
   # - Change ClientId in appsettings.json
   # - Or use different port
   ```

---

### Service Crashes After Running

**Symptom**: Service runs for a while, then crashes

**Diagnostics**:

1. **Check Event Viewer**
   ```powershell
   # Open Event Viewer
   eventvwr.msc
   
   # Navigate to: Windows Logs > Application
   # Look for errors from "TradingSupervisorService" or "OptionsExecutionService"
   ```

2. **Check service logs**
   ```powershell
   # Look for unhandled exceptions
   Get-Content logs/supervisor-*.log | Select-String "Critical|Unhandled"
   ```

3. **Common Causes**:
   - Disk full (check `DiskThresholdGb` alert)
   - Out of memory (check RAM usage)
   - IBKR disconnect loop (check reconnection logs)
   - Database corruption (see Database Issues below)

---

### Service Runs But No Activity

**Symptom**: Service is running, but no heartbeats/trades/alerts

**Diagnostics**:

1. **Check if workers started**
   ```powershell
   Get-Content logs/supervisor-*.log | Select-String "Worker.*started"
   
   # Should see:
   # [INFO] HeartbeatWorker started
   # [INFO] OutboxSyncWorker started
   # etc.
   ```

2. **Check database for heartbeats**
   ```powershell
   sqlite3 data/supervisor.db "SELECT service_name, last_seen_at FROM service_heartbeats ORDER BY last_seen_at DESC;"
   
   # Should show recent timestamps (within last 60 seconds)
   ```

3. **Verify configuration**
   ```powershell
   # Check if workers are enabled
   Get-Content src/TradingSupervisorService/appsettings.json | Select-String "Enabled"
   
   # Some workers (IVTS) are disabled by default
   ```

---

## IBKR Connection Issues

### Connection Refused

**Symptom**: Logs show "Connection refused" or "No connection could be made"

**Solutions**:

1. **Verify TWS/Gateway is running**
   - Open TWS or IB Gateway
   - Ensure logged in to paper trading account
   - Check status bar shows "Connected"

2. **Check API settings**
   - TWS: Configure → API → Settings
   - Enable "ActiveX and Socket Clients"
   - Verify "Socket port" matches config (7497 for paper TWS, 4002 for paper Gateway)
   - Disable "Read-Only API" if enabled

3. **Verify port number**
   ```json
   // In appsettings.json
   "IBKR": {
     "PaperPort": 7497,  // TWS paper
     // OR
     "PaperPort": 4002   // Gateway paper
   }
   ```

4. **Check firewall**
   ```powershell
   # Allow localhost connections
   # Usually not blocked, but verify:
   Test-NetConnection -ComputerName 127.0.0.1 -Port 7497
   ```

---

### Connection Drops Frequently

**Symptom**: IBKR connection established, then drops every few minutes

**Solutions**:

1. **Check keepalive settings**
   ```json
   "IBKR": {
     "KeepaliveIntervalSeconds": 60  // Send heartbeat every 60s
   }
   ```

2. **Check TWS/Gateway stability**
   - Restart TWS/Gateway
   - Update to latest version
   - Check TWS logs: Help → Log Viewer

3. **Network issues**
   - Check internet connection stability
   - Verify no VPN disconnects
   - Check router/firewall logs

4. **Check reconnection logic**
   ```powershell
   # Logs should show automatic reconnection
   Get-Content logs/options-execution-*.log | Select-String "Reconnect"
   
   # Should see exponential backoff: 5s, 10s, 20s, etc.
   ```

---

### ClientId Conflict

**Symptom**: "Client with ID X is already connected"

**Solutions**:

1. **Use unique ClientId for each service**
   ```json
   // TradingSupervisorService
   "IBKR": { "ClientId": 1 }
   
   // OptionsExecutionService
   "IBKR": { "ClientId": 2 }
   ```

2. **Check for other TWS API clients**
   - Excel with RTD connection
   - Other trading apps
   - Duplicate service instance

3. **Restart TWS/Gateway** to clear all connections

---

### Market Data Not Updating

**Symptom**: No market data in positions table, or stale Greeks

**Solutions**:

1. **Check market data subscriptions**
   - IBKR account must have real-time data subscription
   - Paper accounts get delayed data (15-20 minutes)
   - Verify subscription: Account → Market Data Subscriptions

2. **Check connection state**
   ```powershell
   Get-Content logs/options-execution-*.log | Select-String "Market data|reqMktData"
   ```

3. **IBKR sends -1, -2, -999 for unavailable Greeks**
   - This is normal for some options
   - System filters NULL values in queries

---

## Database Issues

### Database Locked

**Symptom**: "Database is locked" or "Database table is locked"

**Solutions**:

1. **Close SQLite browser/viewer**
   - DB Browser for SQLite
   - sqlite3 command-line (if interactive mode running)

2. **Stop services before manual queries**
   ```powershell
   Stop-Service TradingSupervisorService, OptionsExecutionService
   sqlite3 data/supervisor.db "SELECT * FROM ..."
   Start-Service TradingSupervisorService, OptionsExecutionService
   ```

3. **Check WAL file**
   ```powershell
   # WAL mode uses -wal and -shm files
   dir data/*.db*
   
   # If corrupted, checkpoint and delete:
   sqlite3 data/supervisor.db "PRAGMA wal_checkpoint(FULL);"
   Remove-Item data/*.db-wal, data/*.db-shm
   ```

---

### Migration Fails

**Symptom**: "Migration failed" or "Table already exists"

**Solutions**:

1. **Check schema_migrations table**
   ```powershell
   sqlite3 data/supervisor.db "SELECT * FROM schema_migrations ORDER BY version;"
   
   # Shows applied migrations
   ```

2. **Manual migration recovery**
   ```powershell
   # If migration partially applied:
   # 1. Identify failed migration version (check logs)
   # 2. Remove from schema_migrations
   sqlite3 data/supervisor.db "DELETE FROM schema_migrations WHERE version = 2;"
   
   # 3. Restart service (will retry migration)
   ```

3. **Nuclear option: Delete and recreate**
   ```powershell
   Stop-Service TradingSupervisorService, OptionsExecutionService
   Remove-Item data/*.db, data/*.db-wal, data/*.db-shm
   Start-Service TradingSupervisorService, OptionsExecutionService
   # Databases recreated with all migrations
   ```

---

### Database Corruption

**Symptom**: "Database disk image is malformed" or "Corruption detected"

**Solutions**:

1. **Attempt recovery**
   ```powershell
   # Dump to SQL, recreate database
   sqlite3 data/supervisor.db ".dump" > supervisor.sql
   Remove-Item data/supervisor.db*
   sqlite3 data/supervisor.db < supervisor.sql
   ```

2. **Restore from backup**
   ```powershell
   # If backups enabled:
   Copy-Item backups/supervisor-YYYYMMDD.db data/supervisor.db
   ```

3. **Start fresh** (loses historical data)
   ```powershell
   # Last resort:
   Remove-Item data/*.db*
   # Restart services (will recreate)
   ```

---

## Build and Test Issues

### Build Fails

**Symptom**: `dotnet build` shows compilation errors

**Solutions**:

1. **Check .NET SDK version**
   ```powershell
   dotnet --version
   # Must be 10.0.x
   
   # List all SDKs:
   dotnet --list-sdks
   ```

2. **Restore packages**
   ```powershell
   dotnet clean
   dotnet restore
   dotnet build
   ```

3. **Check for package conflicts**
   ```powershell
   # If package restore errors:
   Remove-Item -Recurse -Force ~/.nuget/packages
   dotnet restore
   ```

---

### Tests Fail

**Symptom**: `dotnet test` shows failures

**Common Failures**:

1. **Database locked in tests**
   ```
   Solution: Tests use in-memory databases
   Check that no test is holding connection open
   ```

2. **Timing-sensitive tests fail**
   ```
   Symptom: Worker tests timeout
   Solution: Increase test timeout or use deterministic delays
   ```

3. **Platform-specific failures**
   ```
   Symptom: PerformanceCounter tests fail on non-Windows
   Solution: Tests are Windows-only (skip on other platforms)
   ```

**Debug Single Test**:
```powershell
dotnet test --filter "FullyQualifiedName~HeartbeatRepositoryTests.SaveHeartbeatAsync"
```

---

## Configuration Issues

### Configuration Not Loading

**Symptom**: Service ignores appsettings.json changes

**Solutions**:

1. **Check JSON syntax**
   - Use https://jsonlint.com/ to validate
   - Common errors: missing comma, trailing comma

2. **Verify file location**
   ```powershell
   # appsettings.json must be in:
   # - src/TradingSupervisorService/ (for dotnet run)
   # - bin/Release/net10.0/win-x64/publish/ (for deployed service)
   ```

3. **Check environment**
   ```powershell
   # Verify which environment is active
   $env:DOTNET_ENVIRONMENT
   
   # Service loads appsettings.{Environment}.json
   # Production overrides Development
   ```

4. **Restart service after config change**
   ```powershell
   Restart-Service TradingSupervisorService
   ```

---

### Secrets Not Working

**Symptom**: Environment variables or User Secrets not applied

**Solutions**:

1. **Verify environment variable syntax**
   ```powershell
   # Correct format:
   $env:Cloudflare__ApiKey="secret"  # Double underscore
   
   # Wrong:
   $env:Cloudflare:ApiKey="secret"  # Colon doesn't work in PowerShell
   ```

2. **Check User Secrets**
   ```powershell
   cd src/TradingSupervisorService
   dotnet user-secrets list
   
   # If empty, initialize:
   dotnet user-secrets init
   dotnet user-secrets set "Telegram:BotToken" "123456:ABC..."
   ```

3. **Windows Service environment**
   - Service doesn't inherit user environment variables
   - Set via registry or service configuration
   - Or use appsettings.Production.json (not recommended for secrets)

---

## Deployment Issues

### PowerShell Script Fails

**Symptom**: `install-supervisor.ps1` exits with error

**Solutions**:

1. **Run as Administrator**
   ```powershell
   # Right-click PowerShell → Run as Administrator
   # Or check:
   [Security.Principal.WindowsIdentity]::GetCurrent().Groups -contains 'S-1-5-32-544'
   # Should return True
   ```

2. **Execution Policy**
   ```powershell
   # If "script cannot be loaded":
   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
   ```

3. **Binary not found**
   ```
   Error: Service binary not found at: ...
   
   Solution: Build and publish first:
   dotnet publish -c Release -r win-x64 --self-contained src/TradingSupervisorService/TradingSupervisorService.csproj
   ```

---

### Service Installed But Not Starting

**Symptom**: Service shows "Stopped" after install

**Diagnostics**:

1. **Check Event Viewer** (see above)

2. **Try manual start with error details**
   ```powershell
   Start-Service TradingSupervisorService -ErrorAction Stop
   # Shows detailed error message
   ```

3. **Run as console app to debug**
   ```powershell
   cd src/TradingSupervisorService/bin/Release/net10.0/win-x64/publish
   .\TradingSupervisorService.exe
   # See error output directly
   ```

---

## Dashboard Issues

### Dashboard Won't Load

**Symptom**: Blank page or error in browser console

**Solutions**:

1. **Check API endpoint**
   ```javascript
   // In browser console:
   fetch('http://localhost:8787/health')
     .then(r => r.json())
     .then(console.log)
   
   // Should return: {"status":"ok"}
   ```

2. **Check CORS**
   ```
   Error: "Access-Control-Allow-Origin"
   
   Solution: Update wrangler.toml:
   [vars]
   DASHBOARD_ORIGIN = "http://localhost:5173"  // Match dashboard URL
   ```

3. **Check .env.local**
   ```bash
   # Must have:
   VITE_API_BASE_URL=http://localhost:8787
   VITE_API_KEY=test-key-local-dev
   ```

---

### Dashboard Shows No Data

**Symptom**: Dashboard loads, but all pages show "No data"

**Solutions**:

1. **Check Worker is running**
   ```powershell
   # For local dev:
   cd infra/cloudflare/worker
   bun run dev
   # Should show: Listening on http://localhost:8787
   ```

2. **Check D1 database has data**
   ```bash
   # If deployed:
   wrangler d1 execute trading-db --command "SELECT * FROM service_heartbeats;"
   
   # Should show heartbeat records
   ```

3. **Check browser console for API errors**
   - F12 → Console tab
   - Look for 401 (authentication), 404 (endpoint not found), 500 (server error)

---

### Theme Not Persisting

**Symptom**: Theme resets to default on page reload

**Solutions**:

1. **Check localStorage**
   ```javascript
   // Browser console:
   localStorage.getItem('ui-store')
   // Should show: {"state":{"theme":"dark"},...}
   ```

2. **Check anti-flash script**
   ```html
   <!-- In index.html, must be in <head> BEFORE other scripts -->
   <script>
     (function() {
       try {
         const stored = localStorage.getItem('ui-store');
         // ...
       } catch (e) {}
     })();
   </script>
   ```

3. **Private browsing mode**
   - localStorage disabled in some browsers' private mode
   - Use normal browsing mode

---

## Cloudflare Worker Issues

### Deployment Fails

**Symptom**: `wrangler deploy` exits with error

**Solutions**:

1. **Check authentication**
   ```bash
   wrangler whoami
   # Should show your Cloudflare account
   
   # If not logged in:
   wrangler login
   ```

2. **Check wrangler.toml**
   ```toml
   # Verify D1 database ID is set:
   [[d1_databases]]
   database_id = "REPLACE_WITH_YOUR_D1_ID"  # Must be actual ID
   ```

3. **Create D1 database first**
   ```bash
   wrangler d1 create trading-db
   # Copy database_id from output to wrangler.toml
   ```

---

### Worker Returns 500 Errors

**Symptom**: API calls return "Internal Server Error"

**Diagnostics**:

1. **Check Worker logs**
   ```bash
   wrangler tail
   # Shows real-time logs from Worker
   # Make API call to see error
   ```

2. **Check D1 database migration**
   ```bash
   # Verify tables exist:
   wrangler d1 execute trading-db --command "SELECT name FROM sqlite_master WHERE type='table';"
   
   # Should show: service_heartbeats, sync_outbox, alert_history, etc.
   ```

3. **Check API key**
   ```bash
   # Verify secret is set:
   wrangler secret list
   # Should show: API_KEY
   
   # If missing:
   wrangler secret put API_KEY
   ```

---

## Logs and Diagnostics

### Enable Debug Logging

```json
// In appsettings.json:
"Serilog": {
  "MinimumLevel": {
    "Default": "Debug",  // Was "Information"
    "Override": {
      "Microsoft": "Information",  // More Microsoft logs
      "System": "Information"
    }
  }
}
```

**Warning**: Debug logs are verbose. Use only for troubleshooting, then revert to Information.

---

### Useful Log Queries

```powershell
# Find all errors in last 24 hours
Get-Content logs/supervisor-*.log | Select-String "\[ERR\]|\[CRT\]"

# Find IBKR connection events
Get-Content logs/options-execution-*.log | Select-String "IBKR|Connection"

# Find specific service events
Get-Content logs/supervisor-*.log | Select-String "HeartbeatWorker"

# Count log levels
Get-Content logs/supervisor-*.log | Select-String "\[INF\]" | Measure-Object
Get-Content logs/supervisor-*.log | Select-String "\[WRN\]" | Measure-Object
Get-Content logs/supervisor-*.log | Select-String "\[ERR\]" | Measure-Object
```

---

### Performance Diagnostics

```powershell
# Check service memory usage
Get-Process TradingSupervisorService | Select-Object WorkingSet64, PrivateMemorySize64

# Check service CPU usage (requires monitoring)
Get-Counter '\Process(TradingSupervisorService)\% Processor Time' -SampleInterval 1 -MaxSamples 10

# Check database size
dir data/*.db | Select-Object Name, Length

# Check log size
dir logs/*.log | Select-Object Name, Length | Sort-Object Length -Descending
```

---

### Database Diagnostics

```powershell
# Check database integrity
sqlite3 data/supervisor.db "PRAGMA integrity_check;"
# Should return: ok

# Check WAL mode
sqlite3 data/supervisor.db "PRAGMA journal_mode;"
# Should return: wal

# Check database size and page count
sqlite3 data/supervisor.db "PRAGMA page_count; PRAGMA page_size;"

# Vacuum database (reclaim space)
Stop-Service TradingSupervisorService
sqlite3 data/supervisor.db "VACUUM;"
Start-Service TradingSupervisorService
```

---

## Getting Help

### Before Asking for Help

1. **Check logs** for error messages
2. **Search this troubleshooting guide**
3. **Verify configuration** is valid
4. **Test with example strategies** first

### Information to Provide

When reporting an issue, include:

1. **Symptom**: What's not working?
2. **Logs**: Last 50 lines from relevant log file
3. **Configuration**: Anonymized appsettings.json
4. **Environment**:
   - Windows version
   - .NET version (`dotnet --version`)
   - IBKR TWS/Gateway version
5. **Steps to reproduce**

### Contact

Email: lorenzo.padovani@padosoft.com

---

*Last updated: 2026-04-05 | Trading System v1.0*
