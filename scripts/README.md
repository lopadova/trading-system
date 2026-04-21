---
title: "Trading System Scripts"
tags: ["dev", "reference", "workflow"]
aliases: ["Scripts README"]
status: current
audience: ["developer"]
last-reviewed: "2026-04-21"
related:
  - "[[Scripts per Windows|scripts-windows]]"
  - "[[Changelog — Scripts & Workflow|CHANGELOG-SCRIPTS]]"
---

# Trading System Scripts

Automation scripts for building, testing, and deploying the Trading System.

## Overview

This directory contains system-wide scripts. Component-specific scripts are located in their respective directories:
- Windows Services: `infra/windows/`
- Cloudflare Worker: `infra/cloudflare/worker/scripts/`
- Dashboard: `dashboard/scripts/`

## Scripts

### pre-deployment-checklist.sh

**Purpose**: Comprehensive pre-deployment verification

**Usage**:
```bash
./scripts/pre-deployment-checklist.sh
```

**What it checks**:
- Git status (uncommitted changes, branch)
- .NET solution builds (Release mode)
- All tests pass
- Configuration files valid
- **TradingMode = "paper" (CRITICAL)**
- Gitignore rules (strategies/private/, *.db)
- Cloudflare Worker builds and tests pass
- Dashboard builds and tests pass
- Database migrations present
- No hardcoded secrets
- Documentation present

**Exit codes**:
- `0` - All checks passed
- `1` - One or more checks failed

**When to use**: Before every deployment to production

---

### check-knowledge.sh / check-knowledge.ps1

**Purpose**: Verify knowledge base files are up to date

**Usage**:
```bash
./scripts/check-knowledge.sh
```

**What it checks**:
- `knowledge/errors-registry.md` exists
- `knowledge/lessons-learned.md` exists
- `knowledge/skill-changelog.md` exists
- `knowledge/task-corrections.md` exists

**When to use**: As part of task completion verification

---

### reset-task.sh / reset-task.ps1

**Purpose**: Reset agent state for a specific task

**Usage**:
```bash
./scripts/reset-task.sh T-XX
```

**What it does**:
- Sets task status back to "pending" in `.agent-state.json`

**When to use**: When rerunning a task that failed

---

### run-agents.sh / run-agents.ps1

**Purpose**: Run tasks sequentially or in parallel

**Usage**:
```bash
./scripts/run-agents.sh
```

**When to use**: For automated task orchestration (experimental)

---

## Component-Specific Scripts

### Windows Services (`infra/windows/`)

#### install-supervisor.ps1
Install TradingSupervisorService as Windows Service
```powershell
.\infra\windows\install-supervisor.ps1
```

#### install-options-engine.ps1
Install OptionsExecutionService as Windows Service (Manual startup)
```powershell
.\infra\windows\install-options-engine.ps1
```

#### verify-installation.ps1
Verify Windows Services installation
```powershell
.\infra\windows\verify-installation.ps1 [-Verbose]
```

#### update-services.ps1
Update Windows Services with new binaries
```powershell
.\infra\windows\update-services.ps1 [-SupervisorOnly] [-OptionsOnly] [-SkipBackup]
```

#### uninstall-services.ps1
Uninstall Windows Services
```powershell
.\infra\windows\uninstall-services.ps1 [-KeepData] [-KeepLogs] [-Force]
```

---

### Cloudflare Worker (`infra/cloudflare/worker/scripts/`)

#### setup-d1.sh
Setup Cloudflare D1 database (first-time setup)
```bash
cd infra/cloudflare/worker
./scripts/setup-d1.sh
```

#### deploy.sh
Deploy Cloudflare Worker with pre-flight checks
```bash
cd infra/cloudflare/worker
./scripts/deploy.sh [--skip-tests] [--skip-build] [--env production]
```

#### rollback.sh
Rollback Cloudflare Worker to previous version
```bash
cd infra/cloudflare/worker
./scripts/rollback.sh
```

#### verify.sh
Verify Cloudflare Worker deployment (existing)
```bash
cd infra/cloudflare/worker
./scripts/verify.sh
```

---

### Dashboard (`dashboard/scripts/`)

#### build.sh
Build dashboard for production
```bash
cd dashboard
./scripts/build.sh [--skip-tests] [--skip-lint]
```

#### deploy.sh
Deploy dashboard to Cloudflare Pages or custom hosting
```bash
cd dashboard
./scripts/deploy.sh [--target cloudflare-pages|custom] [--skip-build]
```

#### verify-deployment.sh
Verify dashboard deployment health
```bash
cd dashboard
./scripts/verify-deployment.sh [URL]
```

---

## Typical Deployment Workflow

### First-Time Deployment

1. **Pre-flight check**:
   ```bash
   ./scripts/pre-deployment-checklist.sh
   ```

