# Trading System — Automated Options Trading with IBKR

Production-ready automated trading system for options strategies via Interactive Brokers TWS/Gateway.

---

## Table of Contents

- [Documentation](#documentation)
  - [⭐ New Developers Start Here](#-new-developers-start-here)
  - [Getting Started](#getting-started)
  - [Architecture & Design](#architecture--design)
  - [Operations & Deployment](#operations--deployment)
  - [Development](#development)
  - [Knowledge Base](#knowledge-base)
  - [CI/CD](#cicd)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Key Features](#key-features)
- [Project Structure](#project-structure)
- [Development Workflow](#development-workflow)
- [Deployment](#deployment)
- [Monitoring](#monitoring)
- [Security Best Practices](#security-best-practices)
- [Troubleshooting](#troubleshooting)
- [Technology Stack](#technology-stack)
- [Support](#support)
- [License](#license)

---

## Documentation

### ⭐ New Developers Start Here
- **[Onboarding Guide](docs/ONBOARDING.md)** - 30-minute quick start for new team members
- **[Architecture Overview](docs/ARCHITECTURE_OVERVIEW.md)** - Complete system design and data flows

### Getting Started
- **[Getting Started Guide](docs/GETTING_STARTED.md)** - Installation and first steps
- **[Configuration Reference](docs/CONFIGURATION.md)** - All configuration options
- **[Strategy Format Guide](docs/STRATEGY_FORMAT.md)** - How to create strategies

### Architecture & Design
- **[Architecture Overview (NEW)](docs/ARCHITECTURE_OVERVIEW.md)** - System design, components, database schema
- **[Database Schema](docs/database-schema.md)** - Legacy schema reference (see ARCHITECTURE_OVERVIEW.md for latest)
- **[Telegram Integration](docs/telegram-integration.md)** - Alert configuration

### Operations
- **[Deployment Guide (NEW)](docs/DEPLOYMENT_GUIDE.md)** - Complete deployment instructions (manual + CI/CD)
- **[Deployment Guide (Legacy)](docs/DEPLOYMENT.md)** - Original deployment guide (T-24)
- **[Monitoring Guide](docs/MONITORING.md)** - Observability and alerting
- **[Troubleshooting](docs/TROUBLESHOOTING.md)** - Common issues and solutions

### Development
- **[Development Setup](DEVELOPMENT_SETUP.md)** - Complete environment setup (IDE, build, AVIRA/antivirus, strong-name signing)
- **[Onboarding](docs/ONBOARDING.md)** - Developer quick start (NEW)
- **[Contributing Guide](docs/CONTRIBUTING.md)** - How to extend the system
- **[Dashboard Guide](docs/DASHBOARD_GUIDE.md)** - React dashboard development
- **[Workflow Nuove Feature](docs/WORKFLOW-NUOVE-FEATURE.md)** - Complete workflow for new features
- **[Quick Start Nuova Feature](docs/QUICK-START-NUOVA-FEATURE.md)** - Quick reference for developers
- **[FAQ Workflow](docs/FAQ-WORKFLOW.md)** - Frequently asked questions

### Knowledge Base
- **[Errors Registry](knowledge/errors-registry.md)** - 11 documented errors with root causes and fixes
- **[Lessons Learned](knowledge/lessons-learned.md)** - 156 lessons from development
- **[Skills](\.claude\skills\)** - Coding patterns (.NET, Testing, SQLite, IBKR, Cloudflare)

### CI/CD
- **[GitHub Actions](.github/workflows/)** - Automated build, test, and deployment
  - `dotnet-build-test.yml` - .NET services build and test
  - `cloudflare-deploy.yml` - Worker and Dashboard deployment

---

## Quick Start

### Prerequisites

- **Windows Server 2019+** or Windows 10/11 Pro
- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/10.0))
- **Interactive Brokers TWS** or **IB Gateway** (paper trading account)
- **Bun 1.x** (optional, for dashboard development)

### 1. Build the Solution

```powershell
dotnet restore
dotnet build -c Release
dotnet test  # Verify all tests pass
```

### 2. Configure Services

**⚠️ IMPORTANT: Configuration File Strategy (.NET equivalent of `.env` / `.env.local`)**

.NET uses a **layered configuration** approach. Files are merged in this order (later overrides earlier):

| File | Git Status | Purpose | When to Edit |
|------|-----------|---------|--------------|
| `appsettings.json` | ✅ Committed | Default values (safe placeholders) | ❌ Never (shared by team) |
| `appsettings.Development.json` | ✅ Committed | Development overrides | ❌ Rarely (shared dev config) |
| `appsettings.Local.json` | ❌ **NOT committed** | **Your personal overrides** | ✅ **Always (your secrets)** |
| `appsettings.Production.json` | ❌ NOT committed | Production secrets | ⚠️ Only on server |

**✅ Correct workflow for developers:**

1. **Copy template files** (first time only):
   ```powershell
   # TradingSupervisorService
   cp src/TradingSupervisorService/appsettings.Local.json.example `
      src/TradingSupervisorService/appsettings.Local.json

   # OptionsExecutionService
   cp src/OptionsExecutionService/appsettings.Local.json.example `
      src/OptionsExecutionService/appsettings.Local.json
   ```

2. **Edit YOUR `appsettings.Local.json`** with your API keys, paths, etc.
   - ✅ These files are in `.gitignore` (safe to put secrets)
   - ✅ Loaded automatically by .NET (merged with other configs)
   - ✅ Never committed to git

3. **How configuration merge works:**
   ```
   appsettings.json              (base - committed)
   + appsettings.Development.json  (env override - committed)
   + appsettings.Local.json        (YOUR overrides - NOT committed)
   + Environment variables
   + Command line args
   = Final configuration at runtime
   ```

4. **Commit without fear:**
   ```powershell
   git status
   # ✅ Modified: appsettings.json (OK - only defaults)
   # ❌ NOT shown: appsettings.Local.json (already in .gitignore)
   
   git commit -m "Updated config"
   # Your secrets stay local! 🎉
   ```

**❌ NEVER edit files in `bin/Debug/` or `bin/Release/`** (overwritten on every build)

**✅ Why this pattern?**
- **Problem**: Editing `appsettings.json` with personal API keys → accidentally commit secrets to git
- **Solution**: Put personal overrides in `appsettings.Local.json` (gitignored, never committed)
- **Equivalent**: Same concept as `.env` (committed) vs `.env.local` (gitignored) in Node.js

---

**Edit `src/TradingSupervisorService/appsettings.json`:**
```json
{
  "TradingMode": "paper",  // ✅ SAFE default (never use "live" during development)
  "IBKR": {
    "Host": "127.0.0.1",
    "PaperPort": 4002,     // TWS Paper Trading (default)
    "LivePort": 4001,      // ⚠️ PRODUCTION ONLY
    "ClientId": 1
  }
}
```

**Edit `src/OptionsExecutionService/appsettings.json`:**
```json
{
  "TradingMode": "paper",  // ✅ CRITICAL: never commit "live" to git
  "Strategy": {
    "FilePath": "strategies/private/current.json",  // Relative to working directory
    "ReloadIntervalSeconds": 300
  },
  "IBKR": {
    "Host": "127.0.0.1",
    "PaperPort": 4002,
    "ClientId": 2          // Different from TradingSupervisor (1)
  }
}
```

**📂 Strategy File Path Notes:**
- Path is relative to the **working directory** (not `bin/`)
- For Windows Services: use **absolute paths** in Production:
  ```json
  "FilePath": "C:\\trading-system\\strategies\\private\\current.json"
  ```
- `strategies/private/` is in `.gitignore` (safe for production strategies)

See [Configuration Reference](docs/CONFIGURATION.md) for all options.
See [Configuration Checklist](docs/CONFIGURATION-CHECKLIST.md) to verify your setup.

### 2.1. API Keys Setup (Optional - Features Degrade Gracefully)

**IMPORTANT**: All API keys are completely optional. The application starts and runs without them:
- **No Telegram key**: Telegram alerting disabled, logs to file only
- **No Discord key**: Discord bot disabled, dashboard still works
- **No Anthropic key**: EasyLanguage converter disabled, manual conversion required

**The system never compromises functionality if keys are missing.**

#### Configuration Files Quick Reference

| API/Service | Config File | Purpose |
|-------------|-------------|---------|
| **Telegram Bot** | `src/TradingSupervisorService/appsettings.Production.json` | Real-time alerts from .NET service |
| **Discord Bot** | `infra/cloudflare/worker/.dev.vars` | Alerts from Cloudflare Worker |
| **Anthropic API** | `infra/cloudflare/worker/.dev.vars` | EasyLanguage → SDF conversion |
| **IBKR TWS** | `src/OptionsExecutionService/appsettings.json` | Trading connection settings |

**⚠️ Security**: All config files with secrets are in `.gitignore` - never commit tokens to git!

#### Telegram Bot Token (For Real-Time Alerts)

1. **Open Telegram** and search for `@BotFather`
2. **Start conversation** with BotFather (click "Start" or send `/start`)
3. **Create new bot**:
   - Send `/newbot` command
   - Choose name (e.g., "Trading System Alerts")
   - Choose username ending in `bot` (e.g., `pado_trading_alerts_bot`)
4. **Copy token**: BotFather replies with HTTP API token:
   ```
   Use this token to access the HTTP API:
   123456789:ABCdefGHIjklMNOpqrsTUVwxyz1234567890
   ```
5. **Get Chat ID**:
   - Send any message to your bot
   - Open in browser (replace `YOUR_BOT_TOKEN`):
     ```
     https://api.telegram.org/botYOUR_BOT_TOKEN/getUpdates
     ```
   - Copy `id` from `chat` object in JSON (negative for groups, positive for DMs)

6. **Add to TradingSupervisorService config** (`src/TradingSupervisorService/appsettings.Production.json`):
   ```json
   "Telegram": {
     "Enabled": true,
     "BotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz1234567890",
     "ChatId": YOUR_CHAT_ID_HERE
   }
   ```
   **⚠️ Nota**: `appsettings.Production.json` è in `.gitignore`, mai committare i token!

#### Discord Bot Token (For Cloudflare Worker Alerts - Optional)

> ⚠️ **Nota**: Discord è configurato nel **Cloudflare Worker**, non in TradingSupervisorService o Dashboard.

1. **Go to Discord Developer Portal**: https://discord.com/developers/applications

2. **Create Application**:
   - Click "New Application" (top right)
   - Name it (e.g., "Trading System Bot")
   - Click "Create"

3. **Configure Bot** (sidebar sinistro → "Bot"):
   - **Username**: Imposta un nome per il bot (es: "TradingBot")
   - **Public Bot**: Toggle OFF (bot privato, solo tu puoi invitarlo)
   - **Require OAuth2 Code Grant**: Toggle OFF (NON spuntare, altrimenti errore "integration require code grant")
   - **Privileged Gateway Intents** (scroll giù):
     - ✅ **SERVER MEMBERS INTENT**: Toggle ON
     - ✅ **MESSAGE CONTENT INTENT**: Toggle ON
   - **Save Changes** (importante!)

4. **Configure Installation** (sidebar sinistro → "Installation"):
   - **Installation Contexts**:
     - ✅ **Guild Install**: Toggle ON (installazione nei server)
     - ❌ **User Install**: Toggle OFF
   - **Default Install Link**: Lascia vuoto (None)
   - **Save Changes**

5. **Copy Bot Token**:
   - Torna nella sezione "Bot" → sotto "TOKEN"
   - Click "Reset Token" → Confirm
   - Click "Copy" (format: `YOUR_APP_ID_HERE.XXXXXX.YOUR_TOKEN_SECRET_HERE`)
   - **⚠️ Save immediately** (non potrai vederlo di nuovo senza reset)

6. **Copy Public Key** (REQUIRED for webhook verification):
   - Sidebar → "General Information"
   - Scroll down → sotto "APPLICATION ID"
   - Trova **"PUBLIC KEY"** (64-character hex string, e.g., `a1b2c3d4e5f6...`)
   - Click "Copy"
   - **⚠️ Purpose**: Discord signs all webhook requests (slash commands) with Ed25519 encryption.
     The worker verifies these signatures to ensure requests actually come from Discord (not attackers).
   - **⚠️ CRITICAL**: Without this key, slash commands `/status`, `/positions` will return error 500!

7. **Invite Bot to Server**:
   - **Metodo A - OAuth2 URL Generator** (consigliato):
     - Sidebar → "OAuth2" → "URL Generator"
     - **Scopes**: Spunta SOLO ✅ **`bot`** (nient'altro!)
     - **Bot Permissions** (appaiono dopo aver spuntato `bot`):
       - ✅ View Channels
       - ✅ Send Messages
       - ✅ Read Message History
     - Copia l'URL generato (deve contenere `permissions=68608`)
     - Apri l'URL nel browser → Seleziona server → Autorizza
   
   - **Metodo B - Link Diretto**:
     ```
     https://discord.com/oauth2/authorize?client_id=YOUR_APPLICATION_ID&permissions=68608&scope=bot
     ```
     (Sostituisci `YOUR_APPLICATION_ID` con il tuo Application ID dalla sezione "General Information")

7. **Enable Developer Mode in Discord**:
   - Apri Discord (desktop app o web)
   - Click sull'**icona ingranaggio** in basso a sinistra (accanto al tuo username) → Si aprono le **User Settings**
   - Scroll nel menu laterale sinistro → Cerca **"Advanced"** (o **"Avanzate"** se italiano)
   - **Developer Mode** → Toggle **ON** ✅
   - Chiudi le settings

8. **Get Channel ID**:
   - Torna al tuo server Discord
   - Vai nel canale dove vuoi ricevere alert dal bot
   - **Right-click sul nome del canale** (nella sidebar dei canali)
   - Nel menu contestuale vedrai: **"Copy Channel ID"** (o **"Copia ID canale"**)
   - Click → L'ID viene copiato (formato: 18 cifre, es: `987654321098765432`)
   - **⚠️ Nota**: Se non vedi "Copy Channel ID", Developer Mode non è abilitato (torna allo step 6)

9. **Add to Cloudflare Worker config** (`infra/cloudflare/worker/.dev.vars`):
   ```bash
   # Copy .dev.vars.example to .dev.vars
   cp infra/cloudflare/worker/.dev.vars.example infra/cloudflare/worker/.dev.vars
   
   # Edit .dev.vars with your values
   DISCORD_BOT_TOKEN=YOUR_APP_ID.XXXXXX.YOUR_SECRET_TOKEN
   DISCORD_PUBLIC_KEY=a1b2c3d4e5f6789abcdef...  # ⭐ REQUIRED for slash commands!
   DISCORD_CHANNEL_ID=987654321098765432
   ```

   **⚠️ Nota**: `.dev.vars` è in `.gitignore`, mai committare i token!
   
   **What each key does**:
   - `DISCORD_BOT_TOKEN` - Send messages to Discord (alerts)
   - `DISCORD_PUBLIC_KEY` - Verify webhook signatures (slash commands like `/status`)
   - `DISCORD_CHANNEL_ID` - Where to send alert messages

10. **Test Configuration**:
   ```powershell
   # Test Discord bot
   ./scripts/test-discord.ps1
   ```
   **Aspettativa**: Ricevi messaggio "✅ Test from Trading System" nel canale Discord

**Troubleshooting Discord Bot**:
- **403 Forbidden**: Bot non ha permessi → Verifica OAuth2 URL contenga `permissions=68608`
- **Bot non visibile in server**: Verifica "Installation" → "Guild Install" = ON, "User Install" = OFF
- **"integration require code grant"**: Vai in "Bot" → "Require OAuth2 Code Grant" = OFF
- **Bot non vede canali**: Verifica "Privileged Gateway Intents" abilitati PRIMA di rigenerare token
- **401 Unauthorized**: Token scaduto → Rigenera token in Developer Portal → Aggiorna `.dev.vars`

#### Anthropic Claude API Key (For EasyLanguage Converter - Optional)

> ⚠️ **Nota**: Anthropic API è usata dal **Cloudflare Worker** per convertire EasyLanguage → SDF.

1. **Sign up**: https://console.anthropic.com/

2. **Create API Key**:
   - Dashboard → "API Keys"
   - Click "Create Key"
   - Name it (e.g., "Trading System Worker")
   - Copy key (format: `sk-ant-api03-...`)
   - **⚠️ Save immediately** (non potrai vederla di nuovo)

3. **Add to Cloudflare Worker config** (`infra/cloudflare/worker/.dev.vars`):
   ```bash
   # Same file as Discord config
   ANTHROPIC_API_KEY=sk-ant-api03-REPLACE_WITH_YOUR_API_KEY
   ```

   **⚠️ Nota**: `.dev.vars` è in `.gitignore`, mai committare i token!

4. **Deploy secret to Cloudflare** (when ready for production):
   ```bash
   cd infra/cloudflare/worker
   npx wrangler secret put ANTHROPIC_API_KEY
   # Paste your key when prompted
   ```

### 3. Run Locally (Development)

```powershell
# Terminal 1 - Supervisor Service
cd src/TradingSupervisorService
dotnet run

# Terminal 2 - Execution Service
cd src/OptionsExecutionService
dotnet run
```

### 4. Install as Windows Services (Production)

```powershell
# Build for deployment
dotnet publish -c Release -r win-x64 --self-contained src/TradingSupervisorService/TradingSupervisorService.csproj
dotnet publish -c Release -r win-x64 --self-contained src/OptionsExecutionService/OptionsExecutionService.csproj

# Install (run as Administrator)
.\infra\windows\install-supervisor.ps1
.\infra\windows\install-options-engine.ps1
```

See [Getting Started Guide](docs/GETTING_STARTED.md) for detailed instructions.

### 5. Create Your Strategy

Copy `strategies/examples/example-put-spread.json` to `strategies/private/my-strategy.json` and customize.

**IMPORTANT:** Files in `strategies/private/` are git-ignored. Never commit real strategy configurations.

See [Strategy Format Guide](docs/STRATEGY_FORMAT.md) for complete reference.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Windows Server                            │
│                                                              │
│  ┌─────────────────────┐      ┌──────────────────────────┐ │
│  │ Supervisor Service  │      │ Execution Service        │ │
│  │                     │      │                          │ │
│  │ - Health Monitor    │      │ - Strategy Engine        │ │
│  │ - IBKR Monitor      │      │ - Order Placer           │ │
│  │ - Log Reader        │      │ - Campaign Manager       │ │
│  │ - Alert Creator     │      │ - Position Tracker       │ │
│  │ - Telegram Sender   │      │ - Market Data Service    │ │
│  │ - IVTS Monitor      │      │ - Greeks Monitor         │ │
│  │                     │      │                          │ │
│  │  supervisor.db      │      │  options.db              │ │
│  │  (SQLite + WAL)     │      │  (SQLite + WAL)          │ │
│  └──────────┬──────────┘      └───────────┬──────────────┘ │
│             │                             │                 │
└─────────────┼─────────────────────────────┼─────────────────┘
              │                             │
              └─────────────┬───────────────┘
                            │ HTTPS POST
                            ▼
                ┌───────────────────────────┐
                │  Cloudflare Worker        │
                │  - Event Ingestion        │
                │  - D1 Storage             │
                │  - REST API               │
                └───────────┬───────────────┘
                            │ REST API
                            ▼
                ┌───────────────────────────┐
                │  React Dashboard          │
                │  - Real-time Monitoring   │
                │  - Campaign Management    │
                │  - Risk Analytics         │
                └───────────────────────────┘
```

See [Architecture Overview](docs/ARCHITECTURE.md) for detailed design documentation.

---

## Key Features

### Safety Architecture
1. **Multi-Layer Validation**: Configuration defaults, runtime checks, git protection
2. **Default Paper Mode**: `TradingMode` defaults to `"paper"` in all configurations
3. **Git Protection**: `strategies/private/` automatically git-ignored
4. **Circuit Breaker**: Automatic order rejection after consecutive failures
5. **Safety-First Defaults**: Safe enum values, immutable configs

### Reliability
1. **Auto-Reconnection**: IBKR connection with exponential backoff
2. **Transactional Outbox**: Guaranteed event delivery to cloud
3. **WAL Mode**: Concurrent SQLite access without locking
4. **Graceful Degradation**: Services continue on component failure

### Observability
1. **Structured Logging**: Serilog JSON logs with correlation
2. **Heartbeat System**: 60-second health metrics
3. **Real-Time Alerts**: Telegram notifications for critical events
4. **Web Dashboard**: Live monitoring and control
5. **Comprehensive Tests**: 100+ unit and integration tests

---

## Project Structure

```
trading-system/
├── docs/                       # Documentation
│   ├── ARCHITECTURE.md         # System design
│   ├── GETTING_STARTED.md      # Installation guide
│   ├── CONFIGURATION.md        # Config reference
│   ├── STRATEGY_FORMAT.md      # Strategy JSON guide
│   ├── TROUBLESHOOTING.md      # Common issues
│   ├── MONITORING.md           # Observability
│   ├── CONTRIBUTING.md         # Development guide
│   ├── database-schema.md      # Schema reference
│   └── telegram-integration.md # Alert setup
├── src/
│   ├── SharedKernel/           # Domain types, shared utilities
│   ├── TradingSupervisorService/  # Health monitoring service
│   └── OptionsExecutionService/   # Strategy execution service
├── tests/                      # Unit and integration tests
├── dashboard/                  # React monitoring dashboard
├── infra/
│   ├── windows/                # Windows Service install scripts
│   └── cloudflare/worker/      # Cloudflare Worker API
├── strategies/
│   ├── examples/               # Example strategies (committed to git)
│   └── private/                # Your real strategies (git-ignored)
├── knowledge/                  # Self-improvement system
│   ├── errors-registry.md      # Known errors and fixes
│   ├── lessons-learned.md      # 75+ lessons from development
│   ├── skill-changelog.md      # Skill file version history
│   └── task-corrections.md     # Spec corrections
└── logs/                       # Runtime logs (git-ignored)
```

---

## Development Workflow

### Local Development

```powershell
# Build all projects
dotnet build

# Run tests
dotnet test

# Run services locally (2 terminals)
cd src/TradingSupervisorService && dotnet run
cd src/OptionsExecutionService && dotnet run

# Run dashboard (separate terminal, requires Bun)
cd dashboard && bun run dev
```

### Testing

#### Windows Defender Setup (IMPORTANT)

On Windows, Windows Defender Application Control may block test DLL execution with error `0x800711C7`. This affects **49 tests** in OptionsExecutionService.Tests.

**🚀 RECOMMENDED: All-in-One Unlock Script** (Administrator required):

```powershell
# This script does EVERYTHING:
# 1. Temporarily disables Windows Defender Real-Time Protection
# 2. Cleans + builds solution
# 3. Unblocks ALL test DLLs
# 4. Runs FULL test suite
# 5. Re-enables Windows Defender automatically

.\scripts\unlock-and-test-all.ps1
```

**Expected Result**: 276/278 tests PASS (99.3% coverage) ✅

---

**Alternative: Standard Setup** (if you prefer permanent exclusions):

```powershell
# Automated setup (requires Administrator privileges)
.\scripts\run-tests-with-exclusion.ps1

# If PowerShell policy blocks the script, use manual setup:
# See ADD_EXCLUSIONS_MANUAL.md for step-by-step GUI instructions
```

**What it does**:
1. Adds Windows Defender exclusions for project directories (permanent)
2. Cleans and rebuilds the solution
3. Unblocks pre-compiled DLLs
4. Runs the test suite

---

**Manual alternative** (if both scripts fail):

```powershell
# 1. Unblock existing DLLs
.\scripts\unblock-test-dlls.ps1

# 2. Add exclusions via Windows Security GUI
# See ADD_EXCLUSIONS_MANUAL.md for detailed steps

# 3. Clean and rebuild
dotnet clean
dotnet build
dotnet test
```

---

📖 **Complete Guide**: See [WINDOWS_DEFENDER_UNLOCK.md](WINDOWS_DEFENDER_UNLOCK.md) for:
- Detailed troubleshooting
- CI/CD alternatives (GitHub Actions, WSL2, Docker)
- Expected test results breakdown

#### Running Tests

```powershell
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/SharedKernel.Tests

# Watch mode (auto-run on file change)
dotnet watch test --project tests/SharedKernel.Tests

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

See [Contributing Guide](docs/CONTRIBUTING.md) for coding standards and PR process.

---

## Deployment

### Windows Services

```powershell
# Build for deployment
dotnet publish -c Release -r win-x64 --self-contained src/TradingSupervisorService/TradingSupervisorService.csproj
dotnet publish -c Release -r win-x64 --self-contained src/OptionsExecutionService/OptionsExecutionService.csproj

# Install (run as Administrator)
.\infra\windows\install-supervisor.ps1
.\infra\windows\install-options-engine.ps1

# Verify installation
.\infra\windows\verify-installation.ps1
```

### Cloudflare Worker

```bash
cd infra/cloudflare/worker

# Create D1 database
wrangler d1 create trading-db

# Update wrangler.toml with database_id

# Deploy migrations
wrangler d1 execute trading-db --file=migrations/001_initial.sql

# Deploy Worker
wrangler deploy

# Set secrets
wrangler secret put API_KEY
```

### React Dashboard

```bash
cd dashboard

# Build for production
bun run build

# Deploy to Cloudflare Pages (or any static host)
bun run deploy
```

See [Deployment Guide](docs/DEPLOYMENT.md) for complete instructions.

---

## Monitoring

### Health Checks

```powershell
# Check service status
Get-Service TradingSupervisorService, OptionsExecutionService

# View latest heartbeats
sqlite3 data/supervisor.db "SELECT * FROM service_heartbeats ORDER BY last_seen_at DESC LIMIT 5;"

# Check recent alerts
sqlite3 data/supervisor.db "SELECT severity, message, created_at FROM alert_history ORDER BY created_at DESC LIMIT 10;"

# View logs
Get-Content logs/supervisor-*.log -Tail 50
Get-Content logs/options-execution-*.log -Tail 50
```

### Dashboard

Access web dashboard at configured URL to monitor:
- Service health and uptime
- Active campaigns and positions
- Real-time P&L and Greeks
- Alert history
- System configuration

See [Monitoring Guide](docs/MONITORING.md) for comprehensive observability documentation.

---

## Security Best Practices

### Development
- **Never commit secrets**: Use User Secrets or environment variables
- **Git ignore**: `strategies/private/`, `appsettings.Production.json`, `.env.production`
- **User Secrets**: `dotnet user-secrets set "Telegram:BotToken" "your-token"`

### Production
- **Minimal Permissions**: Run services as dedicated service account
- **File Permissions**: Restrict config files to service account only
- **API Keys**: Store in Cloudflare secrets or Azure Key Vault
- **Network**: Firewall rules for IBKR connection only
- **Backups**: Daily automated backups of SQLite databases

### Trading Safety
- **Always test in paper mode first** (weeks/months of testing)
- **Never modify safety validators** without thorough review
- **Monitor alerts closely** during first weeks
- **Review all orders** in order tracking table
- **Set conservative position limits** initially

---

## Troubleshooting

### Common Issues

**Service won't start**: Check logs, verify .NET 10 SDK, check database locks  
**IBKR connection fails**: Verify TWS/Gateway running, check port config, verify API enabled  
**Tests fail**: Check .NET SDK version, restore packages, close SQLite browsers  
**Dashboard shows no data**: Verify Worker deployed, check CORS config, check API key

See [Troubleshooting Guide](docs/TROUBLESHOOTING.md) for detailed solutions.

---

## Technology Stack

**Backend**: .NET 10, C# 13, SQLite, Dapper, Serilog, IBApi, Telegram.Bot  
**Frontend**: React 19, TypeScript 5.9, Vite 8, Tailwind CSS 4, React Query 5, Zustand 5  
**Infrastructure**: Cloudflare Workers, Cloudflare D1, Hono, Windows Services  
**Testing**: xUnit, Vitest, Playwright

---

## Support

### Documentation
Start with [Getting Started Guide](docs/GETTING_STARTED.md) for installation.

### Issues
1. Check [Troubleshooting Guide](docs/TROUBLESHOOTING.md)
2. Review logs for error details
3. Search knowledge base in `knowledge/`

---

Developed with ❤️ by Lorenzo Padovani Padosoft for accelerating enterprise development with AI tools.
