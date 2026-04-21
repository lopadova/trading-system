# T-SW-07 — EasyLanguage Converter Panel + Worker AI Endpoint

## Obiettivo
Implementare il pannello di conversione EasyLanguage → SDF v1 con editor
styled, chiamata al Worker che usa Claude API, visualizzazione risultati
con accordion, e applicazione al wizard. Comprende sia il frontend React
che il backend endpoint Worker.

## Dipendenze
- T-SW-02 (store — metodo applyConversionResult)
- T-SW-03 (componenti base UI per layout split-pane)
- Worker esistente (infra/cloudflare/worker/src/index.ts)

## Files da Creare
- `dashboard/src/components/strategy-wizard/el-converter/ELConverterPanel.tsx`
- `dashboard/src/components/strategy-wizard/el-converter/ELCodeEditor.tsx`
- `dashboard/src/components/strategy-wizard/el-converter/ConversionResultPanel.tsx`
- `dashboard/src/hooks/useELConversion.ts`
- `dashboard/src/pages/trading/strategies/StrategyConvertPage.tsx`
- `infra/cloudflare/worker/src/routes/strategies-convert.ts`

## Files da Modificare
- `infra/cloudflare/worker/src/index.ts` — aggiungere route `/api/v1/strategies/convert-el`

## Implementazione

### ELConverterPanel.tsx — layout split-pane
```
┌──────────────────────────────┬──────────────────────────────┐
│  CODICE EASYLANGUAGE          │  RISULTATO CONVERSIONE        │
│  [ELCodeEditor]               │  [ConversionResultPanel]      │
│  [📋 Incolla esempio]         │  (loading / result)           │
│  [🗑 Cancella]               │                              │
├──────────────────────────────┴──────────────────────────────┤
│         [🤖 Converti con AI →]   (amber, centrato)           │
└────────────────────────────────────────────────────────────┘
```
Mobile: stack verticale (editor sopra, risultato sotto).

### ELCodeEditor.tsx
- textarea con font JetBrains Mono, sfondo #0d1117
- Syntax highlight base EasyLanguage (keyword=amber, string=verde, number=azzurro, comment=grigio)
- Tab key → 4 spazi (preventDefault su Tab, inserisci \t×4)
- Min-height 300px, resizable verticalmente
- Placeholder: "Incolla qui il codice EasyLanguage..."
- Bottone "Incolla esempio" → precompila con EL di esempio non operativo

### ConversionResultPanel.tsx — accordion
Stati:
- Loading: animazione "Claude sta analizzando..." (pulse)
- convertible=true: ✅ + confidence% + [🔧 Applica al Wizard →]
- convertible='partial': ⚠️ + [🔧 Applica Parzialmente →]
- convertible=false: ❌ lista issues, nessun bottone applica

Accordion sezioni:
1. Issues (🔴 not_supported / 🟡 ambiguous / 🟠 manual_required)
2. Preview JSON (JSONPreview read-only)
3. Note e warning (lista gialla)

### strategies-convert.ts — Worker endpoint

```typescript
import Anthropic from '@anthropic-ai/sdk'

const SYSTEM_PROMPT = `
Sei un esperto di EasyLanguage (MultiCharts/TradeStation) e del formato SDF v1
per strategie di trading in opzioni. Converti il codice EasyLanguage in SDF v1.

LIMITAZIONI SDF v1 (documenta come issues se presenti):
- Solo strategie in opzioni (no futures, no equity)
- Greche calcolate da IBKR (non personalizzabili)
- Condizioni ingresso IVTS-based (no indicatori tecnici custom)
- Nessuna logica condizionale multi-livello if/then/else
- Nessun ML o ottimizzazione parametrica
- Solo timeframe daily (no intraday)

Rispondi SOLO in JSON (nessun markdown):
{
  "convertible": true | false | "partial",
  "confidence": 0.0-1.0,
  "result_json": { ... SDF v1 ... } | null,
  "issues": [{ "type": "not_supported"|"ambiguous"|"manual_required",
               "el_construct": "...", "description": "...", "suggestion": "..." }],
  "warnings": ["..."],
  "notes": "..."
}
`

// Validazioni request
if (!body.easylanguage_code?.trim()) return 400
if (body.easylanguage_code.length > 50_000) return 413

// Chiama Claude API (NON stream — risposta completa)
const message = await client.messages.create({
  model: 'claude-sonnet-4-5',
  max_tokens: 4096,
  system: SYSTEM_PROMPT,
  messages: [{ role: 'user', content: `Converti:\n\`\`\`\n${code}\n\`\`\`` }]
})

// Estrai JSON dalla risposta (rimuovi markdown se Claude lo aggiunge)
const rawText = message.content.filter(b => b.type === 'text').map(b => b.text).join('')
const jsonMatch = rawText.match(/\{[\s\S]*\}/)
if (!jsonMatch) return 500 "Risposta AI non parseable"

// Log in D1
await db.prepare('INSERT INTO el_conversion_log (...) VALUES (...)').run()
return result
```

## Test
- `TEST-SW-07-01`: POST /convert-el con codice EL valido → 200 con `convertible` field
- `TEST-SW-07-02`: POST /convert-el body vuoto → 400
- `TEST-SW-07-03`: POST /convert-el codice > 50000 chars → 413
- `TEST-SW-07-04`: `applyConversionResult()` → draft aggiornato con result_json + navigate to step 1
- `TEST-SW-07-05`: convertible=false → pulsante "Applica" non presente nel DOM
- `TEST-SW-07-06`: D1.el_conversion_log ha record dopo ogni chiamata
- `TEST-SW-07-07`: ELCodeEditor Tab key → 4 spazi inseriti (no blur/navigate)
- `TEST-SW-07-08`: Risposta AI senza JSON parseable → errore UI "Risposta non interpretabile"

## Done Criteria
- [ ] Build pulito (dashboard + worker)
- [ ] Tutti i test TEST-SW-07-XX passano
- [ ] ANTHROPIC_API_KEY letta da env — mai hardcoded o loggata
- [ ] Conversione manuale testata con codice EL reale (test manuale)
- [ ] Log in D1 verificato dopo conversione

## Stima
~2 giorni
