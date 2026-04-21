# T-BOT-03 — Image Generator PNG (Gauge Termometro)

## Obiettivo
Generare immagini PNG dinamiche nel Worker tramite SVG → resvg-wasm
e inviarle come foto Telegram con caption. Il gauge visualizza PnL MTD
come arco circolare e metriche risk come barre orizzontali colorate.

## Dipendenze
- T-BOT-02 (query e semafori per i dati del gauge)

## Files da Creare
- `infra/cloudflare/worker/src/bot/image-generator.ts`
- `infra/cloudflare/worker/src/bot/images/portfolio-gauge.ts`

## Files da Modificare
- `infra/cloudflare/worker/package.json` — aggiungere `@resvg/resvg-wasm`
- `infra/cloudflare/worker/src/bot/dispatcher.ts` — usare sendPhoto per /portfolio

## Implementazione

### image-generator.ts
```typescript
import initResvg, { Resvg } from '@resvg/resvg-wasm'
import resvgWasm from '@resvg/resvg-wasm/index_bg.wasm'

let initialized = false
async function ensureResvg() {
  if (!initialized) { await initResvg(resvgWasm); initialized = true }
}

export async function svgToPng(svg: string): Promise<Uint8Array>
```
Importante: `initialized` è singleton nel Worker — warm requests non reinizializzano.

### portfolio-gauge.ts — SVG generato come stringa

Canvas: 480×280px, sfondo dark `#0a0a0f`

Elementi:
1. **Gauge arco PnL MTD**: arco 240° centrato in alto a sinistra (cx=130, cy=150, r=80)
   - Arco grigio (range completo -$5k…+$5k)
   - Arco colorato (verde se PnL>0, rosso se PnL<0) — angolo proporzionale a pnl/maxPnl
   - Label centrale: valore PnL formattato + "PnL MTD"
2. **IVTS indicator**: cerchio colorato (verde/giallo/rosso) + label valore
3. **Barre risk orizzontali** (destra): per ogni campagna attiva (max 4)
   - Label campagna + barra proporzionale colorata (verde/giallo/rosso)
4. **Timestamp** UTC in basso a destra (JetBrains Mono 9px)
5. **Branding** "Trading System" in basso a sinistra

```typescript
export function generatePortfolioGaugeSvg(data: {
  pnlMtd: number | null
  activeCampaigns: number
  ivts: number | null
  ivtsState: string
  risks: Array<{ label: string; valuePct: number; signal: string }>
}): string
```

### Invio come foto Telegram
```typescript
export async function telegramSendPhoto(chatId, pngBytes, caption, keyboard, token)
// FormData con: chat_id, caption, parse_mode='Markdown', reply_markup, photo (Blob PNG)
// POST https://api.telegram.org/bot{TOKEN}/sendPhoto
```

Fallback: se `svgToPng` lancia → catch → invia solo testo con `telegramSend` (no crash).

### Integrazione in dispatchCommand
```typescript
case 'portfolio': {
  const [portfolio, riskData] = await Promise.all([queryPortfolio(db), queryRisk(db)])
  const risks = riskData.slice(0, 4).map(r => ({
    label: r.strategy_id.slice(0, 12),
    valuePct: Math.abs((r.pnl_usd ?? 0) / (r.stop_usd ?? 5000)),
    signal: pnlVsStopSignal(r.pnl_usd, r.stop_usd ?? 5000)
  }))
  const svg = generatePortfolioGaugeSvg({ ... })
  const png = await svgToPng(svg)
  const caption = formatPortfolio(portfolio, lang)
  await telegramSendPhoto(chatId, png, caption, makeRefreshKeyboard('portfolio', lang), token)
}
```

## Test
- `TEST-BOT-03-01`: `generatePortfolioGaugeSvg(...)` → stringa che inizia con `<svg`
- `TEST-BOT-03-02`: SVG generato è valido XML (parseabile con DOMParser)
- `TEST-BOT-03-03`: `svgToPng(validSvg)` → Uint8Array che inizia con `[137,80,78,71]` (PNG header)
- `TEST-BOT-03-04`: `pnlMtd=0` → arc angle = 0 (cerchio al centro, nessun arco colorato)
- `TEST-BOT-03-05`: `pnlMtd=5000` (max) → arco colorato verde per 120° (metà positivo)
- `TEST-BOT-03-06`: svgToPng fallisce → dispatcher invia testo senza crash
- `TEST-BOT-03-07`: `telegramSendPhoto` chiamata con `FormData` contenente Blob PNG

## Done Criteria
- [ ] Build pulito (Worker TypeScript + wasm import)
- [ ] Tutti i test TEST-BOT-03-XX passano
- [ ] PNG inviato come foto su Telegram con caption (test manuale)
- [ ] Fallback testo funzionante se resvg lancia
- [ ] Cold start Worker < 5 secondi dopo aggiunta wasm

## Stima
~1.5 giorni