2. **Deploy Windows Services**:
   ```powershell
   cd infra/windows
   .\install-supervisor.ps1
   .\install-options-engine.ps1
   .\verify-installation.ps1 -Verbose
   ```

3. **Setup and deploy Cloudflare Worker**:
   ```bash
   cd infra/cloudflare/worker
   ./scripts/setup-d1.sh
   wrangler secret put API_KEY
   ./scripts/deploy.sh
   ```

4. **Build and deploy Dashboard**:
   ```bash
   cd dashboard
   ./scripts/build.sh
   ./scripts/deploy.sh --target cloudflare-pages
   ./scripts/verify-deployment.sh https://your-dashboard.pages.dev
   ```

### Updates

1. **Pre-flight check**:
   ```bash
   ./scripts/pre-deployment-checklist.sh
   ```

2. **Update Windows Services**:
   ```powershell
   cd infra/windows
   .\update-services.ps1
   .\verify-installation.ps1
   ```

3. **Update Cloudflare Worker**:
   ```bash
   cd infra/cloudflare/worker
   ./scripts/deploy.sh
   ```

4. **Update Dashboard**:
   ```bash
   cd dashboard
   ./scripts/build.sh
   ./scripts/deploy.sh
   ```

### Rollback

If deployment fails:

1. **Rollback Windows Services**:
   ```powershell
   # Restore from backup/ directory manually
   # Or redeploy previous version
   cd infra/windows
   .\update-services.ps1
   ```

2. **Rollback Cloudflare Worker**:
   ```bash
   cd infra/cloudflare/worker
   ./scripts/rollback.sh
   ```

3. **Rollback Dashboard**:
   - Use Cloudflare Pages deployment history
   - Or redeploy previous version from git

---

## CI/CD Pipeline

Automated testing and deployment via GitHub Actions:

**Location**: `.github/workflows/ci.yml`

**Triggers**:
- Push to main branch (tests only)
- Pull requests (tests only)
- Manual trigger via workflow_dispatch (tests + deploy)

**Jobs**:
1. `dotnet-build-test` - Build and test .NET services
2. `cloudflare-worker` - Build and test Worker
3. `dashboard` - Build and test Dashboard
4. `security` - Security checks (TradingMode, secrets, gitignore)
5. `deploy` - Deploy Worker and Dashboard (manual only)

**Running manually**:
1. Go to GitHub Actions tab
2. Select "CI/CD" workflow
3. Click "Run workflow"
4. Select branch and run

**Note**: Windows Services deployment is manual only (requires Windows Server access).

---

## Safety Checks

All deployment scripts include safety checks:

✓ **TradingMode validation** - Ensures OptionsExecutionService is in "paper" mode
✓ **Administrator checks** - Windows scripts require admin privileges
✓ **Pre-flight checks** - Verify configuration before deployment
✓ **Backups** - Create backups before updates
✓ **Confirmations** - Interactive prompts for destructive operations
✓ **No secrets in code** - All secrets via environment or prompts
✓ **Gitignore verification** - Prevent committing sensitive files

---

## Troubleshooting

### Script won't execute (Permission denied)

**Bash scripts**:
```bash
chmod +x ./scripts/*.sh
chmod +x ./infra/cloudflare/worker/scripts/*.sh
chmod +x ./dashboard/scripts/*.sh
```

**PowerShell scripts**:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Pre-deployment checklist fails

Run the script to see which checks fail:
```bash
./scripts/pre-deployment-checklist.sh
```

Fix each failure before proceeding with deployment.

### Windows Service install fails

1. Verify running as Administrator
2. Check binary exists at expected path
3. Check Event Viewer for errors
4. Review service logs in `logs/` directory

### Cloudflare deployment fails

1. Verify authenticated: `wrangler whoami`
2. Check wrangler.toml has no placeholders
3. Verify API_KEY secret is set: `wrangler secret list`
4. Check D1 database exists: `wrangler d1 list`

### Dashboard build fails

1. Check Node.js/Bun version
2. Verify dependencies installed: `bun install`
3. Run type check: `bun run typecheck`
4. Run tests: `bun test`

---

## Documentation

- **Deployment Guide**: `docs/DEPLOYMENT_GUIDE.md` - Complete deployment instructions
- **Coding Standards**: `CLAUDE.md` - Project guidelines and rules
- **Database Schema**: `infra/cloudflare/worker/migrations/*.sql` (source of truth)
- **Telegram Integration**: `docs/telegram-integration.md`
- **Knowledge Base**: `knowledge/` - Errors, lessons learned, corrections

---

## Support

For issues or questions:

1. Check script output for error messages
2. Review `DEPLOYMENT.md` troubleshooting section
3. Check `knowledge/errors-registry.md` for known issues
4. Review `knowledge/lessons-learned.md` for solutions
5. Run verification scripts to diagnose issues

---

*Last updated: 2026-04-05*
*Task: T-24 (Deployment Scripts + CI/CD)*
