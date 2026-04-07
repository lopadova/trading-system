# T-BOT-04 — Inline Keyboards + Discord Slash Commands

## Obiettivo
Implementare gli inline keyboard Telegram con callback_data strutturati
e registrare i slash commands Discord tramite script. Completare il
supporto Discord per interaction type 2 (APPLICATION_COMMAND) e
type 3 (MESSAGE_COMPONENT per i bottoni).

## Dipendenze
- T-BOT-02 (dispatcher — keyboards vengono passati alle send functions)

## Files da Creare
- `infra/cloudflare/worker/src/bot/keyboards/main-keyboard.ts`
- `infra/cloudflare/worker/src/bot/keyboards/detail-keyboard.ts`
- `scripts/register-discord-commands.ts`

## Files da Modificare
- `infra/cloudflare/worker/src/routes/bot-discord.ts` — gestire interaction type 3 (bottoni)

## Implementazione

### main-keyboard.ts — Telegram inline keyboard
```typescript
export function makeMainKeyboard(lang: Lang) {
  return { inline_keyboard: [
    [{ text: t('menu.portfolio',  lang), callback_data: 'query:portfolio'  },
     { text: t('menu.status',     lang), callback_data: 'query:status'     }],
    [{ text: t('menu.campaigns',  lang), callback_data: 'query:campaigns'  },
     { text: t('menu.market',     lang), callback_data: 'query:market'     }],
    [{ text: t('menu.strategies', lang), callback_data: 'query:strategies' },
     { text: t('menu.alerts',     lang), callback_data: 'query:alerts'     }],
    [{ text: t('menu.risk',       lang), callback_data: 'query:risk'       },
     { text: t('menu.snapshot',   lang), callback_data: 'query:snapshot'   }],
  ]}
}

export function makeRefreshKeyboard(lastCmd: string, lang: Lang) {
  return { inline_keyboard: [[
    { text: t('btn.refresh', lang), callback_data: `refresh:${lastCmd}` },
    { text: t('btn.menu',    lang), callback_data: 'menu:main'           },
  ]]}
}
```

### Discord Components (buttons)
```typescript
export function makeDiscordComponents(lastCmd: string, lang: Lang) {
  return [{ type: 1, components: [
    { type: 2, style: 2, label: t('btn.refresh', lang), custom_id: `refresh:${lastCmd}` },
    { type: 2, style: 1, label: t('btn.menu',    lang), custom_id: 'menu:main'           },
  ]}]
}
```

### register-discord-commands.ts — script una-tantum
```typescript
// eseguire: bun run scripts/register-discord-commands.ts
const commands = [
  { name: 'menu',       description: 'Mostra il menu principale' },
  { name: 'portfolio',  description: 'Snapshot portfolio e PnL' },
  { name: 'status',     description: 'Status servizi e macchina' },
  { name: 'campaigns',  description: 'Lista campagne attive' },
  { name: 'market',     description: 'Mercato e IVTS corrente' },
  { name: 'strategies', description: 'Strategie caricate' },
  { name: 'alerts',     description: 'Ultimi 10 alert inviati' },
  { name: 'risk',       description: 'Risk check campagne' },
  { name: 'snapshot',   description: 'Snapshot completo (2 messaggi)' },
]
// PUT https://discord.com/api/v10/applications/{APP_ID}/guilds/{GUILD_ID}/commands
```

### bot-discord.ts — gestione interaction type 3 (bottoni)
```typescript
if (interaction.type === 3) {
  // MESSAGE_COMPONENT (bottone premuto)
  const customId = interaction.data.custom_id
  const command = parseCallbackData(customId)
  // Risposta immediata (edit del messaggio originale, type=7)
  // o deferred (type=6) per operazioni lente
}
```

## Test
- `TEST-BOT-04-01`: `makeMainKeyboard('it')` → 4 righe, 2 bottoni per riga
- `TEST-BOT-04-02`: `makeMainKeyboard('en')` → label in inglese
- `TEST-BOT-04-03`: `makeRefreshKeyboard('portfolio')` → `callback_data='refresh:portfolio'`
- `TEST-BOT-04-04`: `makeDiscordComponents('status')` → type=1 ACTION_ROW con 2 buttons
- `TEST-BOT-04-05`: Discord interaction type=3 (bottone) → dispatchCommand chiamato
- `TEST-BOT-04-06`: register-discord-commands.ts eseguito → 200 da Discord API (test manuale)

## Done Criteria
- [ ] Build pulito
- [ ] Tutti i test TEST-BOT-04-XX passano
- [ ] Bottoni Telegram funzionanti: click → risposta aggiornata (test manuale)
- [ ] Slash commands Discord registrati e funzionanti (test manuale)
- [ ] Discord bottoni (components) funzionanti dopo risposta

## Stima
~1 giorno
