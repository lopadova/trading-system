# T-SW-07b — Worker Endpoint + Claude API Integration

## Obiettivo
Implementare endpoint Worker `/api/v1/strategies/convert-el` che chiama Claude API
per convertire EasyLanguage → SDF v1, con validazioni, logging D1, e gestione errori.

## Dipendenze
- T-SW-01a (tipi SDF v1 per validazione response)
- Worker esistente (infra/cloudflare/worker/src/index.ts)
- Anthropic SDK già installato

## Files da Creare
- `infra/cloudflare/worker/src/routes/strategies-convert.ts`
- `infra/cloudflare/worker/src/prompts/el-converter-system.ts`

## Files da Modificare
- `infra/cloudflare/worker/src/index.ts` — aggiungere route
- `infra/cloudflare/worker/src/types/env.ts` — aggiungere ANTHROPIC_API_KEY

## Implementazione

### env.ts — Aggiungere API key type

```typescript
export interface Env {
  DB: D1Database
  DASHBOARD_ORIGIN: string
  API_KEY: string
  ANTHROPIC_API_KEY: string  // ← NUOVO
  // ... altri
}
```

### el-converter-system.ts — System prompt

```typescript
export const EL_CONVERTER_SYSTEM_PROMPT = `
Sei un esperto di EasyLanguage (MultiCharts/TradeStation) e del formato SDF v1
per strategie di trading in opzioni.

La tua funzione è convertire codice EasyLanguage in JSON SDF v1.

LIMITAZIONI SDF v1 (documenta come issues se presenti nel codice EL):
- Solo strategie in opzioni (no futures, no equity)
- Greche calcolate da IBKR (non personalizzabili)
- Condizioni ingresso IVTS-based (no indicatori tecnici custom)
- Nessuna logica condizionale multi-livello if/then/else
- Nessun ML o ottimizzazione parametrica
- Solo timeframe daily (no intraday)
- Max 10 legs per strategia
- Solo opzioni americane

ISTRUZIONI RISPOSTA:
Rispondi SOLO con JSON valido (nessun markdown, nessun testo prima o dopo).

Schema risposta:
{
  "convertible": true | false | "partial",
  "confidence": 0.0-1.0,
  "result_json": { ...SDF v1 completo... } | null,
  "issues": [
    {
      "type": "not_supported" | "ambiguous" | "manual_required",
      "el_construct": "nome costrutto EL",
      "description": "descrizione problema",
      "suggestion": "come risolvere manualmente"
    }
  ],
  "warnings": ["lista warning non bloccanti"],
  "notes": "note aggiuntive sulla conversione"
}

REGOLE:
1. Se convertibile al 100%: convertible=true, result_json completo
2. Se convertibile parzialmente: convertible="partial", result_json con campi convertibili, issues per i mancanti
3. Se non convertibile: convertible=false, result_json=null, issues dettagliati
4. confidence: 0.0-1.0 basato su quante assunzioni hai dovuto fare
5. Ogni issue deve avere suggestion pratico
6. warnings per best practices non seguite ma non bloccanti

ESEMPI ISSUES:
- type: "not_supported", construct: "intraday bars", suggestion: "SDF v1 supporta solo daily"
- type: "ambiguous", construct: "DaysToExp", suggestion: "Specifica target_dte esatto"
- type: "manual_required", construct: "custom indicator", suggestion: "Converti logica in IVTS formula"
`.trim()
```

### strategies-convert.ts — Endpoint implementation

