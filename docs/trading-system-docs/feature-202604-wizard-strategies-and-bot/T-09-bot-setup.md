# T-BOT-01 — Bot Webhook Handler + Auth + i18n + Command Router

## Obiettivo
Implementare la base del sistema bot: webhook handler per Telegram (HMAC-SHA256)
e Discord (Ed25519), autenticazione whitelist single-user, i18n IT/EN, router
comandi, log in D1, e BotWebhookRegistrar C# per registrazione all'avvio.

## Dipendenze
- T-08 del progetto principale (Worker e D1 esistenti)
- T-10 del progetto principale (stesso bot token già configurato per alerting)

## Files da Creare
- `infra/cloudflare/worker/migrations/0003_bot_commands_log.sql`
- `infra/cloudflare/worker/src/bot/auth.ts`
- `infra/cloudflare/worker/src/bot/i18n.ts`
- `infra/cloudflare/worker/src/bot/dispatcher.ts`
- `infra/cloudflare/worker/src/routes/bot-telegram.ts`
- `infra/cloudflare/worker/src/routes/bot-discord.ts`
- `src/TradingSupervisorService/Bot/BotWebhookRegistrar.cs`

## Files da Modificare
- `infra/cloudflare/worker/src/index.ts` — aggiungere route webhook
- `infra/cloudflare/worker/wrangler.toml` — aggiungere vars BOT_*
- `src/TradingSupervisorService/Program.cs` — AddHostedService<BotWebhookRegistrar>
- `src/TradingSupervisorService/appsettings.json` — sezione Bots

## Implementazione

### 0003_bot_commands_log.sql
```sql
CREATE TABLE IF NOT EXISTS bot_command_log (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  executed_at TEXT NOT NULL DEFAULT (datetime('now')),
  bot_type TEXT NOT NULL,
  user_id TEXT NOT NULL,
  command TEXT NOT NULL,
  response_ok INTEGER NOT NULL DEFAULT 0,
  error TEXT
);
CREATE INDEX IF NOT EXISTS idx_bot_log_user ON bot_command_log(user_id, executed_at DESC);
```

### auth.ts
- `verifyTelegramSignature(body, botToken, headerToken)` → boolean (HMAC-SHA256)
- `verifyDiscordSignature(body, signature, timestamp, publicKey)` → boolean (Ed25519)
- `isWhitelisted(userId, whitelist)` → boolean (split ',' + trim + includes)

### i18n.ts
Oggetto `messages: Record<'it'|'en', Record<string, string>>` con tutte le
stringhe UI del bot. Funzione `t(key, lang)` → string. (Vedi 00-DESIGN.md sezione 2.1 per lista completa stringhe)

### dispatcher.ts
```typescript
type BotCommand =
  | { type: 'menu' }
  | { type: 'query'; query: 'portfolio'|'status'|'campaigns'|'market'|'strategies'|'alerts'|'risk'|'snapshot' }
  | { type: 'detail'; entity: 'campaign'; id: string }
  | { type: 'refresh'; last: string }
  | { type: 'unknown'; raw: string }

function parseCommand(text: string): BotCommand
function parseCallbackData(data: string): BotCommand
async function dispatchCommand(command, chatId, env, lang): Promise<void>
```

Mapping slash commands → BotCommand:
/start, /menu, /help → { type: 'menu' }
/portfolio → { type: 'query', query: 'portfolio' }
... ecc.

### bot-telegram.ts
1. Verifica HMAC firma `X-Telegram-Bot-Api-Secret-Token`
2. Estrai userId da `update.message.from.id` o `update.callback_query.from.id`
3. Check whitelist
4. Parse comando (message.text o callback_query.data)
5. Se callback_query → `answerCallbackQuery` (rimuove spinner)
6. `dispatchCommand(command, chatId, env, lang)`
7. Log in bot_command_log

### bot-discord.ts
1. Verifica Ed25519 firma (headers `X-Signature-Ed25519` + `X-Signature-Timestamp`)
2. Se interaction.type === 1 → risponde `{ type: 1 }` (PING obbligatorio Discord)
3. Estrai userId
4. Check whitelist
5. Per /snapshot → risposta type=5 (deferred) + `executionCtx.waitUntil(followup)`
6. Per altri comandi → risposta type=4 (immediate)

### BotWebhookRegistrar.cs
```csharp
// IHostedService.StartAsync:
// Se ActiveBot == "telegram":
//   GET https://api.telegram.org/bot{TOKEN}/setWebhook?url={WEBHOOK_URL}&secret_token={SECRET}
// Se ActiveBot == "discord": log info "Slash commands registrati manualmente"
// Se ActiveBot == "none": skip
```

## Test
- `TEST-BOT-01-01`: `verifyTelegramSignature` con token valido → true
- `TEST-BOT-01-02`: `verifyTelegramSignature` con token invalido → false
- `TEST-BOT-01-03`: `isWhitelisted('123456789', '123456789')` → true
- `TEST-BOT-01-04`: `isWhitelisted('999', '123456789')` → false
- `TEST-BOT-01-05`: `parseCommand('/portfolio')` → `{ type: 'query', query: 'portfolio' }`
- `TEST-BOT-01-06`: `parseCommand('/start')` → `{ type: 'menu' }`
- `TEST-BOT-01-07`: `parseCallbackData('detail:campaign:abc-123')` → type='detail', id='abc-123'
- `TEST-BOT-01-08`: Webhook POST firma invalida → 401
- `TEST-BOT-01-09`: Discord PING (type=1) → risposta `{ type: 1 }`
- `TEST-BOT-01-10`: Comando da user non whitelisted → risposta "⛔ Non autorizzato"
- `TEST-BOT-01-11`: bot_command_log ha record dopo ogni comando processato

## Done Criteria
- [ ] Migration 0003 applicata senza errori
- [ ] Tutti i test TEST-BOT-01-XX passano
- [ ] Webhook Telegram registrato all'avvio Supervisor (verifica log)
- [ ] Discord PING verification supera il check Discord (test manuale su Discord dev portal)
- [ ] i18n: tutte le chiavi disponibili in IT e EN

## Stima
~2 giorni
