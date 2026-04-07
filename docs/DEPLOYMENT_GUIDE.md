# Trading System - Deployment Guide

**Last Updated**: 2026-04-07  
**Version**: 1.0.0

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Quick Start](#quick-start)
4. [Deployment Steps](#deployment-steps)
5. [Post-Deployment Verification](#post-deployment-verification)
6. [CI/CD Pipeline](#cicd-pipeline)
7. [Troubleshooting](#troubleshooting)

---

## Overview

The Trading System consists of three main components:

1. **Windows Services** (.NET 10):
   - `TradingSupervisorService`: Monitors health, IBKR connection, metrics collection
   - `OptionsExecutionService`: Executes trading strategies, manages campaigns

2. **Cloudflare Worker** (TypeScript + Hono):
   - Bot API (Telegram + Discord)
   - Strategy conversion API
   - D1 database for whitelist and logs

3. **Dashboard** (React + TypeScript):
   - Strategy wizard UI
   - Campaign management
   - System monitoring

---

## Prerequisites

### Windows Server (for .NET Services)

- Windows Server 2019+ or Windows 10/11 Pro
- .NET 10 Runtime (or SDK for build)
- Administrator privileges
- PowerShell 5.1+

### Cloudflare (for Worker + Dashboard)

- Cloudflare account with Workers enabled
- Wrangler CLI: `npm install -g wrangler`
- Authenticated: `wrangler login`

### Build Tools (for local deployment)

- .NET 10 SDK
- Bun (latest): `curl -fsSL https://bun.sh/install | bash`
- Git

---

## Quick Start

### Option 1: Manual Deployment

```bash
# 1. Build .NET services
dotnet publish src/TradingSupervisorService/TradingSupervisorService.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/TradingSupervisorService
dotnet publish src/OptionsExecutionService/OptionsExecutionService.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/OptionsExecutionService

# 2. Deploy Windows Services (PowerShell as Admin)
.\scripts\deploy-windows-services.ps1 -Action Install

# 3. Deploy Cloudflare Worker
cd infra/cloudflare/worker
bun install
bunx wrangler deploy

# 4. Deploy Dashboard
cd dashboard
bun install
bun run build
bunx wrangler pages deploy dist --project-name=trading-dashboard
```

### Option 2: CI/CD (Recommended)

Push to `main` branch triggers automatic deployment:

```bash
git add .
git commit -m "feat: deploy to production"
git push origin main
```

GitHub Actions will:
- ✅ Build and test .NET services
- ✅ Build and test Worker + Dashboard
- ✅ Deploy Worker to Cloudflare
- ✅ Deploy Dashboard to Cloudflare Pages
- ✅ Create deployment artifacts

**Note**: Windows Services still require manual installation (security requirement).

---

## Deployment Steps

### 1. Pre-Deployment Checklist

Run automated checks:

```bash
./scripts/pre-deployment-checklist.sh
```

Verify:
- [ ] Git on `main` branch
- [ ] No uncommitted changes
- [ ] All tests pass
- [ ] `TradingMode = "paper"` in appsettings.json
- [ ] No hardcoded secrets

### 2. Build Services

#### .NET Services

```bash
# TradingSupervisorService
dotnet publish src/TradingSupervisorService/TradingSupervisorService.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/TradingSupervisorService

# OptionsExecutionService
dotnet publish src/OptionsExecutionService/OptionsExecutionService.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o publish/OptionsExecutionService
```

Output: `publish/TradingSupervisorService/TradingSupervisorService.exe` (~76MB)

#### Cloudflare Worker

```bash
cd infra/cloudflare/worker
bun install
bun run build   # TypeScript compilation
bun test        # Run tests
```

#### Dashboard

```bash
cd dashboard
bun install
bun run build   # Vite build
# Output: dashboard/dist/
```

### 3. Deploy Windows Services

**PowerShell (as Administrator)**:

```powershell
# Install (first time)
.\scripts\deploy-windows-services.ps1 -Action Install

# Update (subsequent deployments)
.\scripts\deploy-windows-services.ps1 -Action Update

# Restart services
.\scripts\deploy-windows-services.ps1 -Action Restart

# Uninstall
.\scripts\deploy-windows-services.ps1 -Action Uninstall
```

**WhatIf mode** (dry run):

```powershell
.\scripts\deploy-windows-services.ps1 -Action Install -WhatIf
```

**Custom install path**:

```powershell
.\scripts\deploy-windows-services.ps1 -Action Install -InstallPath "D:\TradingSystem"
```

Services will be installed at:
- `C:\TradingSystem\TradingSupervisorService\`
- `C:\TradingSystem\OptionsExecutionService\`

### 4. Deploy Cloudflare Worker

```bash
cd infra/cloudflare/worker

# Deploy to production
bunx wrangler deploy

# Deploy to preview
bunx wrangler deploy --env preview
```

**Required secrets** (set once):

```bash
bunx wrangler secret put TELEGRAM_BOT_TOKEN
bunx wrangler secret put DISCORD_PUBLIC_KEY
bunx wrangler secret put CLAUDE_API_KEY
```

### 5. Deploy Dashboard

```bash
cd dashboard

# Deploy to Cloudflare Pages
bunx wrangler pages deploy dist --project-name=trading-dashboard

# Or use Cloudflare Pages GitHub integration (automatic on push)
```

---

## Post-Deployment Verification

### Windows Services

```powershell
# Check service status
Get-Service TradingSupervisorService, OptionsExecutionService

# View logs (Event Viewer)
Get-EventLog -LogName Application -Source TradingSupervisorService -Newest 10
Get-EventLog -LogName Application -Source OptionsExecutionService -Newest 10

# Check database files
Test-Path C:\TradingSystem\data\supervisor.db
Test-Path C:\TradingSystem\data\options.db
```

Expected output:
```
Status   Name                           DisplayName
------   ----                           -----------
Running  TradingSupervisorService       Trading Supervisor Service
Running  OptionsExecutionService        Options Execution Service
```

### Cloudflare Worker

```bash
# Health check
curl https://trading-bot.padosoft.workers.dev/health

# Expected response:
# {"status":"ok","timestamp":"2026-04-07T10:00:00.000Z","version":"1.0.0"}
```

### Dashboard

Visit: https://trading-dashboard.pages.dev

Expected:
- ✅ Page loads
- ✅ Strategy wizard accessible
- ✅ API connection working

---

## CI/CD Pipeline

### GitHub Actions Workflows

#### 1. `.github/workflows/dotnet-build-test.yml`

**Triggers**: Push/PR to `main` or `develop`

**Jobs**:
1. `build-and-test`: Build solution, run tests
2. `publish-services`: Create Windows Service artifacts (on `main` only)

**Artifacts**:
- `build-artifacts`: Build output (7 days retention)
- `windows-services`: Published .exe files (30 days retention)

#### 2. `.github/workflows/cloudflare-deploy.yml`

**Triggers**: 
- Push to `main` (paths: `infra/cloudflare/worker/**`, `dashboard/**`)
- Manual trigger (`workflow_dispatch`)

**Jobs**:
1. `test-worker`: Build and test Worker
2. `test-dashboard`: Build and test Dashboard
3. `deploy-worker`: Deploy Worker to Cloudflare (on `main` only)
4. `deploy-dashboard`: Deploy Dashboard to Cloudflare Pages (on `main` only)

**Required Secrets**:
- `CLOUDFLARE_API_TOKEN`: Cloudflare API token with Workers and Pages permissions
- `CLOUDFLARE_ACCOUNT_ID`: Your Cloudflare account ID

### Setup GitHub Secrets

1. Go to repository Settings → Secrets and variables → Actions
2. Add secrets:

```
CLOUDFLARE_API_TOKEN=<your-token>
CLOUDFLARE_ACCOUNT_ID=<your-account-id>
```

Get tokens from: https://dash.cloudflare.com/profile/api-tokens

Required permissions:
- Workers Scripts:Edit
- Workers KV Storage:Edit
- Pages:Edit

---

## Configuration

### appsettings.json

**CRITICAL**: Always verify `TradingMode` before deployment:

```bash
grep -i "tradingmode" src/*/appsettings.json
```

Expected output:
```
src/OptionsExecutionService/appsettings.json:  "TradingMode": "paper",
src/TradingSupervisorService/appsettings.json:  "TradingMode": "paper",
```

**Live trading** (production only, explicit approval required):

```json
{
  "TradingMode": "live"
}
```

### Database Paths

Default paths (Windows):
```
C:\TradingSystem\data\supervisor.db
C:\TradingSystem\data\options.db
```

Override in `appsettings.json`:
```json
{
  "Sqlite": {
    "SupervisorDbPath": "D:\\Data\\supervisor.db"
  },
  "OptionsDb": {
    "OptionsDbPath": "D:\\Data\\options.db"
  }
}
```

---

## Troubleshooting

### Windows Services

#### Service won't start

1. Check Event Viewer:
   ```powershell
   Get-EventLog -LogName Application -Source TradingSupervisorService -Newest 5
   ```

2. Check permissions:
   ```powershell
   icacls "C:\TradingSystem\TradingSupervisorService"
   # Should show: NT AUTHORITY\SYSTEM:(OI)(CI)(F)
   ```

3. Run manually (debug mode):
   ```powershell
   cd C:\TradingSystem\TradingSupervisorService
   .\TradingSupervisorService.exe
   ```

#### Database locked errors

SQLite busy_timeout issues:

1. Check for multiple service instances
2. Verify WAL mode:
   ```sql
   PRAGMA journal_mode;  -- Should return 'wal'
   ```

### Cloudflare Worker

#### Deployment fails

```bash
# Check wrangler authentication
bunx wrangler whoami

# Re-authenticate
bunx wrangler login

# Deploy with verbose output
bunx wrangler deploy --verbose
```

#### D1 migrations not applied

```bash
# List migrations
bunx wrangler d1 migrations list trading-db

# Apply manually
bunx wrangler d1 migrations apply trading-db --remote
```

### Dashboard

#### Build fails with TypeScript errors in tests

Use production build (excludes tests):

```bash
bun vite build
# Instead of: bun run build (which runs tsc -b && vite build)
```

Or fix `package.json`:
```json
{
  "scripts": {
    "build": "vite build",
    "typecheck": "tsc -b"
  }
}
```

---

## Rollback Procedures

### Windows Services

```powershell
# Stop services
Stop-Service TradingSupervisorService, OptionsExecutionService

# Restore previous version (if backed up)
Copy-Item -Path "C:\TradingSystem\backup\TradingSupervisorService\*" `
          -Destination "C:\TradingSystem\TradingSupervisorService\" `
          -Recurse -Force

# Restart services
Start-Service TradingSupervisorService, OptionsExecutionService
```

### Cloudflare Worker

```bash
# List deployments
bunx wrangler deployments list

# Rollback to specific version
bunx wrangler rollback --deployment-id=<deployment-id>
```

### Dashboard (Cloudflare Pages)

1. Go to Cloudflare dashboard
2. Navigate to Pages → trading-dashboard → Deployments
3. Click "..." → Rollback on desired version

---

## Support

- **Documentation**: `docs/`
- **Issues**: GitHub Issues
- **Knowledge Base**: `knowledge/errors-registry.md`, `knowledge/lessons-learned.md`
- **Skills**: `.claude/skills/`

---

**Last verified**: 2026-04-07  
**Author**: Trading System Team  
**Version**: 1.0.0