```typescript
import { Hono } from 'hono'
import Anthropic from '@anthropic-ai/sdk'
import type { Env } from '../types/env'
import { EL_CONVERTER_SYSTEM_PROMPT } from '../prompts/el-converter-system'

const app = new Hono<{ Bindings: Env }>()

interface ConvertRequest {
  easylanguage_code: string
  user_notes?: string
}

interface ConversionIssue {
  type: 'not_supported' | 'ambiguous' | 'manual_required'
  el_construct: string
  description: string
  suggestion: string
}

interface ConvertResponse {
  convertible: boolean | 'partial'
  confidence: number
  result_json: Record<string, unknown> | null
  issues: ConversionIssue[]
  warnings: string[]
  notes: string
}

/**
 * POST /api/v1/strategies/convert-el
 * Converte EasyLanguage → SDF v1 via Claude API
 */
app.post('/convert-el', async (c) => {
  try {
    // Validazione request
    const body = await c.req.json<ConvertRequest>().catch(() => null)
    
    if (!body || !body.easylanguage_code?.trim()) {
      return c.json({ error: 'easylanguage_code required' }, 400)
    }

    if (body.easylanguage_code.length > 50_000) {
      return c.json({ error: 'Code too large (max 50,000 chars)' }, 413)
    }

    // Check API key
    const apiKey = c.env.ANTHROPIC_API_KEY
    if (!apiKey) {
      console.error('ANTHROPIC_API_KEY not configured')
      return c.json({ 
        error: 'AI conversion not available',
        message: 'Anthropic API key not configured. Feature disabled.'
      }, 503)
    }

    // Inizializza client Anthropic
    const anthropic = new Anthropic({ apiKey })

    // Chiama Claude API
    console.log('Calling Claude API for EL conversion...')
    const startTime = Date.now()

    const message = await anthropic.messages.create({
      model: 'claude-sonnet-4-5',
      max_tokens: 4096,
      system: EL_CONVERTER_SYSTEM_PROMPT,
      messages: [
        {
          role: 'user',
          content: `Converti questo codice EasyLanguage in SDF v1:\n\n\`\`\`\n${body.easylanguage_code}\n\`\`\``
        }
      ]
    })

    const elapsedMs = Date.now() - startTime
    console.log(`Claude API responded in ${elapsedMs}ms`)

    // Estrai JSON dalla risposta
    const textContent = message.content
      .filter(block => block.type === 'text')
      .map(block => block.type === 'text' ? block.text : '')
      .join('')

    // Rimuovi markdown code fence se presente
    const jsonMatch = textContent.match(/```json\s*\n?([\s\S]*?)\n?```/) || 
                      textContent.match(/\{[\s\S]*\}/)

    if (!jsonMatch) {
      console.error('No JSON found in Claude response:', textContent)
      return c.json({
        error: 'unparseable_response',
        message: 'Claude API response not parseable as JSON'
      }, 500)
    }

    const jsonText = jsonMatch[1] || jsonMatch[0]
    const result: ConvertResponse = JSON.parse(jsonText)

    // Validazione risposta
    if (typeof result.convertible === 'undefined' ||
        typeof result.confidence !== 'number' ||
        !Array.isArray(result.issues)) {
      console.error('Invalid response schema from Claude:', result)
      return c.json({
        error: 'invalid_response_schema',
        message: 'Claude response missing required fields'
      }, 500)
    }

    // Log in D1
    try {
      await c.env.DB.prepare(`
        INSERT INTO el_conversion_log (
          id, easylanguage_code, convertible, confidence,
          result_json, issues_count, elapsed_ms, created_at
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
      `).bind(
        crypto.randomUUID(),
        body.easylanguage_code.substring(0, 10000), // Max 10k per log
        result.convertible.toString(),
        result.confidence,
        result.result_json ? JSON.stringify(result.result_json) : null,
        result.issues.length,
        elapsedMs,
        new Date().toISOString()
      ).run()
    } catch (err) {
      console.error('Failed to log conversion to D1:', err)
      // Non fail la request se log fallisce
    }

    return c.json(result)

  } catch (error) {
    console.error('Error in /convert-el:', error)
    
    if (error instanceof Anthropic.APIError) {
      return c.json({
        error: 'anthropic_api_error',
        message: error.message,
        status: error.status
      }, error.status || 500)
    }

    return c.json({
      error: 'internal_error',
      message: 'An error occurred during conversion'
    }, 500)
  }
})

export { app as strategiesConvert }
```

### index.ts — Mount route

```typescript
// ... imports esistenti
import { strategiesConvert } from './routes/strategies-convert'

// ... middleware esistente

// Mount routes
app.route('/api/v1/strategies', strategiesConvert)
app.route('/api/positions', positions)
// ... altre route
```

## Test

- `TEST-SW-07b-01`: POST /convert-el con codice EL valido → 200 con `convertible` field
- `TEST-SW-07b-02`: POST /convert-el body vuoto → 400
- `TEST-SW-07b-03`: POST /convert-el body non JSON → 400
- `TEST-SW-07b-04`: POST /convert-el codice > 50000 chars → 413
- `TEST-SW-07b-05`: POST /convert-el senza ANTHROPIC_API_KEY → 503 con messaggio graceful
- `TEST-SW-07b-06`: Response Claude non JSON → 500 con error "unparseable_response"
- `TEST-SW-07b-07`: Response Claude schema invalido → 500 con error "invalid_response_schema"
- `TEST-SW-07b-08`: D1.el_conversion_log ha record dopo conversione
- `TEST-SW-07b-09`: Log D1 fallisce → request procede comunque (no crash)
- `TEST-SW-07b-10`: Claude API timeout → error con status appropriato
- `TEST-SW-07b-11`: System prompt non contiene API key hardcoded
- `TEST-SW-07b-12`: System prompt limita response a JSON only (no markdown)

## Done Criteria

- [ ] `npm run build` worker compila senza errori
- [ ] Tutti i test TEST-SW-07b-XX passano
- [ ] ANTHROPIC_API_KEY mai hardcoded o loggato
- [ ] Graceful degradation se API key mancante (503 invece di crash)
- [ ] D1 schema migration per `el_conversion_log` creato
- [ ] Test manuale con codice EL reale → JSON SDF v1 ricevuto

## Schema D1 Migration

```sql
-- Add to migrations/
CREATE TABLE IF NOT EXISTS el_conversion_log (
  id TEXT PRIMARY KEY,
  easylanguage_code TEXT NOT NULL,
  convertible TEXT NOT NULL, -- "true" | "false" | "partial"
  confidence REAL NOT NULL,
  result_json TEXT,
  issues_count INTEGER NOT NULL DEFAULT 0,
  elapsed_ms INTEGER NOT NULL,
  created_at TEXT NOT NULL
);

CREATE INDEX idx_el_conversion_created ON el_conversion_log(created_at DESC);
CREATE INDEX idx_el_conversion_convertible ON el_conversion_log(convertible);
```

## Stima

~8 ore
