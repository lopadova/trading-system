# Bot Setup Guide - Telegram & Discord Integration

> **Guida completa e dettagliata** per configurare i bot Telegram e Discord nel Trading System.
> Spiega cosa sono le whitelist, come trovarle, configurarle e testarle.

---

## 📋 Indice

1. [Panoramica Bot System](#panoramica-bot-system)
2. [Le Tre Whitelist - Differenze Fondamentali](#le-tre-whitelist)
3. [Bot User Whitelist - Guida Completa](#bot-user-whitelist)
4. [Setup Telegram Bot](#setup-telegram-bot)
5. [Setup Discord Bot](#setup-discord-bot)
6. [Testing e Troubleshooting](#testing-e-troubleshooting)
7. [Gestione Multi-Utente](#gestione-multi-utente)
8. [Security Best Practices](#security-best-practices)

---

## 🎯 Panoramica Bot System

Il Trading System supporta **comandi interattivi** tramite bot Telegram e Discord. Gli utenti autorizzati possono:

- 📊 `/status` - Stato del sistema (servizi attivi, ultimo heartbeat)
- 💼 `/positions` - Posizioni attive correnti
- 📈 `/portfolio` - Summary completo del portfolio
- 💰 `/pnl` - Profit & Loss report
- 📉 `/greeks` - Greeks monitor (delta, gamma, theta, vega)

### Architettura

```
┌──────────────┐         ┌─────────────────┐         ┌────────────────┐
│   Telegram   │  HTTPS  │  Cloudflare     │  Query  │   D1 Database  │
│   Discord    │────────▶│  Worker         │────────▶│   - positions  │
│   (User)     │         │  - Bot Handler  │         │   - portfolio  │
└──────────────┘         │  - Auth Check   │         │   - whitelist  │
                         └─────────────────┘         └────────────────┘
                                  │
                                  ▼
                         ┌─────────────────┐
                         │  bot_whitelist  │ ← Controlla se user è autorizzato
                         │  table (D1)     │
                         └─────────────────┘
```

**Flusso di un comando**:
1. User invia `/positions` al bot
2. Telegram/Discord inoltra richiesta al Cloudflare Worker (webhook)
3. Worker verifica **firma HMAC** (Telegram) o **Ed25519** (Discord)
4. Worker controlla se **user_id** è nella tabella `bot_whitelist`
5. Se autorizzato → Esegue query D1 → Risponde all'utente
6. Se NON autorizzato → Ignora silenziosamente (nessuna risposta)

---

## 🔐 Le Tre Whitelist - Differenze Fondamentali

Il sistema usa **TRE whitelist diverse** con scopi completamente differenti. È cruciale capire la differenza per evitare confusione.

### Whitelist 1: API Key Whitelist (HTTP Authentication)

**Tabella D1**: `whitelist`  
**Migration**: `0005_api_whitelist.sql`  
**Scopo**: Autenticazione HTTP per Dashboard → Worker

```sql
CREATE TABLE whitelist (
  id INTEGER PRIMARY KEY,
  api_key TEXT NOT NULL UNIQUE,        -- SHA256 hash
  description TEXT,                     -- "Production Dashboard"
  created_at TEXT,
  active INTEGER DEFAULT 1
);
```

**Usata da**:
- Dashboard React (header `X-Api-Key`)
- Windows Services .NET (chiamate HTTP al Worker)
- Script/tool esterni che chiamano l'API Worker

**Quando serve**:
- ✅ **SEMPRE** per production (Dashboard deve autenticarsi)
- ✅ Per chiamate HTTP protette (`/api/positions`, `/api/portfolio`)

**Come si popola**:
```bash
# Genera token
openssl rand -hex 32

# Aggiungi a D1
bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO whitelist (api_key, description) 
VALUES ('20c98b3f05c7a06a2fcca3168aeeb7df...', 'Production Dashboard');
"
```

---

### Whitelist 2: Bot User Whitelist (Bot Commands Authorization)

**Tabella D1**: `bot_whitelist`  
**Migration**: `0004_bot_whitelist.sql`  
**Scopo**: Controllo accesso comandi bot (chi può usare `/status`, `/positions`, etc.)

```sql
CREATE TABLE bot_whitelist (
  id INTEGER PRIMARY KEY,
  user_id TEXT NOT NULL UNIQUE,        -- "123456789" (Telegram) o "987654321098765432" (Discord)
  bot_type TEXT CHECK(bot_type IN ('telegram', 'discord')),
  added_at TEXT,
  added_by TEXT,                       -- "admin", "lorenzo", etc.
  notes TEXT                           -- "Owner - Full access"
);
```

**Usata da**:
- Cloudflare Worker (bot handler)
- Ogni volta che un utente invia un comando bot

**Quando serve**:
- ✅ **SOLO se usi comandi bot interattivi** (`/status`, `/positions`, etc.)
- ❌ **NON serve** se usi solo alert one-way (servizio → utente, senza comandi)
- ❌ **NON serve** se `ActiveBot = "none"`

**Come si popola**:
```bash
# Manuale via D1
bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO bot_whitelist (user_id, bot_type, added_by, notes) 
VALUES ('123456789', 'telegram', 'admin', 'Lorenzo - Owner');
"

# Oppure via .NET service (appsettings.Local.json)
{
  "Bots": {
    "Whitelist": "123456789,987654321"  // ← Sync iniziale al Worker
  }
}
```

---

### Whitelist 3: Bots.Whitelist (.NET Service Config)

**File**: `src/TradingSupervisorService/appsettings.Local.json`  
**Formato**: Comma-separated string (`"123456789,987654321"`)  
**Scopo**: Configurazione iniziale per popolare `bot_whitelist` nel Worker

```json
{
  "Bots": {
    "ActiveBot": "telegram",
    "WebhookUrl": "https://trading-bot.padosoft.workers.dev/api/bot",
    "TelegramBotToken": "123456789:ABCdef...",
    "Whitelist": "123456789"  // ← User IDs autorizzati (comma-separated)
  }
}
```

**Usata da**:
- `BotWebhookRegistrar` (.NET service)
- Solo durante la **registrazione iniziale del webhook**

**Quando serve**:
- ✅ Setup iniziale (prima volta che avvii il bot)
- ✅ Se vuoi aggiungere utenti via config file invece di D1

**Come funziona**:
1. .NET service avvia → Legge `Bots.Whitelist`
2. Chiama Worker API: `POST /api/bot/register-webhook`
3. Worker riceve lista user IDs → Popola tabella `bot_whitelist`
4. Da quel momento, solo user nella tabella possono usare comandi

**⚠️ Nota importante**: Dopo il primo setup, puoi gestire la whitelist direttamente nel database D1. Non serve riavviare il servizio .NET per aggiungere/rimuovere utenti.

---

### Riepilogo Differenze

| Caratteristica | `whitelist` (API keys) | `bot_whitelist` (User IDs) | `Bots.Whitelist` (Config) |
|----------------|------------------------|----------------------------|---------------------------|
| **Cosa contiene** | API keys (SHA256) | User IDs (Telegram/Discord) | User IDs (comma-separated) |
| **Dove vive** | D1 database | D1 database | appsettings.json |
| **Controlla** | Autenticazione HTTP | Accesso comandi bot | Sync iniziale |
| **Quando serve** | Sempre (Dashboard) | Solo se usi bot comandi | Setup iniziale |
| **Formato** | `20c98b3f05c7a...` | `123456789` | `"123456789,987..."` |
| **Gestione** | D1 execute | D1 execute o config | Config file |

---

## 🤖 Bot User Whitelist - Guida Completa

Questa sezione spiega **in dettaglio** come configurare la Bot User Whitelist (`bot_whitelist` table).

### Quando Va Configurata

**✅ CONFIGURALA SE**:
- Vuoi abilitare comandi bot interattivi (`/status`, `/positions`, etc.)
- Hai creato un bot Telegram o Discord
- Vuoi che utenti specifici possano interrogare il sistema via bot

**❌ NON SERVE SE**:
- Usi solo Telegram per **alert one-way** (servizio invia alert, utente non risponde)
- Non hai bot attivi (`ActiveBot = "none"`)
- Usi solo la Dashboard web (no bot)

### Sicurezza By Default

**⚠️ IMPORTANTE**: Se la whitelist è **vuota**, **NESSUN UTENTE** può usare i comandi bot.

Questo è un design intenzionale per sicurezza:
- Bot pubblico → Solo admin autorizzati possono comandare
- No whitelist → No accesso → Massima sicurezza
- Devi esplicitamente aggiungere user IDs per abilitare accesso

### Come Trovare il Tuo User ID

#### Telegram User ID

**Metodo 1: @userinfobot (Più semplice)**

1. Apri Telegram (app mobile, desktop o web)
2. Cerca: `@userinfobot`
3. Avvia la chat: Click su "START" o invia `/start`
4. Il bot risponde immediatamente con:
   ```
   Id: 123456789
   First: Lorenzo
   Last: Padovani
   Username: @lorenzo_padovani
   Language: it
   ```
5. Copia il numero accanto a "Id:" → `123456789`

**Metodo 2: Get Updates API (Avanzato)**

Se preferisci via API:

```bash
# Sostituisci YOUR_BOT_TOKEN con il tuo token
curl "https://api.telegram.org/botYOUR_BOT_TOKEN/getUpdates"

# Invia un messaggio al tuo bot, poi esegui il comando
# Nella risposta JSON cerca:
{
  "message": {
    "from": {
      "id": 123456789,  // ← Questo è il tuo user ID
      "first_name": "Lorenzo"
    }
  }
}
```

**Metodo 3: Forward to @RawDataBot**

1. Apri Telegram → Cerca `@RawDataBot`
2. Inoltra un tuo messaggio al bot
3. Il bot risponde con JSON completo:
   ```json
   {
     "from": {
       "id": 123456789,  // ← User ID
       "is_bot": false
     }
   }
   ```

#### Discord User ID

**Step 1: Abilita Developer Mode**

1. Apri Discord (app desktop o web)
2. Click sull'**icona ingranaggio** in basso a sinistra (accanto al tuo username)
3. Si aprono le **User Settings**
4. Scroll nel menu laterale sinistro → Cerca **"Advanced"** (o **"Avanzate"** in italiano)
5. Trova **"Developer Mode"** → Toggle su **ON** ✅
6. Chiudi le settings

**Step 2: Copia il tuo User ID**

1. Vai in una chat qualsiasi (server o DM)
2. **Right-click sul tuo username** (in chat o nella lista utenti)
3. Nel menu contestuale vedrai: **"Copy User ID"** (o **"Copia ID utente"**)
4. Click → L'ID viene copiato (formato: 18 cifre, es: `987654321098765432`)

**⚠️ Nota**: Se non vedi "Copy User ID", il Developer Mode non è abilitato correttamente. Torna allo Step 1.

**Differenze Telegram vs Discord**:
- **Telegram ID**: 9 cifre (es: `123456789`)
- **Discord ID**: 18 cifre (es: `987654321098765432`)

---

### Come Configurare la Whitelist

Ci sono **DUE modalità** per popolare la Bot User Whitelist:

#### Modalità 1: Via .NET Service Config (Setup Iniziale)

**Quando usarla**: Prima volta che configuri il bot, setup iniziale.

**File**: `src/TradingSupervisorService/appsettings.Local.json`

```json
{
  "Bots": {
    "ActiveBot": "telegram",  // o "discord" o "none"
    
    // Webhook URL del Worker Cloudflare
    "WebhookUrl": "https://trading-bot.padosoft.workers.dev/api/bot",
    
    // Token del bot Telegram (se usi Telegram)
    "TelegramBotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz",
    
    // Token del bot Discord (se usi Discord)
    "DiscordBotToken": "YOUR_DISCORD_BOT_TOKEN_HERE",
    "DiscordPublicKey": "YOUR_DISCORD_PUBLIC_KEY_HERE",
    
    // ⭐ WHITELIST: Comma-separated user IDs
    // Esempi:
    // - Singolo utente: "123456789"
    // - Multi utente: "123456789,987654321,555666777"
    // - Vuota: "" (nessun accesso)
    "Whitelist": "123456789"  // ← Il TUO user ID qui
  }
}
```

**Come funziona**:
1. Avvia il servizio .NET: `dotnet run --project src/TradingSupervisorService`
2. `BotWebhookRegistrar` legge la config
3. Chiama Worker API: `POST /api/bot/register-webhook`
4. Worker popola `bot_whitelist` table con gli user IDs
5. Da ora, solo quegli utenti possono usare comandi bot

**Vantaggi**:
- ✅ Setup automatico al primo avvio
- ✅ Configurazione in versioning (se non usi .Local.json)
- ✅ Facile per setup iniziale

**Svantaggi**:
- ❌ Richiede riavvio servizio per aggiungere utenti
- ❌ Non ideale per gestione dinamica

---

#### Modalità 2: Via D1 Database (Gestione Dinamica)

**Quando usarla**: Dopo il setup iniziale, per aggiungere/rimuovere utenti al volo.

**Aggiungi utente Telegram**:

```bash
cd infra/cloudflare/worker

bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO bot_whitelist (user_id, bot_type, added_by, notes) 
VALUES ('123456789', 'telegram', 'admin', 'Lorenzo - Owner e amministratore sistema');
"
```

**Aggiungi utente Discord**:

```bash
bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO bot_whitelist (user_id, bot_type, added_by, notes) 
VALUES ('987654321098765432', 'discord', 'admin', 'Lorenzo - Owner');
"
```

**Aggiungi multipli utenti**:

```bash
bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO bot_whitelist (user_id, bot_type, added_by, notes) VALUES
  ('123456789', 'telegram', 'admin', 'Lorenzo - Owner'),
  ('111222333', 'telegram', 'lorenzo', 'Marco - Collaboratore'),
  ('444555666', 'telegram', 'lorenzo', 'Luca - Tester');
"
```

**Verifica whitelist**:

```bash
bunx wrangler d1 execute trading-db --remote --command="
SELECT 
  user_id, 
  bot_type, 
  added_by, 
  notes, 
  datetime(added_at) as added_at 
FROM bot_whitelist 
ORDER BY added_at DESC;
"
```

Output esempio:
```
┌──────────────────────┬──────────┬──────────┬─────────────────────┬─────────────────────┐
│ user_id              │ bot_type │ added_by │ notes               │ added_at            │
├──────────────────────┼──────────┼──────────┼─────────────────────┼─────────────────────┤
│ 123456789            │ telegram │ admin    │ Lorenzo - Owner     │ 2026-04-18 22:15:30 │
├──────────────────────┼──────────┼──────────┼─────────────────────┼─────────────────────┤
│ 987654321098765432   │ discord  │ admin    │ Lorenzo - Owner     │ 2026-04-18 22:16:45 │
└──────────────────────┴──────────┴──────────┴─────────────────────┴─────────────────────┘
```

**Rimuovi utente**:

```bash
bunx wrangler d1 execute trading-db --remote --command="
DELETE FROM bot_whitelist 
WHERE user_id = '111222333' AND bot_type = 'telegram';
"
```

**Vantaggi**:
- ✅ Immediato (no riavvio servizio)
- ✅ Gestione granulare
- ✅ Storico con timestamp e note
- ✅ Ideale per production

**Svantaggi**:
- ❌ Richiede accesso a wrangler CLI
- ❌ Non in versioning (solo database)

---

### Quale Modalità Usare?

**Raccomandazione**:

```
Setup iniziale → Modalità 1 (Config file)
  ↓
Dopo deploy → Modalità 2 (Database D1)
```

**Workflow consigliato**:
1. **Prima volta**: Configura `Bots.Whitelist` in `appsettings.Local.json` con il tuo user ID
2. **Avvia servizio**: Il servizio popola automaticamente `bot_whitelist`
3. **Dopo deploy**: Usa D1 execute per aggiungere/rimuovere utenti dinamicamente
4. **Non toccare più** la config file (a meno di reset completo)

---

## 📱 Setup Telegram Bot

### Step 1: Crea il Bot

1. Apri Telegram → Cerca `@BotFather`
2. Invia: `/newbot`
3. Segui il wizard:
   ```
   BotFather: Alright, a new bot. How are we going to call it?
   Tu: Trading System Bot
   
   BotFather: Good. Now let's choose a username for your bot.
   Tu: trading_system_padosoft_bot
   
   BotFather: Done! Keep your token secure:
   123456789:ABCdefGHIjklMNOpqrsTUVwxyz1234567890
   ```
4. **Copia il token** (lo userai nella config)

### Step 2: Configura il Bot

**File**: `src/TradingSupervisorService/appsettings.Local.json`

```json
{
  "Bots": {
    "ActiveBot": "telegram",
    "WebhookUrl": "https://trading-bot.padosoft.workers.dev/api/bot",
    "TelegramBotToken": "123456789:ABCdefGHIjklMNOpqrsTUVwxyz1234567890",
    "Whitelist": "123456789"  // ← Il TUO user ID da @userinfobot
  }
}
```

### Step 3: Ottieni il Tuo User ID

1. Cerca `@userinfobot` su Telegram
2. Invia `/start`
3. Copia il numero (es: `123456789`)
4. Aggiorna `Bots.Whitelist` con questo numero

### Step 4: Avvia il Servizio .NET

```bash
cd src/TradingSupervisorService
dotnet run
```

**Log attesi**:
```
[INFO] BotWebhookRegistrar: Registering webhook for telegram bot
[INFO] BotWebhookRegistrar: Webhook URL: https://trading-bot.padosoft.workers.dev/api/bot
[INFO] BotWebhookRegistrar: Syncing 1 whitelisted users to Worker
[INFO] BotWebhookRegistrar: Webhook registered successfully
```

### Step 5: Testa il Bot

1. Apri Telegram → Cerca il tuo bot (`@trading_system_padosoft_bot`)
2. Click "START" (o invia `/start`)
3. Se nella whitelist:
   ```
   Bot: 👋 Benvenuto! Trading System Bot attivo.
   
   Comandi disponibili:
   /status - Stato sistema
   /positions - Posizioni attive
   /portfolio - Portfolio summary
   /pnl - Profit & Loss
   /greeks - Greeks monitor
   ```
4. Prova un comando: `/status`
5. Se funziona → ✅ Setup completato!

---

## 💬 Setup Discord Bot

### Step 1: Crea l'Applicazione

1. Vai su: https://discord.com/developers/applications
2. Click **"New Application"**
3. Nome: `Trading System Bot`
4. Click **"Create"**

### Step 2: Crea il Bot

1. Nel menu laterale → Click **"Bot"**
2. Click **"Add Bot"** → Conferma
3. **Bot Token**:
   - Click **"Reset Token"** → Conferma
   - Copia il token (es: `MTIzNDU2Nzg5.XXXXXX.YYYYYYYYYYYYYYYYYYYYYYYYYY`)
   - **⚠️ Salvalo subito**: Non lo vedrai più!
4. **Privileged Gateway Intents** (scroll down):
   - ✅ Enable: **MESSAGE CONTENT INTENT**
   - ✅ Enable: **SERVER MEMBERS INTENT**
   - Click **"Save Changes"**

### Step 3: Ottieni Public Key

1. Nel menu laterale → Click **"General Information"**
2. Trova **"PUBLIC KEY"** (es: `a1b2c3d4e5f6789abcdef0123456789...`)
3. Click **"Copy"**

### Step 4: Invita il Bot nel Server

1. Nel menu laterale → Click **"OAuth2"** → **"URL Generator"**
2. **Scopes** (seleziona):
   - ✅ `bot`
   - ✅ `applications.commands` (per slash commands)
3. **Bot Permissions** (seleziona):
   - ✅ Read Messages/View Channels
   - ✅ Send Messages
   - ✅ Embed Links
   - ✅ Read Message History
4. Copia l'**URL generato** in basso (es: `https://discord.com/oauth2/authorize?client_id=...`)
5. Apri l'URL nel browser → Seleziona il tuo server → **"Authorize"**
6. Il bot appare nel server (offline)

### Step 5: Ottieni Channel ID

1. Apri Discord → Abilita **Developer Mode**:
   - Settings → Advanced → Developer Mode **ON**
2. Right-click sul **canale** dove vuoi ricevere alert
3. Click **"Copy Channel ID"** (es: `987654321098765432`)

### Step 6: Ottieni il Tuo User ID

1. Right-click sul tuo **username** (in chat o lista utenti)
2. Click **"Copy User ID"** (es: `111222333444555666`)

### Step 7: Configura il Bot

**File**: `src/TradingSupervisorService/appsettings.Local.json`

```json
{
  "Bots": {
    "ActiveBot": "discord",
    "WebhookUrl": "https://trading-bot.padosoft.workers.dev/api/bot",
    "DiscordBotToken": "MTIzNDU2Nzg5.XXXXXX.YYYYYYYYYYYYYYYYYYYYYYYYYY",
    "DiscordPublicKey": "a1b2c3d4e5f6789abcdef0123456789...",
    "Whitelist": "111222333444555666"  // ← Il TUO user ID
  }
}
```

**File**: `infra/cloudflare/worker/.dev.vars`

```bash
DISCORD_BOT_TOKEN=MTIzNDU2Nzg5.XXXXXX.YYYYYYYYYYYYYYYYYYYYYYYYYY
DISCORD_PUBLIC_KEY=a1b2c3d4e5f6789abcdef0123456789...
DISCORD_CHANNEL_ID=987654321098765432
```

### Step 8: Avvia il Servizio

```bash
cd src/TradingSupervisorService
dotnet run
```

### Step 9: Registra Slash Commands

Discord richiede **registrazione esplicita** dei slash commands:

```bash
cd infra/cloudflare/worker

# Script di registrazione
node scripts/register-discord-commands.js
```

Output atteso:
```
✅ Registered: /status
✅ Registered: /positions
✅ Registered: /portfolio
✅ Registered: /pnl
✅ Registered: /greeks
```

### Step 10: Testa il Bot

1. Apri Discord → Vai nel canale configurato
2. Digita `/` → Vedi i comandi del bot
3. Click su `/status` → Invia
4. Se funziona → ✅ Setup completato!

---

## 🧪 Testing e Troubleshooting

### Test Checklist

**✅ Verifica 1: Token Configurati**

```bash
# .NET Service
grep -E "TelegramBotToken|DiscordBotToken" src/TradingSupervisorService/appsettings.Local.json

# Worker
grep -E "DISCORD_BOT_TOKEN|TELEGRAM_BOT_TOKEN" infra/cloudflare/worker/.dev.vars
```

**✅ Verifica 2: User ID nella Whitelist**

```bash
cd infra/cloudflare/worker
bunx wrangler d1 execute trading-db --remote --command="
SELECT user_id, bot_type, notes FROM bot_whitelist;
"
```

Devi vedere il TUO user ID nella lista.

**✅ Verifica 3: Webhook Registrato**

```bash
# Telegram
curl "https://api.telegram.org/bot123456789:ABCdef.../getWebhookInfo"

# Output atteso:
{
  "url": "https://trading-bot.padosoft.workers.dev/api/bot",
  "has_custom_certificate": false,
  "pending_update_count": 0
}
```

**✅ Verifica 4: Worker Risponde**

```bash
curl https://trading-bot.padosoft.workers.dev/health

# Output atteso:
{"status":"ok","timestamp":"2026-04-18T22:30:45Z"}
```

**✅ Verifica 5: Invia Comando Test**

1. Apri Telegram/Discord
2. Invia `/status`
3. **Aspettati**:
   - ✅ Risposta immediata (< 2 secondi)
   - ✅ Emoji e formattazione corretta
4. **Se no risposta** → Vai a troubleshooting

---

### Troubleshooting Comune

#### Problema 1: Bot Non Risponde

**Sintomo**: Invii `/status` ma nessuna risposta.

**Diagnosi**:
```bash
# 1. Verifica user ID è nella whitelist
bunx wrangler d1 execute trading-db --remote --command="
SELECT * FROM bot_whitelist WHERE user_id = '123456789';
"

# Se vuoto → NON sei nella whitelist
```

**Fix**:
```bash
# Aggiungi il tuo user ID
bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO bot_whitelist (user_id, bot_type, added_by, notes) 
VALUES ('123456789', 'telegram', 'admin', 'Owner');
"

# Riprova il comando
```

---

#### Problema 2: "Unauthorized" Error

**Sintomo**: Bot risponde con errore "Unauthorized" o 401.

**Causa**: Token bot non configurato o errato nel Worker.

**Diagnosi**:
```bash
# Verifica token in Worker secrets
cd infra/cloudflare/worker
bunx wrangler secret list

# Deve contenere:
# - TELEGRAM_BOT_TOKEN (se usi Telegram)
# - DISCORD_BOT_TOKEN (se usi Discord)
```

**Fix**:
```bash
# Telegram
bunx wrangler secret put TELEGRAM_BOT_TOKEN
# Paste: 123456789:ABCdef...

# Discord
bunx wrangler secret put DISCORD_BOT_TOKEN
# Paste: MTIzNDU2Nzg5.XXXXXX...
```

---

#### Problema 3: Webhook Non Registrato

**Sintomo**: `getWebhookInfo` restituisce `url: ""`.

**Causa**: .NET service non ha chiamato il Worker per registrare webhook.

**Fix**:
```bash
# 1. Verifica config
cat src/TradingSupervisorService/appsettings.Local.json | grep -A 5 "Bots"

# 2. Riavvia servizio
cd src/TradingSupervisorService
dotnet run

# 3. Verifica log
# Cerca: "BotWebhookRegistrar: Webhook registered successfully"

# 4. Se fallisce, registra manualmente
curl -X POST "https://api.telegram.org/bot123456789:ABCdef.../setWebhook" \
  -d "url=https://trading-bot.padosoft.workers.dev/api/bot"
```

---

#### Problema 4: Discord Slash Commands Non Appaiono

**Sintomo**: Digiti `/` ma non vedi i comandi del bot.

**Causa**: Slash commands non registrati.

**Fix**:
```bash
cd infra/cloudflare/worker

# Crea script se non esiste
cat > scripts/register-discord-commands.js << 'EOF'
// Script per registrare Discord slash commands
const fetch = require('node-fetch');

const APPLICATION_ID = 'YOUR_APPLICATION_ID';  // Da Discord Developer Portal
const BOT_TOKEN = 'YOUR_BOT_TOKEN';

const commands = [
  { name: 'status', description: 'System status' },
  { name: 'positions', description: 'Active positions' },
  { name: 'portfolio', description: 'Portfolio summary' },
  { name: 'pnl', description: 'Profit & Loss' },
  { name: 'greeks', description: 'Greeks monitor' }
];

async function registerCommands() {
  const url = `https://discord.com/api/v10/applications/${APPLICATION_ID}/commands`;
  
  for (const command of commands) {
    const response = await fetch(url, {
      method: 'POST',
      headers: {
        'Authorization': `Bot ${BOT_TOKEN}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(command)
    });
    
    if (response.ok) {
      console.log(`✅ Registered: /${command.name}`);
    } else {
      console.error(`❌ Failed: /${command.name}`, await response.text());
    }
  }
}

registerCommands();
EOF

# Esegui script
node scripts/register-discord-commands.js
```

---

#### Problema 5: Rate Limit Error

**Sintomo**: Bot risponde "Rate limit exceeded".

**Causa**: Troppi comandi in poco tempo.

**Limiti**:
- **Telegram**: 30 richieste/secondo per bot
- **Discord**: 5 richieste/secondo per bot

**Fix**:
- Aspetta 1 minuto
- Riprova il comando
- Non spammare comandi

---

## 👥 Gestione Multi-Utente

### Scenario: Team di Trading

Hai un team di 3 persone:
- **Lorenzo** (Owner): Accesso completo
- **Marco** (Trader): Solo `/positions`, `/pnl`
- **Luca** (Analista): Solo `/status`, `/greeks`

**Setup**:

```bash
# Aggiungi tutti alla whitelist
bunx wrangler d1 execute trading-db --remote --command="
INSERT INTO bot_whitelist (user_id, bot_type, added_by, notes) VALUES
  ('123456789', 'telegram', 'admin', 'Lorenzo - Owner - Full access'),
  ('111222333', 'telegram', 'lorenzo', 'Marco - Trader - Trading ops'),
  ('444555666', 'telegram', 'lorenzo', 'Luca - Analista - Monitoring');
"
```

**Nota**: Attualmente il sistema **NON supporta permessi granulari**. Tutti gli utenti nella whitelist hanno accesso a tutti i comandi.

**Roadmap**: Permessi per comando (es: Marco può `/positions` ma non `/status`) è pianificato per versioni future.

---

### Revoca Accesso

**Scenario**: Luca lascia il team, devi revocare accesso.

```bash
# 1. Verifica user ID
bunx wrangler d1 execute trading-db --remote --command="
SELECT user_id, notes FROM bot_whitelist WHERE notes LIKE '%Luca%';
"

# 2. Rimuovi dalla whitelist
bunx wrangler d1 execute trading-db --remote --command="
DELETE FROM bot_whitelist WHERE user_id = '444555666';
"

# 3. Verifica rimosso
bunx wrangler d1 execute trading-db --remote --command="
SELECT COUNT(*) as count FROM bot_whitelist WHERE user_id = '444555666';
"
# count deve essere 0
```

**Effetto immediato**: Luca non può più usare comandi bot (no riavvio servizio).

---

### Audit Log

Tieni traccia di chi è stato aggiunto/rimosso:

```bash
# Query audit
bunx wrangler d1 execute trading-db --remote --command="
SELECT 
  user_id,
  bot_type,
  added_by,
  notes,
  datetime(added_at) as added_at
FROM bot_whitelist
ORDER BY added_at DESC;
"
```

**Raccomandazione**: Usa il campo `notes` per documentare:
- Nome completo
- Ruolo
- Motivo accesso
- Data scadenza (se temporaneo)

Esempio:
```sql
INSERT INTO bot_whitelist (user_id, bot_type, added_by, notes) 
VALUES (
  '999888777', 
  'telegram', 
  'lorenzo', 
  'Mario Rossi - Contractor - Access until 2026-12-31'
);
```

---

## 🔒 Security Best Practices

### 1. MAI Committare Token in Git

**File da NON committare**:
- `appsettings.Local.json` → Contiene `TelegramBotToken`, `Whitelist`
- `appsettings.Production.json` → Contiene token production
- `.dev.vars` → Contiene `DISCORD_BOT_TOKEN`
- Qualsiasi file con `.local.*` o `.secret.*`

**Verifica `.gitignore`**:
```bash
grep -E "Local\.json|\.dev\.vars" .gitignore

# Deve contenere:
# **/appsettings.Local.json
# **/*.local.json
# .dev.vars
```

---

### 2. Usa Cloudflare Secrets per Production

**Development**:
```bash
# .dev.vars (per wrangler dev)
TELEGRAM_BOT_TOKEN=123456789:ABCdef...
```

**Production**:
```bash
# Secrets (encrypted by Cloudflare)
bunx wrangler secret put TELEGRAM_BOT_TOKEN
# Paste token quando richiesto
```

**NEVER** mettere token production in `.dev.vars`!

---

### 3. Hide Production URLs (⭐ RECOMMENDED)

**Problem**: `wrangler.toml` contains `DASHBOARD_ORIGIN` and is committed to git.

If your repository is **public**, your production dashboard URL is **visible to everyone**.

**Solution**: Use Cloudflare Secrets for production URL.

**Step 1: Remove from wrangler.toml**
```toml
# wrangler.toml (before)
[vars]
DASHBOARD_ORIGIN = "https://trading.padosoft.com"  # ❌ Public in git!

# wrangler.toml (after)
[vars]
DASHBOARD_ORIGIN = "http://localhost:5173"  # ✅ Only dev default
# Production URL moved to secrets
```

**Step 2: Set as secret**
```bash
cd infra/cloudflare/worker

bunx wrangler secret put DASHBOARD_ORIGIN
# When prompted, paste: https://trading.padosoft.com
```

**Why this matters**:
- ❌ Public URL in git → Attackers know your production endpoint
- ✅ Secret URL → Hidden from public view, encrypted by Cloudflare
- ✅ Secrets override `vars` → No code changes needed

**Alternative (if URL can be public)**:
```toml
[env.production]
vars = { DASHBOARD_ORIGIN = "https://trading.padosoft.com" }
```

Use this if:
- Dashboard is already public (custom domain like `trading.yourdomain.com`)
- Repository is private
- URL doesn't contain sensitive info

See [DEPLOYMENT_GUIDE.md § 4.3](./DEPLOYMENT_GUIDE.md#43-configure-production-url--recommended) for details.

---

### 4. Rotate Token Periodicamente

**Ogni 90 giorni**:

**Telegram**:
1. `@BotFather` → `/mybots`
2. Seleziona bot → "API Token" → "Revoke current token"
3. Copia nuovo token
4. Update: `appsettings.Local.json` + Cloudflare Secrets
5. Riavvia servizi

**Discord**:
1. Discord Developer Portal → Bot → "Reset Token"
2. Copia nuovo token
3. Update: `.dev.vars` + Cloudflare Secrets
4. Riavvia servizi

---

### 4. Monitora Accessi

**Log Worker** (Cloudflare Dashboard):
1. Cloudflare → Workers & Pages → `trading-bot` → Logs
2. Filtra: `bot_whitelist` o `unauthorized`
3. Cerca pattern sospetti:
   - User ID sconosciuti
   - Tentativi ripetuti da stesso ID
   - Comandi a orari strani (3 AM)

**Setup Alert**:
```bash
# Crea alert per accessi non autorizzati
# Cloudflare Dashboard → Workers → trading-bot → Triggers → Add Alert

Condition: Response status = 401 OR 403
Threshold: > 10 in 5 minutes
Action: Email to lorenzo.padovani@padosoft.com
```

---

### 5. Limita User nella Whitelist

**Principio**: Aggiungi SOLO utenti che SERVONO.

**Raccomandazione**:
- Owner: 1 user ID (te)
- Collaboratori: Solo se necessitano accesso real-time
- Tester: Rimuovi dopo testing

**Anti-pattern**:
```json
// ❌ SBAGLIATO: Whitelist troppo ampia
"Whitelist": "123,456,789,111,222,333,444,555"  // 8 utenti?!
```

**Best practice**:
```json
// ✅ CORRETTO: Solo utenti necessari
"Whitelist": "123456789"  // Solo owner
```

---

### 6. Verifica Signature Webhook

Il Worker **SEMPRE** verifica:
- **Telegram**: HMAC-SHA256 con bot token
- **Discord**: Ed25519 con public key

**NO custom bypass**: Se webhook signature non valida → 401 Unauthorized.

---

## 📚 Riferimenti

### Documentazione Ufficiale

- **Telegram Bot API**: https://core.telegram.org/bots/api
- **Discord Developer Portal**: https://discord.com/developers/docs
- **Cloudflare Workers**: https://developers.cloudflare.com/workers
- **Cloudflare D1**: https://developers.cloudflare.com/d1

### File di Configurazione

| File | Scopo |
|------|-------|
| `src/TradingSupervisorService/appsettings.Local.json.example` | Template config .NET |
| `infra/cloudflare/worker/.dev.vars.example` | Template secrets Worker |
| `infra/cloudflare/worker/migrations/0004_bot_whitelist.sql` | Schema bot_whitelist |
| `infra/cloudflare/worker/src/bot/auth.ts` | Logica autenticazione bot |

### Script Utili

```bash
# Verifica stato bot
./scripts/check-bot-status.sh

# Aggiungi user alla whitelist
./scripts/add-bot-user.sh <user_id> <bot_type> <notes>

# Rimuovi user dalla whitelist
./scripts/remove-bot-user.sh <user_id>

# Test comandi bot
./scripts/test-bot-commands.sh
```

---

## ❓ FAQ

**Q: Posso usare Telegram E Discord contemporaneamente?**  
A: Sì, ma devi configurare entrambi in `appsettings.Local.json`. Il campo `ActiveBot` determina quale webhook registrare, ma puoi avere whitelist per entrambi in D1.

**Q: La whitelist è per utente o per gruppo?**  
A: Per **utente** (user ID). Se vuoi che un gruppo Telegram riceva comandi, aggiungi gli user IDs dei membri alla whitelist.

**Q: Posso limitare un utente a specifici comandi?**  
A: Non ancora. Attualmente, utenti nella whitelist hanno accesso a TUTTI i comandi. Permessi granulari sono pianificati per v2.0.

**Q: Cosa succede se la whitelist è vuota?**  
A: NESSUN utente può usare comandi bot. È sicurezza by design.

**Q: Devo riavviare il servizio per aggiungere utenti?**  
A: No, se aggiungi via D1. Se usi `Bots.Whitelist` config, sì (riavvio richiesto).

**Q: Come faccio il backup della whitelist?**  
A: 
```bash
bunx wrangler d1 execute trading-db --remote --command="
SELECT * FROM bot_whitelist;
" > bot_whitelist_backup_$(date +%Y%m%d).json
```

---

**Guida aggiornata il**: 2026-04-18  
**Versione**: 1.0  
**Autore**: Trading System AI Assistant
