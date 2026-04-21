---
title: "Configuration Reference"
tags: ["reference", "onboarding", "dotnet", "worker", "dashboard"]
aliases: ["Configuration", "Config Reference"]
status: current
audience: ["developer", "operator"]
last-reviewed: "2026-04-21"
related:
  - "[[Configuration Checklist|CONFIGURATION-CHECKLIST]]"
  - "[[SECRETS]]"
  - "[[Getting Started with Trading System|GETTING_STARTED]]"
  - "[[Bot Setup Guide - Telegram & Discord Integration|BOT_SETUP_GUIDE]]"
---

# Configuration Reference

> Complete guide to all configuration options
> Last updated: 2026-04-05

---

## Table of Contents

- [Configuration Files](#configuration-files)
- [TradingSupervisorService Configuration](#tradingsupervisorservice-configuration)
- [OptionsExecutionService Configuration](#optionsexecutionservice-configuration)
- [Cloudflare Worker Configuration](#cloudflare-worker-configuration)
- [Dashboard Configuration](#dashboard-configuration)
- [Environment Variables](#environment-variables)
- [Security Best Practices](#security-best-practices)

---

## Configuration Files

### File Locations

```
src/TradingSupervisorService/
├── appsettings.json              # Base configuration
├── appsettings.Development.json  # Development overrides
└── appsettings.Production.json   # Production overrides (git-ignored)

src/OptionsExecutionService/
├── appsettings.json              # Base configuration
├── appsettings.Development.json  # Development overrides
└── appsettings.Production.json   # Production overrides (git-ignored)

infra/cloudflare/worker/
└── wrangler.toml                 # Worker configuration

dashboard/
├── .env.example                  # Template
├── .env.local                    # Local dev (git-ignored)
└── .env.production               # Production (git-ignored)
```

### Environment Selection

.NET services use `DOTNET_ENVIRONMENT` or `ASPNETCORE_ENVIRONMENT`:

```powershell
# Development (default)
$env:DOTNET_ENVIRONMENT="Development"
dotnet run

# Production
$env:DOTNET_ENVIRONMENT="Production"
dotnet run
```

Configuration cascade:
1. `appsettings.json` (base)
2. `appsettings.{Environment}.json` (overrides)
3. Environment variables (highest priority)

---

## TradingSupervisorService Configuration

### Complete Example

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },
  "TradingMode": "paper",
  "Sqlite": {
    "SupervisorDbPath": "data/supervisor.db"
  },
  "Monitoring": {
    "IntervalSeconds": 60,
    "CpuThresholdPercent": 80.0,
    "RamThresholdPercent": 85.0,
    "DiskThresholdGb": 5.0
  },
  "OutboxSync": {
    "IntervalSeconds": 30,
    "BatchSize": 50,
    "InitialRetryDelaySeconds": 5,
    "MaxRetryDelaySeconds": 300,
    "MaxRetries": 10
  },
  "Cloudflare": {
    "WorkerUrl": "https://trading-alerts.your-account.workers.dev",
    "ApiKey": "your-secret-api-key"
  },
  "IBKR": {
    "Host": "127.0.0.1",
    "PaperPort": 4002,
    "LivePort": 4001,
    "ClientId": 1
  },
  "Telegram": {
    "Enabled": false,
    "BotToken": "",
    "ChatId": 0,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 5,
    "MaxMessagesPerMinute": 20,
    "ProcessIntervalSeconds": 5
  },
  "LogReader": {
    "OptionsServiceLogPath": "logs/options-execution-.log",
    "IntervalSeconds": 30
  },
  "IvtsMonitor": {
    "Enabled": false,
    "IntervalSeconds": 900,
    "Symbol": "SPX",
    "IvrThresholdPercent": 80.0,
    "InvertedThresholdPercent": 5.0,
    "SpikeThresholdPercent": 20.0
  },
  "GreeksMonitor": {
    "Enabled": true,
    "IntervalSeconds": 60,
    "DeltaThreshold": 0.70,
    "GammaThreshold": 0.05,
    "ThetaThreshold": 50.0,
    "VegaThreshold": 100.0
  },
  "OptionsDb": {
    "OptionsDbPath": "data/options.db"
  }
}
```

---

### Serilog Section

Controls structured logging configuration.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MinimumLevel.Default` | string | `Information` | Minimum log level: `Debug`, `Information`, `Warning`, `Error`, `Critical` |
| `MinimumLevel.Override.Microsoft` | string | `Warning` | Log level for Microsoft.* namespaces (reduce noise) |
| `MinimumLevel.Override.System` | string | `Warning` | Log level for System.* namespaces |

**Note**: Logs are written to `logs/supervisor-YYYYMMDD.log` with daily rolling.

---

### TradingMode

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `TradingMode` | string | `paper` | **CRITICAL**: `paper` or `live`. Default is safe. Never commit `live` to git. |

**Validation**: Configuration validator rejects `live` mode (safety feature). To enable live mode, you must modify validator code (intentional friction).

---

### Sqlite Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `SupervisorDbPath` | string | `data/supervisor.db` | Path to SQLite database (relative or absolute) |

**Note**: Database auto-creates on first run. WAL mode enabled automatically.

---

### Monitoring Section

Controls HeartbeatWorker behavior.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `IntervalSeconds` | int | `60` | How often to record heartbeat (30-300 recommended) |
| `CpuThresholdPercent` | decimal | `80.0` | CPU usage alert threshold (0-100) |
| `RamThresholdPercent` | decimal | `85.0` | RAM usage alert threshold (0-100) |
| `DiskThresholdGb` | decimal | `5.0` | Free disk space alert threshold (GB) |

**Alerts**: When thresholds exceeded, creates alert in `alert_history` table and sends to Telegram (if enabled).

---

### OutboxSync Section

Controls OutboxSyncWorker (event publishing to Cloudflare).

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `IntervalSeconds` | int | `30` | Polling interval for pending events (10-60 recommended) |
| `BatchSize` | int | `50` | Max events per sync batch (1-100) |
| `InitialRetryDelaySeconds` | int | `5` | First retry delay on failure |
| `MaxRetryDelaySeconds` | int | `300` | Maximum retry delay (exponential backoff cap) |
| `MaxRetries` | int | `10` | Max retry attempts (0 = infinite retries) |

**Retry Logic**: Uses exponential backoff: 5s → 10s → 20s → 40s → 80s → 160s → 300s (capped).

---

### Cloudflare Section

Connection to Cloudflare Worker API.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `WorkerUrl` | string | (required) | Full URL to Worker, e.g., `https://trading.example.workers.dev` |
| `ApiKey` | string | (required) | Bearer token for authentication. **DO NOT COMMIT** |

**Security**: Use environment variable or User Secrets for `ApiKey` in production:

```powershell
# Set via environment variable
$env:Cloudflare__ApiKey="your-secret-key"
dotnet run

# Or use User Secrets (development)
dotnet user-secrets set "Cloudflare:ApiKey" "your-secret-key"
```

---

### IBKR Section

Interactive Brokers connection settings.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Host` | string | `127.0.0.1` | TWS/Gateway host (usually localhost) |
| `PaperPort` | int | `4002` | Gateway paper port (or 7497 for TWS paper) |
| `LivePort` | int | `4001` | Gateway live port (or 7496 for TWS live) **NOT USED** |
| `ClientId` | int | `1` | Unique client ID (1-99). Supervisor uses 1, Execution uses 2. |

**Port Selection**:
- Paper TWS: `7497`
- Paper Gateway: `4002`
- Live TWS: `7496` (BLOCKED by validator)
- Live Gateway: `4001` (BLOCKED by validator)

**ClientId**: Must be unique per IBKR client on same TWS instance. If running multiple services, use different IDs (1, 2, 3, etc.).

---

### Telegram Section

Telegram bot configuration for critical alerts.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable/disable Telegram alerting |
| `BotToken` | string | `""` | Telegram bot token from @BotFather. **DO NOT COMMIT** |
| `ChatId` | long | `0` | Telegram chat ID (your user ID or group ID) |
| `MaxRetryAttempts` | int | `3` | Retry count on send failure |
| `RetryDelaySeconds` | int | `5` | Delay between retries |
| `MaxMessagesPerMinute` | int | `20` | Rate limit (Telegram allows 30/min, we use 20 for safety) |
| `ProcessIntervalSeconds` | int | `5` | How often worker checks queue |

**Setup**: See [Telegram Integration Guide](./telegram-integration.md).

**Security**: Never commit `BotToken`. Use environment variables or User Secrets.

---

### LogReader Section

Log file tailing for error detection.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `OptionsServiceLogPath` | string | `logs/options-execution-.log` | Path pattern to execution service log file |
| `IntervalSeconds` | int | `30` | How often to check log file (10-60 recommended) |

**Pattern Matching**: LogReader detects ERROR and CRITICAL lines using regex and creates alerts.

---

### IvtsMonitor Section

Implied Volatility Term Structure monitoring (optional).

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `false` | Enable IVTS monitoring (requires IBKR market data subscription) |
| `IntervalSeconds` | int | `900` | Check interval (15 minutes recommended) |
| `Symbol` | string | `SPX` | Underlying symbol to monitor |
| `IvrThresholdPercent` | decimal | `80.0` | Alert when IV Rank > threshold |
| `InvertedThresholdPercent` | decimal | `5.0` | Alert when term structure inverted by > threshold |
| `SpikeThresholdPercent` | decimal | `20.0` | Alert when IV spikes > threshold since last check |

**Note**: Requires IBKR market data subscription for SPX options. Disabled by default.

---

### GreeksMonitor Section

Position Greeks risk monitoring.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enabled` | bool | `true` | Enable Greeks monitoring |
| `IntervalSeconds` | int | `60` | Check interval (30-300 recommended) |
| `DeltaThreshold` | decimal | `0.70` | Alert when portfolio delta > threshold (absolute value) |
| `GammaThreshold` | decimal | `0.05` | Alert when portfolio gamma > threshold |
| `ThetaThreshold` | decimal | `50.0` | Alert when portfolio theta > threshold (absolute value) |
| `VegaThreshold` | decimal | `100.0` | Alert when portfolio vega > threshold |

**Portfolio Greeks**: Sum of all position Greeks from `positions` table in `options.db`.

---

### OptionsDb Section

Cross-database access to execution service database.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `OptionsDbPath` | string | `data/options.db` | Path to execution service database |

**Purpose**: Supervisor reads position data for Greeks monitoring.

---

## OptionsExecutionService Configuration

### Complete Example

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  },
  "TradingMode": "paper",
  "Sqlite": {
    "OptionsDbPath": "data/options-execution.db"
  },
  "Strategy": {
    "FilePath": "strategies/private/current.json",
    "ReloadIntervalSeconds": 300
  },
  "Execution": {
    "MaxConcurrentOrders": 5,
    "OrderTimeoutSeconds": 30
  },
  "IBKR": {
    "Host": "127.0.0.1",
    "PaperPort": 4002,
    "LivePort": 4001,
    "ClientId": 2,
    "ReconnectInitialDelaySeconds": 5,
    "ReconnectMaxDelaySeconds": 300,
    "MaxReconnectAttempts": 0,
    "ConnectionTimeoutSeconds": 10,
    "KeepaliveIntervalSeconds": 60
  },
  "Safety": {
    "MaxPositionSize": 10,
    "MaxPositionValueUsd": 50000,
    "MinAccountBalanceUsd": 10000,
    "MaxRiskPercentOfAccount": 5.0,
    "CircuitBreakerFailureThreshold": 3,
    "CircuitBreakerWindowMinutes": 60,
    "CircuitBreakerResetMinutes": 120
  },
  "Campaign": {
    "MonitorIntervalSeconds": 60
  }
}
```

---

### Serilog Section

Same as Supervisor (see above). Logs written to `logs/options-execution-YYYYMMDD.log`.

---

### TradingMode

Same as Supervisor. **CRITICAL** safety setting.

---

### Sqlite Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `OptionsDbPath` | string | `data/options-execution.db` | Path to SQLite database |

---

### Strategy Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `FilePath` | string | (required) | Path to strategy JSON file |
| `ReloadIntervalSeconds` | int | `300` | How often to reload strategy file (0 = never reload) |

**File Path**:
- Use `strategies/examples/*.json` for testing
- Use `strategies/private/*.json` for real strategies (git-ignored)

**Reload**: If strategy file changes, service picks up changes after reload interval (0 disables hot reload).

---

### Execution Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MaxConcurrentOrders` | int | `5` | Max simultaneous pending orders (1-10 recommended) |
| `OrderTimeoutSeconds` | int | `30` | Timeout for order confirmation from IBKR |

---

### IBKR Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Host` | string | `127.0.0.1` | TWS/Gateway host |
| `PaperPort` | int | `4002` | Gateway paper port |
| `LivePort` | int | `4001` | Gateway live port (BLOCKED) |
| `ClientId` | int | `2` | Client ID (must differ from Supervisor if both connect to same TWS) |
| `ReconnectInitialDelaySeconds` | int | `5` | First reconnect delay |
| `ReconnectMaxDelaySeconds` | int | `300` | Max reconnect delay (exponential backoff cap) |
| `MaxReconnectAttempts` | int | `0` | Max reconnect attempts (0 = infinite) |
| `ConnectionTimeoutSeconds` | int | `10` | TCP connection timeout |
| `KeepaliveIntervalSeconds` | int | `60` | Heartbeat interval to detect stale connection |

**Reconnection**: Uses exponential backoff like Supervisor.

---

### Safety Section

**CRITICAL** risk management settings.

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MaxPositionSize` | int | `10` | Max number of contracts per position |
| `MaxPositionValueUsd` | decimal | `50000` | Max notional value per position |
| `MinAccountBalanceUsd` | decimal | `10000` | Minimum account balance required to trade |
| `MaxRiskPercentOfAccount` | decimal | `5.0` | Max risk per trade as % of account (0-100) |
| `CircuitBreakerFailureThreshold` | int | `3` | Consecutive failures to open circuit |
| `CircuitBreakerWindowMinutes` | int | `60` | Time window for failure counting |
| `CircuitBreakerResetMinutes` | int | `120` | Circuit remains open for this duration before half-open |

**Circuit Breaker**: After N failures in M minutes, circuit opens (reject all orders). After reset period, allow 1 test order. On success, close circuit.

**Validation**: Checked before every order. Violations reject order immediately.

---

### Campaign Section

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `MonitorIntervalSeconds` | int | `60` | How often to evaluate entry/exit rules (30-300 recommended) |

---

## Cloudflare Worker Configuration

### wrangler.toml

```toml
name = "trading-system"
main = "src/index.ts"
compatibility_date = "2025-01-01"
compatibility_flags = ["nodejs_compat"]

[[d1_databases]]
binding = "DB"
database_name = "trading-db"
database_id = "REPLACE_WITH_YOUR_D1_ID"

[vars]
DASHBOARD_ORIGIN = "http://localhost:5173"

# Secrets (set via: wrangler secret put API_KEY)
# API_KEY - Authentication key for protected endpoints
```

### Settings

| Setting | Type | Description |
|---------|------|-------------|
| `name` | string | Worker name (must be unique in your Cloudflare account) |
| `main` | string | Entry point file |
| `compatibility_date` | string | Cloudflare runtime compatibility date |
| `[[d1_databases]]` | array | D1 database bindings |
| `binding` | string | Env variable name for database (`c.env.DB`) |
| `database_id` | string | D1 database ID (from `wrangler d1 create`) |
| `vars.DASHBOARD_ORIGIN` | string | CORS allowed origin (update for production) |

### Secrets

Set via CLI (not in file):

```bash
# Set API key
wrangler secret put API_KEY
# Paste key when prompted

# Verify secrets
wrangler secret list
```

**API_KEY**: Required for POST /events endpoint. Services must include in `Authorization: Bearer <key>` header.

---

## Dashboard Configuration

### .env.local (Development)

```bash
# API endpoint
VITE_API_BASE_URL=http://localhost:8787

# API key for authenticated endpoints
VITE_API_KEY=test-key-local-dev

# Polling interval (milliseconds)
VITE_REFRESH_INTERVAL=30000

# Feature flags
VITE_ENABLE_TELEGRAM=false
VITE_ENABLE_IVTS=false
```

### .env.production (Production)

```bash
VITE_API_BASE_URL=https://trading-system.your-account.workers.dev
VITE_API_KEY=your-production-api-key
VITE_REFRESH_INTERVAL=30000
VITE_ENABLE_TELEGRAM=true
VITE_ENABLE_IVTS=false
```

### Environment Variables

All variables prefixed with `VITE_` are embedded in build:

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `VITE_API_BASE_URL` | string | (required) | Cloudflare Worker URL |
| `VITE_API_KEY` | string | (required) | API key for Worker authentication |
| `VITE_REFRESH_INTERVAL` | number | `30000` | Dashboard polling interval (ms) |
| `VITE_ENABLE_TELEGRAM` | bool | `false` | Show Telegram settings in UI |
| `VITE_ENABLE_IVTS` | bool | `false` | Show IVTS charts in UI |

**Note**: Vite embeds these at build time. Changing `.env` after build has no effect. Rebuild required.

---

## Environment Variables

### .NET Services

Override any `appsettings.json` value via environment variable:

```powershell
# Format: Section__SubSection__Key
$env:TradingMode="paper"
$env:IBKR__ClientId="3"
$env:Cloudflare__ApiKey="secret-key"
$env:Telegram__BotToken="123456:ABC..."

dotnet run
```

Double underscore (`__`) represents JSON hierarchy.

### Cloudflare Worker

Set secrets via `wrangler secret put` (see above).

Set public variables in `wrangler.toml` under `[vars]`.

---

## Security Best Practices

### 1. Never Commit Secrets

**DO NOT commit**:
- `appsettings.Production.json`
- `.env.production`
- Any file containing API keys, bot tokens, passwords

**Git Ignore**:
```gitignore
appsettings.Production.json
appsettings.*.json
!appsettings.json
!appsettings.Development.json
.env.local
.env.production
strategies/private/
```

### 2. Use User Secrets (Development)

```powershell
# Initialize user secrets
cd src/TradingSupervisorService
dotnet user-secrets init

# Set secret
dotnet user-secrets set "Telegram:BotToken" "123456:ABC..."

# List secrets
dotnet user-secrets list
```

Secrets stored in: `%APPDATA%\Microsoft\UserSecrets\<guid>\secrets.json`

### 3. Use Environment Variables (Production)

```powershell
# Windows Service environment variables
# Set in service registry or startup script

$env:Cloudflare__ApiKey="prod-secret"
$env:Telegram__BotToken="prod-bot-token"

Start-Service TradingSupervisorService
```

Or use Windows Credential Manager / Azure Key Vault for production.

### 4. Restrict File Permissions

```powershell
# Give service account read-only access to config
icacls "C:\TradingSystem\appsettings.Production.json" /grant "NT SERVICE\TradingSupervisorService:R"

# Deny other users
icacls "C:\TradingSystem\appsettings.Production.json" /inheritance:r
```

### 5. Encrypt Sensitive Config

Use DPAPI (Data Protection API) to encrypt config sections:

```csharp
// Example: Encrypt connection string
var protectedData = ProtectedData.Protect(
    Encoding.UTF8.GetBytes(apiKey),
    null,
    DataProtectionScope.LocalMachine
);
```

Store encrypted blob in config, decrypt at runtime.

---

## Configuration Validation

### At Startup

Both services validate configuration on startup. Invalid config → service fails to start.

**Validation Rules**:
- `TradingMode` must be `paper` or `live`
- IBKR `Host` cannot be empty
- IBKR `Port` must be >0 and <65536
- IBKR `ClientId` must be 0-99
- Live ports (7496, 4002) are REJECTED
- `TradingMode.Live` is REJECTED by default validator
- File paths must be valid (directories exist)

### Manual Validation

Test configuration without starting service:

```powershell
# Build validator tool (custom script)
dotnet run --project tools/ConfigValidator -- src/TradingSupervisorService/appsettings.json
```

Or use dry-run mode (if implemented):

```powershell
dotnet run --no-launch-profile -- --validate-config
```

---

## Troubleshooting Configuration

### Configuration Not Loading

**Symptom**: Service uses default values, ignores `appsettings.json`

**Solutions**:
1. Check JSON syntax (use JSON validator)
2. Verify file exists in service directory
3. Check environment: `$env:DOTNET_ENVIRONMENT` (Development vs Production)
4. Check file permissions (service account can read file)

### Secrets Not Working

**Symptom**: Environment variable or User Secret not applied

**Solutions**:
1. Verify environment variable format: `Section__Key`
2. Check User Secrets are initialized: `dotnet user-secrets list`
3. Restart service after changing environment variables
4. Check service logs for "Configuration loaded" message

### IBKR Connection Fails

**Symptom**: "Connection refused" despite correct config

**Solutions**:
1. Verify TWS/Gateway is running and logged in
2. Check API enabled in TWS: Configure → API → Enable Socket Clients
3. Verify port number matches TWS configuration
4. Check `ClientId` is unique (no conflicts with other clients)
5. Check firewall (allow localhost connections)

---

*Last updated: 2026-04-05 | Trading System v1.0*
