# Trading System Deployment Guide

Complete guide for deploying the Trading System to production.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Pre-Deployment Checklist](#pre-deployment-checklist)
3. [Windows Services Deployment](#windows-services-deployment)
4. [Cloudflare Worker Deployment](#cloudflare-worker-deployment)
5. [Dashboard Deployment](#dashboard-deployment)
6. [Post-Deployment Verification](#post-deployment-verification)
7. [Rollback Procedures](#rollback-procedures)
8. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Windows Server

- Windows Server 2019 or later (or Windows 10/11 Pro)
- .NET 10 SDK or Runtime installed
- Administrator access
- PowerShell 5.1 or later

### Cloudflare Account

- Cloudflare account with Workers enabled
- Wrangler CLI installed: `npm install -g wrangler`
- Authenticated: `wrangler login`

### Build Tools

- .NET 10 SDK (for building services)
- Bun (for building worker and dashboard)
- Git (for version control)

---

## Pre-Deployment Checklist

Run the automated checklist before deployment:

```bash
./scripts/pre-deployment-checklist.sh
```

This verifies:
- ✓ Git status (no uncommitted changes)
- ✓ .NET solution builds
- ✓ All tests pass
- ✓ TradingMode = "paper" (SAFETY)
- ✓ Configuration files valid
- ✓ No hardcoded secrets
- ✓ Gitignore rules correct
- ✓ Worker and dashboard build

**Do not proceed if any critical checks fail.**

---

## Windows Services Deployment

### 1. Build Services

Build both services for Windows x64:

```bash
# Build TradingSupervisorService
dotnet publish src/TradingSupervisorService/TradingSupervisorService.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true

# Build OptionsExecutionService
dotnet publish src/OptionsExecutionService/OptionsExecutionService.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true
```

### 2. Install TradingSupervisorService

Run as Administrator:

```powershell
cd infra/windows
.\install-supervisor.ps1
```

This will:
- Create Windows Service
- Set to automatic startup
- Configure failure recovery
- Start the service

### 3. Install OptionsExecutionService

Run as Administrator:

```powershell
cd infra/windows
.\install-options-engine.ps1
```

**IMPORTANT:** This service is set to **Manual startup** for safety.
Review configuration before starting.

### 4. Verify Installation

```powershell
.\verify-installation.ps1 -Verbose
```

Expected output: All checks PASS

### 5. Start OptionsExecutionService (when ready)

```powershell
Start-Service -Name OptionsExecutionService
```

---

## Cloudflare Worker Deployment

### 1. Setup D1 Database

First-time setup only:

```bash
cd infra/cloudflare/worker
./scripts/setup-d1.sh
```

This will:
- Create D1 database
- Apply migrations
- Update wrangler.toml with database ID

### 2. Configure Secrets

Set the API key for worker authentication:

```bash
wrangler secret put API_KEY
# Enter a strong random key when prompted
```

### 3. Update Configuration

Edit `wrangler.toml`:

```toml
[vars]
DASHBOARD_ORIGIN = "https://your-dashboard-domain.com"
```

### 4. Deploy Worker

```bash
./scripts/deploy.sh
```

This will:
- Run pre-flight checks
- Run tests
- Build TypeScript
- Validate database schema
- Deploy to Cloudflare

### 5. Verify Deployment

```bash
curl https://trading-system.<your-subdomain>.workers.dev/api/v1/health
```

Expected response: `{"ok": true, "ts": "..."}`

---

## Dashboard Deployment

### 1. Build Dashboard

```bash
cd dashboard
./scripts/build.sh
```

This will:
- Run type checking
- Run linting
- Run tests
- Build optimized production bundle

### 2. Configure Environment

Create `.env.production` (NOT committed to git):

```env
VITE_API_BASE_URL=https://trading-system.<your-subdomain>.workers.dev
VITE_API_KEY=<same-key-as-worker>
```

### 3. Deploy to Cloudflare Pages

```bash
./scripts/deploy.sh --target cloudflare-pages
```

Enter project name when prompted (e.g., `trading-dashboard`).

### 4. Configure Environment Variables in Cloudflare

Go to Cloudflare Pages dashboard and set:

- `VITE_API_BASE_URL` - Worker URL
- `VITE_API_KEY` - API authentication key

### 5. Verify Deployment

```bash
./scripts/verify-deployment.sh https://trading-dashboard.pages.dev
```

Or open in browser and test manually.

---

## Post-Deployment Verification

### Windows Services

1. Check service status:
   ```powershell
   Get-Service TradingSupervisorService, OptionsExecutionService
   ```

2. Check logs:
   ```powershell
   Get-Content logs\supervisor-*.log -Tail 50
   Get-Content logs\options-*.log -Tail 50
   ```

3. Verify database connections:
   ```bash
   sqlite3 data/supervisor.db "SELECT COUNT(*) FROM events;"
   sqlite3 data/options.db "SELECT COUNT(*) FROM positions;"
   ```

### Cloudflare Worker

1. Test health endpoint:
   ```bash
   curl https://trading-system.<subdomain>.workers.dev/api/v1/health
   ```

2. Test authenticated endpoint:
   ```bash
   curl -H "X-Api-Key: <your-key>" \
     https://trading-system.<subdomain>.workers.dev/api/v1/system/status
   ```

3. Check D1 database:
   ```bash
   wrangler d1 execute trading-db --command "SELECT COUNT(*) FROM events;"
   ```

### Dashboard

1. Open in browser: `https://trading-dashboard.pages.dev`
2. Check browser console for errors
3. Test authentication
4. Verify all pages load
5. Test API connectivity

---

## Rollback Procedures

### Windows Services

Rollback to previous version:

```powershell
cd infra/windows
.\update-services.ps1
```

If update fails, restore from backup in `backup/` directory.

### Cloudflare Worker

Rollback using git:

```bash
cd infra/cloudflare/worker
./scripts/rollback.sh
```

Choose option 1 (git commit) and enter the commit hash of the previous working version.

### Dashboard

Cloudflare Pages keeps deployment history. Rollback from dashboard:

1. Go to Cloudflare Pages project
2. Click "Deployments"
3. Find previous working deployment
4. Click "Rollback to this deployment"

---

## Troubleshooting

### Windows Services Won't Start

1. Check Event Viewer:
   ```
   Event Viewer > Windows Logs > Application
   Filter by Source: ".NET Runtime"
   ```

2. Check service logs:
   ```powershell
   Get-Content logs\supervisor-*.log -Tail 100
   ```

3. Common issues:
   - Database file locked (restart service)
   - Configuration file missing (restore from backup)
   - Port already in use (check IBKR configuration)

### Cloudflare Worker Errors

1. Check deployment logs:
   ```bash
   wrangler tail
   ```

2. Check D1 database connection:
   ```bash
   wrangler d1 info trading-db
   ```

3. Common issues:
   - API_KEY secret not set (run `wrangler secret put API_KEY`)
   - Database migrations not applied (run `wrangler d1 migrations apply`)
   - CORS issues (check DASHBOARD_ORIGIN in wrangler.toml)

### Dashboard Not Loading

1. Check browser console for errors
2. Verify API endpoint is accessible
3. Check CORS configuration in worker
4. Verify environment variables are set in Cloudflare Pages
5. Test with curl:
   ```bash
   curl -I https://trading-dashboard.pages.dev
   ```

---

## Maintenance

### Update Services

```powershell
cd infra/windows
.\update-services.ps1
```

This will:
- Backup current binaries
- Build new version
- Stop services
- Restart with new binaries

### Update Worker

```bash
cd infra/cloudflare/worker
./scripts/deploy.sh
```

### Update Dashboard

```bash
cd dashboard
./scripts/build.sh
./scripts/deploy.sh
```

---

## Safety Reminders

1. **ALWAYS** verify TradingMode = "paper" before starting OptionsExecutionService
2. **NEVER** commit secrets to git
3. **ALWAYS** run pre-deployment checklist before deploying
4. **BACKUP** databases before major updates
5. **TEST** in paper trading mode first
6. **MONITOR** logs after deployment for errors
7. **VERIFY** all health checks pass before considering deployment complete

---

## Support

For issues or questions:

1. Check logs first (Windows Services, Cloudflare Worker tail, browser console)
2. Run verification scripts
3. Review CLAUDE.md for coding standards
4. Check knowledge/errors-registry.md for known issues
5. Review knowledge/lessons-learned.md for solutions

---

*Last updated: 2026-04-05*
*Deployment version: v1.0*
