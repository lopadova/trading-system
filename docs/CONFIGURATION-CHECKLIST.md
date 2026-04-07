# Configuration Checklist

Usa questa checklist per verificare di aver configurato correttamente tutte le API keys.

## 1. File da Creare (da .example)

### TradingSupervisorService
```bash
# Se non esiste, crea da template
cd src/TradingSupervisorService
cp appsettings.json appsettings.Production.json
```

**Editing**: `appsettings.Production.json`
- [ ] Telegram `BotToken` configurato (o `Enabled: false`)
- [ ] Telegram `ChatId` configurato
- [ ] File in `.gitignore` (verifica mai committato)

### Cloudflare Worker
```bash
cd infra/cloudflare/worker
cp .dev.vars.example .dev.vars
```

**Editing**: `.dev.vars`
- [ ] `ANTHROPIC_API_KEY` configurato (o lascia placeholder se non usi converter)
- [ ] `DISCORD_BOT_TOKEN` configurato (o lascia placeholder se non usi Discord)
- [ ] `DISCORD_CHANNEL_ID` configurato (o lascia placeholder)
- [ ] File in `.gitignore` (verifica mai committato)

## 2. Verifica Graceful Degradation

Testa che l'app parta senza API keys:

### Test 1: Telegram Disabled
```json
// appsettings.Production.json
"Telegram": {
  "Enabled": false,
  "BotToken": "",
  "ChatId": 0
}
```
**Aspettativa**: TradingSupervisorService parte, log dice "Telegram disabled"

### Test 2: Discord Missing
```bash
# .dev.vars - commenta Discord
# DISCORD_BOT_TOKEN=...
```
**Aspettativa**: Cloudflare Worker parte, Discord features disabled

### Test 3: Anthropic Missing
```bash
# .dev.vars - commenta Anthropic
# ANTHROPIC_API_KEY=...
```
**Aspettativa**: Dashboard parte, EasyLanguage converter button disabled/greyed

## 3. Verifica Funzionamento (quando configurato)

### Telegram Test
```powershell
# Script già pronto
./scripts/test-telegram.ps1
```
**Aspettativa**: Ricevi messaggio su Telegram

### Discord Test
```bash
# Script già pronto
./scripts/test-discord.ps1
```

### Anthropic Test
```bash
# Dalla dashboard: prova a convertire un file .EasyLanguage
# Verifica che la conversione funzioni
```

## 4. Security Checklist

- [ ] `.gitignore` include `appsettings.Production.json`
- [ ] `.gitignore` include `.dev.vars`
- [ ] Nessun token visibile in `git status`
- [ ] Nessun token committato per errore (verifica con `git log -p | grep "sk-ant"`)

## 5. Production Deploy Checklist

### Cloudflare Worker Secrets
```bash
cd infra/cloudflare/worker

# Deploy secrets (NON usare .dev.vars in production!)
npx wrangler secret put ANTHROPIC_API_KEY
# Paste key when prompted

npx wrangler secret put DISCORD_BOT_TOKEN
# Paste token when prompted

npx wrangler secret put DISCORD_CHANNEL_ID
# Paste channel ID when prompted
```

### TradingSupervisorService
- [ ] File `appsettings.Production.json` copiato sul server Windows
- [ ] File permissions: Solo Administrator può leggere
- [ ] Service configurato per usare `appsettings.Production.json`

---

**Last Updated**: 2026-04-06
