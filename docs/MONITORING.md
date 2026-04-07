# Monitoring and Alerting

> Observability guide for production operation
> Last updated: 2026-04-05

---

## Table of Contents

- [Monitoring Overview](#monitoring-overview)
- [Metrics Collection](#metrics-collection)
- [Alert Types](#alert-types)
- [Dashboards](#dashboards)
- [Log Analysis](#log-analysis)
- [Performance Monitoring](#performance-monitoring)
- [Health Checks](#health-checks)
- [Incident Response](#incident-response)

---

## Monitoring Overview

The Trading System provides comprehensive observability through:

1. **Heartbeat System**: Service health metrics every 60s
2. **Structured Logging**: Serilog JSON logs with correlation
3. **Alert System**: Critical alerts via Telegram
4. **Dashboard**: Real-time web UI
5. **Database Metrics**: Query position/campaign state

---

## Metrics Collection

### Service Heartbeats

**Source**: `HeartbeatWorker` in TradingSupervisorService

**Frequency**: Every 60 seconds

**Metrics Collected**:

| Metric | Type | Description |
|--------|------|-------------|
| `cpu_percent` | decimal | CPU usage % (0-100) |
| `ram_percent` | decimal | RAM usage % (0-100) |
| `disk_free_gb` | decimal | Free disk space in GB |
| `uptime_seconds` | int | Service uptime since start |
| `trading_mode` | string | "paper" or "live" |
| `version` | string | Semantic version |

**Storage**:
- Local: `supervisor.db` → `service_heartbeats` table
- Cloud: D1 database (via outbox sync)

**Query**:
```sql
SELECT 
    service_name,
    cpu_percent,
    ram_percent,
    disk_free_gb,
    last_seen_at
FROM service_heartbeats
WHERE last_seen_at > datetime('now', '-5 minutes')
ORDER BY last_seen_at DESC;
```

---

### Campaign Metrics

**Source**: `CampaignManager` in OptionsExecutionService

**Metrics**:
- Active campaigns count
- Pending campaigns count
- Closed campaigns count
- Success rate (closed with profit vs loss)

**Query**:
```sql
SELECT 
    state,
    COUNT(*) as count,
    AVG(CASE WHEN unrealized_pnl > 0 THEN 1.0 ELSE 0.0 END) as win_rate
FROM campaigns
GROUP BY state;
```

---

### Position Metrics

**Source**: `OptionsExecutionService` database

**Metrics**:
- Open positions count
- Total notional value
- Portfolio Greeks (delta, gamma, theta, vega)
- Unrealized P&L

**Query**:
```sql
SELECT 
    COUNT(*) as open_positions,
    SUM(quantity * entry_price * 100) as total_notional,
    SUM(delta) as portfolio_delta,
    SUM(gamma) as portfolio_gamma,
    SUM(theta) as portfolio_theta,
    SUM(vega) as portfolio_vega,
    SUM(unrealized_pnl) as total_pnl
FROM positions
WHERE status = 'Open';
```

---

### IBKR Connection Metrics

**Source**: `IbkrClient` in both services

**Metrics**:
- Connection state (Connected, Disconnected, Reconnecting)
- Reconnection attempts
- Last successful connection timestamp
- Market data feed status

**Indicators**:
- Logs show "IBKR connected" → healthy
- Logs show "Connection lost" + automatic reconnect → recoverable
- Logs show "Max reconnect attempts" → critical issue

---

## Alert Types

### Critical Alerts (Sent to Telegram)

#### 1. Service Heartbeat Missing

**Trigger**: No heartbeat in last 2 minutes

**Severity**: Critical

**Action**: Service may have crashed

**Response**:
```powershell
# Check service status
Get-Service TradingSupervisorService

# If stopped, check logs
Get-Content logs/supervisor-*.log -Tail 100

# Restart if needed
Start-Service TradingSupervisorService
```

---

#### 2. IBKR Connection Lost

**Trigger**: Connection state = Disconnected for >5 minutes

**Severity**: Critical (if trading active), Warning (if market closed)

**Action**: Cannot trade, market data stale

**Response**:
1. Check TWS/Gateway is running
2. Check network connectivity
3. Check IBKR API status: https://www.interactivebrokers.com/en/index.php?f=2225
4. Service will auto-reconnect (exponential backoff)

---

#### 3. Order Rejection

**Trigger**: IBKR rejects order placement

**Severity**: Critical

**Possible Causes**:
- Insufficient margin
- Invalid symbol/strike
- Market closed
- API rate limit exceeded
- Account suspended

**Response**:
1. Check order tracking table for error message
2. Verify account margin
3. Check campaign rules (may be too aggressive)

---

#### 4. Position Greeks Threshold Exceeded

**Trigger**: Portfolio delta, gamma, theta, or vega exceeds threshold

**Severity**: Warning

**Example**:
```
Portfolio delta (0.85) exceeds threshold (0.70)
```

**Response**:
1. Review open positions
2. Consider closing positions to reduce risk
3. Adjust campaign limits if threshold too tight

---

#### 5. Disk Space Low

**Trigger**: Free disk space < 5 GB

**Severity**: Critical

**Response**:
```powershell
# Check disk space
Get-PSDrive C

# Clean up old logs (if >30 days)
Get-ChildItem logs/ -Filter *.log | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item

# Vacuum databases
sqlite3 data/supervisor.db "VACUUM;"
sqlite3 data/options.db "VACUUM;"
```

---

### Warning Alerts (Dashboard Only)

#### 6. Log Errors Detected

**Trigger**: ERROR or CRITICAL level log in execution service

**Severity**: Warning

**Source**: `LogReaderWorker`

**Response**: Review logs for context

---

#### 7. Outbox Retry Exhausted

**Trigger**: Event failed to sync after max retries

**Severity**: Warning

**Impact**: Event not visible in dashboard (but saved locally)

**Response**:
1. Check Cloudflare Worker is accessible
2. Verify API key is correct
3. Check Worker logs for errors

---

#### 8. High Resource Usage

**Trigger**: CPU >80% or RAM >85% for >5 minutes

**Severity**: Warning

**Response**: Check for runaway background tasks or memory leaks

---

## Dashboards

### Web Dashboard

**URL**: Configured in deployment (e.g., `https://trading.example.com`)

**Pages**:

1. **Overview**: System health summary
   - Service status
   - IBKR connection status
   - Active campaigns count
   - Open positions count
   - Total P&L

2. **Trading**: Real-time campaign and position data
   - Campaign list with state
   - Position list with Greeks
   - Recent orders

3. **Alerts**: Alert history with filtering
   - Filter by severity (Info, Warning, Critical)
   - Filter by type (Heartbeat, IBKR, Order, etc.)
   - Auto-refresh every 30s

4. **Analytics**: Performance charts
   - P&L over time
   - Win rate by strategy
   - IV rank distribution at entry

5. **Settings**: Configuration
   - Refresh interval
   - Theme (dark/light)
   - Telegram test message

---

### Database Queries

For custom dashboards or monitoring tools:

**Active Campaigns**:
```sql
SELECT 
    campaign_id,
    strategy_name,
    state,
    created_at,
    updated_at
FROM campaigns
WHERE state IN ('Pending', 'Active')
ORDER BY created_at DESC;
```

**Recent Alerts**:
```sql
SELECT 
    alert_type,
    severity,
    message,
    created_at
FROM alert_history
WHERE created_at > datetime('now', '-24 hours')
ORDER BY created_at DESC
LIMIT 100;
```

**System Health**:
```sql
SELECT 
    service_name,
    cpu_percent,
    ram_percent,
    disk_free_gb,
    last_seen_at,
    CASE 
        WHEN last_seen_at > datetime('now', '-2 minutes') THEN 'Healthy'
        WHEN last_seen_at > datetime('now', '-5 minutes') THEN 'Degraded'
        ELSE 'Down'
    END as status
FROM service_heartbeats;
```

---

## Log Analysis

### Log Format

Serilog structured JSON logs:

```json
{
  "@t": "2026-04-05T10:30:45.1234567Z",
  "@mt": "Heartbeat saved for {ServiceName}",
  "@l": "Information",
  "ServiceName": "TradingSupervisorService",
  "SourceContext": "TradingSupervisorService.Repositories.HeartbeatRepository"
}
```

**Fields**:
- `@t`: Timestamp (ISO 8601)
- `@mt`: Message template
- `@l`: Log level (Debug, Information, Warning, Error, Critical)
- Additional properties (structured data)

---

### Log Queries

**Find all errors in last hour**:
```powershell
Get-Content logs/supervisor-*.log | 
  Select-String '"@l":"Error"|"@l":"Critical"' |
  Select-Object -Last 100
```

**Find IBKR connection events**:
```powershell
Get-Content logs/options-execution-*.log |
  Select-String "IBKR|Connection" |
  Select-Object -Last 50
```

**Count log levels**:
```powershell
$logs = Get-Content logs/supervisor-*.log
($logs | Select-String '"@l":"Information"').Count
($logs | Select-String '"@l":"Warning"').Count
($logs | Select-String '"@l":"Error"').Count
```

---

### Log Correlation

Logs include correlation IDs for tracking requests:

```json
{
  "@mt": "Processing campaign {CampaignId}",
  "CampaignId": "abc-123",
  "CorrelationId": "xyz-789"
}
```

Search by correlation ID to trace full request:
```powershell
Get-Content logs/*.log | Select-String "xyz-789"
```

---

## Performance Monitoring

### Response Time Metrics

**Database Query Performance**:
```powershell
# Enable query logging (temporary):
# Add to appsettings.json:
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Microsoft.EntityFrameworkCore.Database.Command": "Information"
      }
    }
  }
}

# Review slow queries in logs
Get-Content logs/*.log | Select-String "Database.*elapsed" | Select-Object -Last 50
```

**Worker Cycle Time**:
```powershell
# Find worker cycle duration
Get-Content logs/supervisor-*.log | Select-String "Worker cycle completed" | Select-Object -Last 20
```

---

### Resource Usage Trends

**CPU Over Time**:
```sql
SELECT 
    datetime(last_seen_at) as time,
    service_name,
    cpu_percent
FROM service_heartbeats
WHERE last_seen_at > datetime('now', '-24 hours')
ORDER BY time;
```

Export to CSV for plotting:
```powershell
sqlite3 -header -csv data/supervisor.db "SELECT ..." > cpu_trend.csv
```

**Memory Leak Detection**:
```sql
SELECT 
    service_name,
    AVG(ram_percent) as avg_ram,
    MAX(ram_percent) as max_ram,
    MIN(ram_percent) as min_ram
FROM service_heartbeats
WHERE last_seen_at > datetime('now', '-7 days')
GROUP BY service_name;
```

Increasing `avg_ram` over days → potential leak.

---

## Health Checks

### Automated Health Checks

**Script**: `infra/windows/verify-installation.ps1`

**Checks**:
- Services running
- Databases accessible
- IBKR connection active
- No critical errors in logs
- Recent heartbeats exist

**Run**:
```powershell
.\infra\windows\verify-installation.ps1
```

**Output**:
```
✓ TradingSupervisorService is running
✓ OptionsExecutionService is running
✓ supervisor.db accessible
✓ options.db accessible
✓ IBKR connection active
✓ No critical errors in last 100 log lines
✓ Heartbeats current (within 2 minutes)
All checks passed.
```

---

### Manual Health Checks

**Check Service Status**:
```powershell
Get-Service TradingSupervisorService, OptionsExecutionService | Select-Object Name, Status, StartType
```

**Check Recent Heartbeat**:
```powershell
sqlite3 data/supervisor.db "SELECT service_name, last_seen_at FROM service_heartbeats WHERE service_name = 'TradingSupervisorService';"
```

**Check IBKR Connection**:
```powershell
Get-Content logs/options-execution-*.log -Tail 50 | Select-String "IBKR.*connected"
```

**Check Disk Space**:
```powershell
Get-PSDrive C | Select-Object Name, @{Name="FreeGB";Expression={[math]::Round($_.Free/1GB,2)}}
```

---

## Incident Response

### Severity Levels

| Level | Description | Response Time | Example |
|-------|-------------|---------------|---------|
| **Critical** | Service down, trading stopped | Immediate | Service crashed, IBKR disconnected >10 min |
| **High** | Degraded performance | <1 hour | High error rate, slow queries |
| **Medium** | Non-critical issue | <4 hours | Warning logs, configuration drift |
| **Low** | Informational | Best effort | Disk space trending up |

---

### Incident Checklist

**When alert received**:

1. **Acknowledge**: Note time and alert details
2. **Assess**: Severity and impact
3. **Diagnose**: Check logs, database, service status
4. **Mitigate**: Apply quick fix if available
5. **Resolve**: Implement permanent fix
6. **Document**: Update knowledge base

---

### Common Incidents

#### Service Crash

**Symptoms**: Heartbeat missing, service stopped

**Diagnosis**:
```powershell
# Check Event Viewer
eventvwr.msc
# Navigate to: Windows Logs > Application
# Look for errors from service

# Check service logs
Get-Content logs/supervisor-*.log -Tail 200 | Select-String "Critical|Unhandled|Exception"
```

**Resolution**:
```powershell
# Restart service
Start-Service TradingSupervisorService

# If crash repeats, check for:
# - Database corruption
# - Disk full
# - Memory exhaustion
# - Unhandled exception (code bug)
```

---

#### IBKR API Outage

**Symptoms**: "Connection refused", no market data

**Diagnosis**:
- Check IBKR status page: https://www.interactivebrokers.com/en/index.php?f=2225
- Check TWS/Gateway is running
- Check network connectivity

**Resolution**:
- Wait for IBKR to restore service (if API outage)
- Restart TWS/Gateway (if local issue)
- Service will auto-reconnect

---

#### Database Locked

**Symptoms**: "Database is locked" errors in logs

**Diagnosis**:
```powershell
# Check for open connections
sqlite3 data/supervisor.db "PRAGMA busy_timeout;"  # Should be 5000 (5s)

# Check for WAL checkpoint stuck
dir data/*.db-wal
```

**Resolution**:
```powershell
# Stop services
Stop-Service TradingSupervisorService, OptionsExecutionService

# Checkpoint WAL
sqlite3 data/supervisor.db "PRAGMA wal_checkpoint(FULL);"

# Restart
Start-Service TradingSupervisorService, OptionsExecutionService
```

---

#### Outbox Sync Failing

**Symptoms**: Events not appearing in dashboard, retry errors in logs

**Diagnosis**:
```powershell
# Check outbox status
sqlite3 data/supervisor.db "SELECT status, COUNT(*) FROM sync_outbox GROUP BY status;"

# Check Worker logs
Get-Content logs/supervisor-*.log | Select-String "OutboxSync|Cloudflare"
```

**Resolution**:
1. Verify Cloudflare Worker is deployed and accessible
2. Check API key is correct
3. Check Worker logs: `wrangler tail`
4. If temporary issue, events will retry automatically

---

## Best Practices

### 1. Regular Health Checks

Run verification script daily:
```powershell
# Add to Task Scheduler
schtasks /create /tn "TradingSystem Health Check" /tr "powershell.exe -File C:\TradingSystem\infra\windows\verify-installation.ps1" /sc daily /st 08:00
```

---

### 2. Log Retention

Configure log rotation:
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "logs/supervisor-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30  // Keep 30 days
        }
      }
    ]
  }
}
```

---

### 3. Database Backups

Automate daily backups:
```powershell
# backup-databases.ps1
$date = Get-Date -Format "yyyyMMdd"
Copy-Item data/supervisor.db backups/supervisor-$date.db
Copy-Item data/options.db backups/options-$date.db

# Keep only last 30 days
Get-ChildItem backups/*.db | Where-Object { $_.CreationTime -lt (Get-Date).AddDays(-30) } | Remove-Item
```

Add to Task Scheduler:
```powershell
schtasks /create /tn "Database Backup" /tr "powershell.exe -File C:\TradingSystem\scripts\backup-databases.ps1" /sc daily /st 03:00
```

---

### 4. Alert Tuning

Adjust thresholds to reduce false positives:

```json
{
  "Monitoring": {
    "CpuThresholdPercent": 80.0,  // Increase if false alerts
    "RamThresholdPercent": 85.0,
    "DiskThresholdGb": 5.0
  },
  "GreeksMonitor": {
    "DeltaThreshold": 0.70,  // Adjust based on strategy
    "ThetaThreshold": 50.0   // Increase if managing more positions
  }
}
```

---

### 5. Performance Baselines

Establish baselines for comparison:

| Metric | Baseline | Alert Threshold |
|--------|----------|-----------------|
| CPU usage | 5-15% | >80% for >5 min |
| RAM usage | 100-200 MB | >85% for >5 min |
| Heartbeat cycle | <1s | >5s |
| Campaign check | <2s | >10s |
| Database query | <50ms | >500ms |

---

## Tools and Integrations

### Recommended Monitoring Tools

1. **Grafana**: Visualize metrics from SQLite
2. **Prometheus**: Scrape metrics (requires exporter)
3. **PagerDuty**: Alert routing and escalation
4. **DataDog**: Full observability platform

### Integration Examples

**Export metrics to Prometheus**:
```csharp
// Add Prometheus.NET package
// Expose /metrics endpoint with:
// - Service uptime
// - Heartbeat metrics
// - Campaign counts
// - Position counts
```

**Send logs to centralized logging**:
```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://seq.internal:5341",
          "apiKey": "your-api-key"
        }
      }
    ]
  }
}
```

---

## References

- [Architecture](./ARCHITECTURE.md) - System components
- [Configuration](./CONFIGURATION.md) - Alert thresholds
- [Troubleshooting](./TROUBLESHOOTING.md) - Incident resolution
- [Telegram Integration](./telegram-integration.md) - Alerting setup

---

*Last updated: 2026-04-05 | Trading System v1.0*
