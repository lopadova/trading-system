---
title: "Getting Started with Trading System"
tags: ["onboarding", "dev", "reference"]
aliases: ["Getting Started", "Setup Guide"]
status: current
audience: ["new-user", "developer"]
last-reviewed: "2026-04-21"
related:
  - "[[Trading System - Developer Onboarding|ONBOARDING]]"
  - "[[Configuration Reference|CONFIGURATION]]"
  - "[[Strategy File Format|STRATEGY_FORMAT]]"
  - "[[Trading System - Deployment Guide|DEPLOYMENT_GUIDE]]"
  - "[[Windows Defender Unlock - Complete Guide|WINDOWS_DEFENDER]]"
---

# Getting Started with Trading System

> Step-by-step guide to set up and run the automated trading system
> Last updated: 2026-04-05

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Initial Setup](#initial-setup)
- [Build and Test](#build-and-test)
- [Configuration](#configuration)
- [Running Locally](#running-locally)
- [Deploy as Windows Service](#deploy-as-windows-service)
- [Verify Installation](#verify-installation)
- [Next Steps](#next-steps)

---

## Prerequisites

### Required Software

1. **Windows** (Server 2019+ or Windows 10/11 Pro)
   - Required for Windows Service hosting
   - PowerShell 5.1+ (built-in)

2. **.NET 10 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/10.0
   - Verify installation: `dotnet --version` (should output 10.0.x)

3. **🔴 CRITICAL: Interactive Brokers TWS API SDK**
   - **⚠️ MUST be installed BEFORE building the solution**
   - Download: https://www.interactivebrokers.com/en/trading/tws-api-install.php
   - Click "TWS API Installer for Windows" (MSI installer)
   - Install to **default path**: `C:\TWS API\`
   - The C# library will be at: `C:\TWS API\source\CSharpClient\client\bin\Release\net8.0\CSharpAPI.dll`
   - **Without this library, the solution will NOT compile**
   - Required by both OptionsExecutionService and TradingSupervisorService

4. **Interactive Brokers TWS or IB Gateway**
   - Download TWS: https://www.interactivebrokers.com/en/trading/tws.php
   - Or IB Gateway: https://www.interactivebrokers.com/en/trading/ibgateway-stable.php
   - **IMPORTANT**: Start with paper trading account

5. **Git** (for cloning repository)
   - Download: https://git-scm.com/downloads

### Optional Software

6. **Bun 1.x** (for dashboard development)
   - Download: https://bun.sh/
   - Only needed if building/modifying dashboard

7. **Visual Studio 2022** or **VS Code**
   - VS 2022: Full IDE with debugger
   - VS Code: Lightweight with C# extension

---

## Initial Setup

### Step 1: Clone Repository

```powershell
cd C:\Projects  # or your preferred location
git clone <repository-url> trading-system
cd trading-system
```

### Step 2: Restore Dependencies

```powershell
dotnet restore
```

This downloads all NuGet packages (Dapper, Serilog, IBApi, etc.).

### Step 3: Verify Solution Structure

```powershell
dir src
# Should show:
#   - SharedKernel
#   - TradingSupervisorService
#   - OptionsExecutionService

dir tests
# Should show:
#   - SharedKernel.Tests
#   - TradingSupervisorService.Tests
#   - OptionsExecutionService.Tests
```

---

## Build and Test

### Build the Solution

```powershell
# Build all projects
dotnet build -c Debug

# Build for Release (optimized)
dotnet build -c Release
```

Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### Run Tests

```powershell
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run tests for specific project
dotnet test tests/SharedKernel.Tests
```

Expected output:
```
Passed! - Failed: 0, Passed: 100+, Skipped: 0
```

If tests fail, check:
- .NET SDK version (`dotnet --version` must be 10.0.x)
- All dependencies restored (`dotnet restore`)
- No antivirus blocking test execution

---

## Configuration

### Step 4: Configure IBKR Connection

1. **Start IBKR Gateway/TWS** in paper trading mode
   - Login with paper trading credentials
   - Enable API connections:
     - **Path**: File → Global Configuration → API → Settings
     - ✅ **Enable ActiveX and Socket Clients** (MUST be checked)
     - ✅ Add `127.0.0.1` to **Trusted IP Addresses**
     - ❌ **Uncheck "Read-Only API"** (prevents order placement)
     - ✅ **Enable "Create API message log file"** (for troubleshooting)
   - Set Socket Port:
     - Paper Trading: **7497** (TWS) or **4002** (Gateway)
     - ⚠️ **NEVER use 7496 or 4001** (live trading ports - REAL MONEY!)
   
   **Troubleshooting**: If connection fails, see [IBKR Connection Issues](TROUBLESHOOTING.md#ibkr-connection-issues)

2. **Configure Supervisor Service**

Edit `src/TradingSupervisorService/appsettings.json`:

```json
{
  "TradingMode": "paper",
  "IbkrConnection": {
    "Host": "127.0.0.1",
    "Port": 7497,
    "ClientId": 1,
    "TradingMode": "paper"
  },
  "CloudflareWorker": {
    "BaseUrl": "http://localhost:8787",
    "ApiKey": "test-key-local-dev"
  },
  "Telegram": {
    "BotToken": "",
    "ChatId": "",
    "Enabled": false
  }
}
```

**Notes**:
- `Port 7497` = Paper TWS, `4002` = Paper Gateway
- `ClientId` must be unique (if running multiple clients, use 2, 3, etc.)
- Leave `Telegram.Enabled: false` for now (configure later)

3. **Configure Execution Service**

Edit `src/OptionsExecutionService/appsettings.json`:

```json
{
  "TradingMode": "paper",
  "IbkrConnection": {
    "Host": "127.0.0.1",
    "Port": 7497,
    "ClientId": 2,
    "TradingMode": "paper"
  },
  "StrategyPath": "strategies/examples/example-put-spread.json",
  "OrderSafety": {
    "MaxOrdersPerMinute": 10,
    "MaxPositionValue": 10000,
    "RequireConfirmation": false
  }
}
```

**Notes**:
- Use different `ClientId` (2) from Supervisor (1)
- `StrategyPath` points to example strategy (safe to test)
- Never commit real strategy files (use `strategies/private/` for custom strategies)

### Step 5: Create Data and Logs Directories

```powershell
# From repository root
mkdir data -Force
mkdir logs -Force
```

These directories will store:
- `data/supervisor.db` - Supervisor service database
- `data/options.db` - Execution service database
- `logs/supervisor-*.log` - Supervisor logs
- `logs/options-execution-*.log` - Execution logs

---

## Running Locally

### Method 1: Console Mode (Development)

Run both services in separate terminals:

**Terminal 1 - Supervisor Service:**
```powershell
cd src/TradingSupervisorService
dotnet run
```

**Terminal 2 - Execution Service:**
```powershell
cd src/OptionsExecutionService
dotnet run
```

You should see:
```
[INFO] TradingSupervisorService starting...
[INFO] Database migrated successfully
[INFO] IBKR connection established
[INFO] All workers started
```

Press `Ctrl+C` to stop each service.

### Method 2: With Debugger (Visual Studio)

1. Open `TradingSystem.sln` in Visual Studio
2. Right-click solution → **Set Startup Projects**
3. Select **Multiple startup projects**
4. Set both services to **Start**
5. Press `F5` to debug

---

## Deploy as Windows Service

### Step 6: Build for Deployment

```powershell
# Build self-contained executables (includes .NET runtime)
dotnet publish -c Release -r win-x64 --self-contained `
  src/TradingSupervisorService/TradingSupervisorService.csproj

dotnet publish -c Release -r win-x64 --self-contained `
  src/OptionsExecutionService/OptionsExecutionService.csproj
```

Output location:
- `src/TradingSupervisorService/bin/Release/net10.0/win-x64/publish/`
- `src/OptionsExecutionService/bin/Release/net10.0/win-x64/publish/`

### Step 7: Install Services

**IMPORTANT**: Run PowerShell as Administrator

```powershell
# Install Supervisor Service
.\infra\windows\install-supervisor.ps1

# Install Execution Service
.\infra\windows\install-options-engine.ps1
```

Expected output:
```
Creating service 'TradingSupervisorService'...
Starting service 'TradingSupervisorService'...
SUCCESS: Service 'TradingSupervisorService' is installed and running
```

### Step 8: Verify Services Running

```powershell
# Check service status
Get-Service TradingSupervisorService, OptionsExecutionService

# Should show:
# Status   Name                           DisplayName
# ------   ----                           -----------
# Running  TradingSupervisorService       Trading Supervisor Service
# Running  OptionsExecutionService        Options Execution Service
```

### Step 9: Check Logs

```powershell
# View latest supervisor log
Get-Content logs/supervisor-*.log -Tail 50

# View latest execution log
Get-Content logs/options-execution-*.log -Tail 50
```

Look for:
```
[INFO] Service started successfully
[INFO] Database migration completed
[INFO] IBKR connection established
[INFO] Background workers started
```

---

## Verify Installation

### Run Verification Script

```powershell
.\infra\windows\verify-installation.ps1
```

This checks:
- Services are running
- Databases exist and are accessible
- IBKR connection is active
- No critical errors in logs
- Configuration is valid

Expected output:
```
✓ TradingSupervisorService is running
✓ OptionsExecutionService is running
✓ supervisor.db exists (size: 120 KB)
✓ options.db exists (size: 80 KB)
✓ IBKR connection active
✓ No critical errors in last 100 log lines
✓ All checks passed
```

### Manual Verification

1. **Check Heartbeats**

```powershell
# Install SQLite CLI (if not installed)
# Download: https://sqlite.org/download.html

sqlite3 data/supervisor.db "SELECT service_name, last_seen_at, trading_mode FROM service_heartbeats;"
```

Should show recent heartbeats (within last 60 seconds):
```
TradingSupervisorService|2026-04-05T10:30:45Z|paper
OptionsExecutionService|2026-04-05T10:30:42Z|paper
```

2. **Check Alerts**

```powershell
sqlite3 data/supervisor.db "SELECT severity, message, created_at FROM alert_history ORDER BY created_at DESC LIMIT 10;"
```

Should show no critical alerts (warnings are OK during startup).

3. **Check Campaign Status**

```powershell
sqlite3 data/options.db "SELECT strategy_name, state, created_at FROM campaigns ORDER BY created_at DESC LIMIT 5;"
```

Should show campaign(s) in `Pending` or `Active` state.

---

## Security & Secrets Configuration

### Overview

The trading system uses **3 layers** of authentication and secrets:

1. **Cloudflare Worker Secrets** (optional) - For bot integration and AI features
2. **API Authentication Token** (required for Worker access) - Protects Worker endpoints
3. **IBKR Credentials** (required) - Already configured in appsettings.json

### Step 10: Cloudflare Worker Secrets (Optional)

These secrets enable optional features. **The Worker works without them** (features degrade gracefully).

#### What You Need

| Secret | Purpose | Required? | Get it from |
|--------|---------|-----------|-------------|
| `TELEGRAM_BOT_TOKEN` | Send Telegram alerts | No | @BotFather on Telegram |
| `DISCORD_PUBLIC_KEY` | Verify Discord slash commands | No | Discord Developer Portal |
| `CLAUDE_API_KEY` | Convert EasyLanguage → SDF | No | Anthropic Console |

#### Setup Instructions

**1. Create `.dev.vars` file** (local development):

```bash
cd infra/cloudflare/worker

# Copy template
cp .dev.vars.example .dev.vars

# Edit with your values
nano .dev.vars  # or your favorite editor
```

**`.dev.vars` content**:
```bash
# Telegram Bot (optional)
TELEGRAM_BOT_TOKEN=123456789:ABCdefGHIjklMNOpqrsTUVwxyz

# Discord Bot (optional)
DISCORD_PUBLIC_KEY=a1b2c3d4e5f6789abcdef0123456789abcdef0123456789abcdef0123456789

# Anthropic API (optional)
CLAUDE_API_KEY=sk-ant-api03-YOUR_KEY_HERE
```

**⚠️ Security**: `.dev.vars` is in `.gitignore` - never commit it!

**2. Deploy secrets to production** (when ready):

```bash
cd infra/cloudflare/worker

# Set each secret (paste value when prompted)
bunx wrangler secret put TELEGRAM_BOT_TOKEN
bunx wrangler secret put DISCORD_PUBLIC_KEY
bunx wrangler secret put CLAUDE_API_KEY
```

**3. Verify secrets** (production):

```bash
bunx wrangler secret list
# Shows:
# Name                   Created
# TELEGRAM_BOT_TOKEN     2026-04-18
# DISCORD_PUBLIC_KEY     2026-04-18
# CLAUDE_API_KEY         2026-04-18
```

#### Detailed Setup Guides

For detailed instructions on getting each secret, see:
- **Telegram Bot**: [Main README § Telegram Bot Token](../README.md#telegram-bot-token-for-real-time-alerts)
- **Discord Bot**: [Main README § Discord Bot Token](../README.md#discord-bot-token-for-cloudflare-worker-alerts---optional)
- **Anthropic API**: [Main README § Anthropic Claude API Key](../README.md#anthropic-claude-api-key-for-easylanguage-converter---optional)

### Step 11: API Authentication Token (Required for Worker Access)

This token authenticates requests from Dashboard and Windows Services to the Cloudflare Worker.

#### Generate Token

```bash
# Method 1: OpenSSL (Linux/Mac/WSL)
openssl rand -hex 32

# Method 2: PowerShell (Windows)
[System.Convert]::ToBase64String((1..32 | ForEach-Object {Get-Random -Minimum 0 -Maximum 256}))
```

**Example output**:
```
a3f5c8d9e2b1f4a6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0
```

**💾 Save this token** - you'll need it for 3 configurations below.

#### Configure Token (3 Places)

**1. Cloudflare Worker D1 Database** (whitelist table):

```bash
cd infra/cloudflare/worker

# STEP 1: Apply migrations (creates whitelist table)
bunx wrangler d1 migrations apply trading-db --remote

# STEP 2: Add token to whitelist (replace YOUR_TOKEN with generated token)
bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO whitelist (api_key, description) 
VALUES ('YOUR_TOKEN', 'Production Dashboard');
"

# STEP 3: Verify token was added
bunx wrangler d1 execute trading-db --remote --command="
SELECT api_key, description, created_at FROM whitelist;
"
```

**Expected output**:
```
┌────┬──────────────────────────────────────────────────────────────────┬──────────────────────┬─────────────────────┐
│ id │ api_key                                                          │ description          │ created_at          │
├────┼──────────────────────────────────────────────────────────────────┼──────────────────────┼─────────────────────┤
│ 1  │ 20c98b3f05c7a06a2fcca3168aeeb7df5d8401cc70d007bde589cead6ea95792 │ Production Dashboard │ 2026-04-18 20:28:57 │
└────┴──────────────────────────────────────────────────────────────────┴──────────────────────┴─────────────────────┘
```

**2. Dashboard Configuration**:

```bash
cd dashboard

# Create .env.local (NOT committed to git)
echo "VITE_API_KEY=YOUR_TOKEN" > .env.local
```

**`.env.local` should contain**:
```bash
# Dashboard → Worker authentication
VITE_API_KEY=a3f5c8d9e2b1f4a6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0

# Worker API URL (production)
VITE_API_URL=https://trading-bot.padosoft.workers.dev
```

**3. Windows Services Configuration**:

```bash
cd src/TradingSupervisorService

# Edit or create appsettings.Local.json
```

**`appsettings.Local.json` should contain**:
```json
{
  "CloudflareWorker": {
    "BaseUrl": "https://trading-bot.padosoft.workers.dev",
    "ApiKey": "a3f5c8d9e2b1f4a6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0"
  }
}
```

**Same for OptionsExecutionService** (if it calls Worker):
```bash
cd src/OptionsExecutionService
# Edit appsettings.Local.json with same CloudflareWorker section
```

#### Verify Authentication

**Test with curl**:

```bash
# Replace with your token and Worker URL
curl -H "X-Api-Key: YOUR_TOKEN" \
  https://trading-bot.padosoft.workers.dev/api/health

# Expected response:
# {"status":"ok","timestamp":"2026-04-18T10:30:00Z"}
```

**Test with dashboard**:

```bash
cd dashboard
npm run dev
# Open http://localhost:5173
# Check browser console for API requests
# Should see successful requests, NOT 401 Unauthorized
```

#### Security Best Practices

✅ **DO**:
- Generate 256-bit random tokens (32 bytes hex)
- Store in gitignored files (`.env.local`, `appsettings.Local.json`)
- Use different tokens for different environments (dev, prod)
- Rotate tokens periodically (e.g., every 90 days)

❌ **DON'T**:
- Commit tokens to git
- Share tokens in plain text (Slack, email)
- Reuse tokens across projects
- Use predictable tokens (e.g., "12345", "password")

#### Multiple Tokens (Advanced)

For production, use **different tokens** for each client:

```bash
# Generate 3 tokens
openssl rand -hex 32  # Dashboard
openssl rand -hex 32  # TradingSupervisorService
openssl rand -hex 32  # OptionsExecutionService

# Add all to D1 whitelist
bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO whitelist (api_key, description) VALUES 
('dashboard-token-here', 'Production Dashboard'),
('supervisor-token-here', 'TradingSupervisorService'),
('execution-token-here', 'OptionsExecutionService');
"
```

**Benefits**:
- Revoke individual clients without affecting others
- Track which client made which request
- Better audit trail

---

## Next Steps

### 1. Configure Bot Integration (Optional)

**For Alerts (One-Way)**:
See [Telegram Integration Guide](./telegram-integration.md) for:
- Creating Telegram bot
- Getting chat ID
- Configuring bot token (alerts only, no commands)

**For Interactive Commands** (`/status`, `/positions`, `/pnl`):
📚 **Complete Guide**: [Bot Setup Guide](./BOT_SETUP_GUIDE.md)

This guide explains:
- ⭐ **Bot User Whitelist**: What it is, when to use it, how to configure
- 🔐 **3 Whitelists Explained**: API keys vs User IDs vs Config (avoid confusion!)
- 📱 **How to find your User ID**: Telegram (@userinfobot) and Discord (Developer Mode)
- ⚙️ **Step-by-step setup**: Config files, D1 database, testing
- 🧪 **Troubleshooting**: Common issues and fixes
- 👥 **Multi-user management**: Add/remove users dynamically

**Quick Reference**:
```bash
# Get your Telegram user ID
# Telegram → Search @userinfobot → /start → Copy ID

# Configure whitelist (appsettings.Local.json)
{
  "Bots": {
    "Whitelist": "123456789"  // Your user ID
  }
}
```

### 2. Create Your First Strategy

See [Strategy Format Guide](./STRATEGY_FORMAT.md) for:
- Strategy JSON structure
- Entry/exit rules
- Risk management settings

Then:
1. Copy `strategies/examples/example-put-spread.json` to `strategies/private/my-strategy.json`
2. Customize settings
3. Update `StrategyPath` in `appsettings.json`
4. Restart service

### 3. Deploy Cloudflare Worker (Optional)

See [Deployment Guide](./DEPLOYMENT_GUIDE.md) for:
- Creating D1 database
- Deploying Worker
- Configuring Worker URL in services

### 4. Set Up Dashboard (Optional)

See [Dashboard Guide](./DASHBOARD_GUIDE.md) for:
- Building React app
- Deploying to Cloudflare Pages
- Configuring API endpoint

---

## Troubleshooting

### Service Won't Start

**Symptom**: Service starts then immediately stops

**Solutions**:
1. Check logs: `Get-Content logs/supervisor-*.log -Tail 100`
2. Common causes:
   - Database file locked (close SQLite browser)
   - Invalid configuration (run validator)
   - IBKR not running (start TWS/Gateway first)
   - Port conflict (change ClientId or Port)

### IBKR Connection Fails

**Symptom**: Logs show "Connection refused" or "Connection timeout"

**Solutions**:
1. Verify TWS/Gateway is running and logged in
2. Check API settings enabled (Configure → API → Enable Socket Clients)
3. Verify port number:
   - Paper TWS: 7497
   - Paper Gateway: 4002
   - NOT 7496 or 4001 (live ports)
4. Check firewall (allow localhost connections)
5. Verify ClientId is unique (no other client using same ID)

### Database Migration Fails

**Symptom**: Error "database is locked" or "migration failed"

**Solutions**:
1. Stop both services: `Stop-Service TradingSupervisorService, OptionsExecutionService`
2. Close any SQLite browser/viewer
3. Delete databases: `Remove-Item data/*.db, data/*.db-wal, data/*.db-shm`
4. Restart services (databases will be recreated)

### Tests Fail

**Symptom**: `dotnet test` shows failures

**Solutions**:
1. Check .NET version: `dotnet --version` (must be 10.0.x)
2. Clean and rebuild: `dotnet clean && dotnet build`
3. Restore packages: `dotnet restore`
4. Check specific test error messages
5. If "database locked": Close any SQLite connections

### Permissions Error

**Symptom**: "Access denied" when installing service

**Solutions**:
1. Run PowerShell as Administrator (required for service install)
2. Check UAC settings (might need to disable temporarily)
3. Verify user has local administrator rights

### Logs Not Created

**Symptom**: `logs/` directory is empty

**Solutions**:
1. Verify `logs/` directory exists: `mkdir logs -Force`
2. Check file permissions (service account needs write access)
3. Verify Serilog configuration in `appsettings.json`
4. Check Event Viewer for Windows Service errors

---

## Configuration Quick Reference

### Safe Testing Configuration

```json
{
  "TradingMode": "paper",
  "IbkrConnection": {
    "Host": "127.0.0.1",
    "Port": 7497,  // Paper TWS
    "ClientId": 1,
    "TradingMode": "paper"
  }
}
```

### Production Configuration

**DO NOT use until thoroughly tested in paper mode**

```json
{
  "TradingMode": "paper",  // KEEP as "paper" until ready
  "IbkrConnection": {
    "Host": "127.0.0.1",
    "Port": 7497,  // Keep paper port
    "ClientId": 1,
    "TradingMode": "paper"
  },
  "OrderSafety": {
    "MaxOrdersPerMinute": 5,  // Lower for production
    "MaxPositionValue": 50000,
    "RequireConfirmation": true  // Recommended
  }
}
```

**NEVER commit files with live credentials or trading mode to git!**

---

## Useful Commands

```powershell
# Check service status
Get-Service TradingSupervisorService, OptionsExecutionService

# Start services
Start-Service TradingSupervisorService
Start-Service OptionsExecutionService

# Stop services
Stop-Service TradingSupervisorService -Force
Stop-Service OptionsExecutionService -Force

# Restart services
Restart-Service TradingSupervisorService

# View real-time logs (requires Get-Content)
Get-Content logs/supervisor-*.log -Wait -Tail 50

# Query database
sqlite3 data/supervisor.db "SELECT * FROM service_heartbeats;"

# Build and test
dotnet build && dotnet test

# Update deployed service
.\infra\windows\update-services.ps1
```

---

## Support and Help

### Documentation

- [Architecture](./ARCHITECTURE.md) - System design and components
- [Configuration](./CONFIGURATION.md) - All config options
- [Strategy Format](./STRATEGY_FORMAT.md) - Strategy JSON reference
- [Troubleshooting](./TROUBLESHOOTING.md) - Common issues

### Logs

Check logs first for error details:
- `logs/supervisor-YYYYMMDD.log`
- `logs/options-execution-YYYYMMDD.log`

### Contact

For support, contact: lorenzo.padovani@padosoft.com

---

*Last updated: 2026-04-05 | Trading System v1.0*
