# T-00 — Setup: Strategy Wizard + Bot

## Pre-Task Knowledge Check

### 1. Rules (auto-loaded)
.claude/rules/*.md già in context

### 2. claude-mem Search (se disponibile)
```
/mem-search "react wizard"
/mem-search "cloudflare worker"
/mem-search "telegram bot"
/mem-search "claude api"
```

### 3. Error Registry Check
```bash
grep -i "react\|cloudflare\|d1\|webhook" knowledge/errors-registry.md
```

### 4. Lessons Learned Check
```bash
grep -i "wizard\|bot\|api" knowledge/lessons-learned.md | tail -20
```

## Obiettivo

Preparare ambiente e infrastruttura per implementazione di:
1. **Strategy Wizard** (React dashboard)
2. **Bot Telegram/Discord** (Cloudflare Worker)

## Checklist

### Infrastructure Setup

- [ ] Verificare `.env` in `dashboard/` per React (se mancano chiavi)
- [ ] Verificare `infra/cloudflare/worker/wrangler.toml` (D1 bindings)
- [ ] Creare database D1 se non esiste:
  ```bash
  cd infra/cloudflare/worker
  wrangler d1 create trading-system-db
  # Aggiorna wrangler.toml con database_id
  ```
- [ ] Verificare secrets Cloudflare Worker:
  ```bash
  wrangler secret list
  # Expected: ANTHROPIC_API_KEY, TELEGRAM_BOT_TOKEN (se Telegram), DISCORD_BOT_TOKEN (se Discord)
  ```

### Dependencies Check

- [ ] Dashboard dependencies:
  ```bash
  cd dashboard
  bun install
  # Verifica: @anthropic-ai/sdk, zod, react-hook-form
  ```
- [ ] Worker dependencies:
  ```bash
  cd infra/cloudflare/worker
  bun install
  # Verifica: hono, @anthropic-ai/sdk
  ```

### Build Verification

- [ ] Build pulito baseline:
  ```bash
  dotnet build TradingSystem.sln
  # Expected: 0 errors
  ```
- [ ] Dashboard build:
  ```bash
  cd dashboard
  bun run build
  # Expected: build success
  ```
- [ ] Worker build:
  ```bash
  cd infra/cloudflare/worker
  bun run build
  # Expected: dist/ generato
  ```

### Schema Verification

- [ ] Leggere 00-DESIGN.md completamente
- [ ] Verificare schema D1 attuale vs richiesto (section 4.3 in design)
- [ ] Annotare migration SQL da creare (se necessario)

## Implementazione

### Step 1: Verifica Environment

```bash
# Check .NET
dotnet --version
# Expected: 10.0.x

# Check Bun
bun --version
# Expected: 1.x

# Check Wrangler
wrangler --version
# Expected: 3.x
```

### Step 2: Secrets Setup (se mancanti)

**ANTHROPIC_API_KEY** (per conversione EasyLanguage → SDF):
```bash
cd infra/cloudflare/worker
wrangler secret put ANTHROPIC_API_KEY
# Inserisci chiave quando richiesto
```

**TELEGRAM_BOT_TOKEN** (se usi Telegram):
```bash
wrangler secret put TELEGRAM_BOT_TOKEN
# Ottieni da @BotFather su Telegram
```

**DISCORD_BOT_TOKEN** (se usi Discord):
```bash
wrangler secret put DISCORD_BOT_TOKEN
# Ottieni da Discord Developer Portal
```

### Step 3: D1 Database Check

```bash
cd infra/cloudflare/worker

# Lista database esistenti
wrangler d1 list

# Se trading-system-db non esiste, crea:
wrangler d1 create trading-system-db

# Output: database_id = "..."
# Aggiorna wrangler.toml:
# [[d1_databases]]
# binding = "DB"
# database_name = "trading-system-db"
# database_id = "<id-qui>"
```

### Step 4: Verify Dependencies

```bash
# Dashboard
cd dashboard
bun install
bun run type-check  # Se presente

# Worker
cd infra/cloudflare/worker
bun install
```

## Test

- TEST-00-01: `dotnet build TradingSystem.sln` → 0 errors
- TEST-00-02: `cd dashboard && bun run build` → success
- TEST-00-03: `cd infra/cloudflare/worker && bun run build` → dist/ generato
- TEST-00-04: `wrangler secret list` → ANTHROPIC_API_KEY presente
- TEST-00-05: `wrangler d1 list` → trading-system-db presente

## Done Criteria

- [ ] Build .NET: PASS
- [ ] Build dashboard: PASS
- [ ] Build worker: PASS
- [ ] Secrets configurati (almeno ANTHROPIC_API_KEY)
- [ ] D1 database esistente (o creato)
- [ ] .agent-state.json: `"T-00": "done"`
- [ ] Log prodotto: `logs/T-00-result.md`

## Output

```json
{
  "task": "T-00",
  "status": "done",
  "infrastructure": {
    "dotnet_build": "success",
    "dashboard_build": "success",
    "worker_build": "success",
    "d1_database": "ready",
    "secrets": ["ANTHROPIC_API_KEY"]
  },
  "next_task": "T-01"
}
```

---

**Feature**: Strategy Wizard + Bot Telegram/Discord  
**Created**: 2026-04-06  
**Task**: Setup infrastructure e dependencies
