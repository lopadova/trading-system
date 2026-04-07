# T-SW-06 — Step 10: Review & Publish UI

## Obiettivo
Implementare lo step finale del wizard: riepilogo step, preview JSON
syntax-highlighted, validation summary cliccabile, conflict check su
strategy_id duplicato, download JSON locale, flusso di publish completo.

## Dipendenze
- T-SW-05 (tutti gli step 1-9 completati)
- T-SW-07 (EL Converter — mode='convert' gestito qui)
- T-SW-08 (Worker endpoint publish disponibile)

## Files da Creare
- `dashboard/src/components/strategy-wizard/steps/Step10Review.tsx`
- `dashboard/src/components/strategy-wizard/shared/StepSummaryCard.tsx`
- `dashboard/src/components/strategy-wizard/shared/ValidationSummary.tsx`
- `dashboard/src/components/strategy-wizard/shared/PublishButton.tsx`
- `dashboard/src/components/strategy-wizard/shared/ConflictDialog.tsx`
- `dashboard/src/hooks/useStrategyPublish.ts`

## Implementazione

### StepSummaryCard.tsx
Griglia 2 colonne con 9 card (step 1-9):
- Nome step + icona + badge stato (✅ OK / ⚠️ N warning / ✗ N errori / — non compilato)
- Preview 2-3 valori chiave del step
- Bottone "Modifica →" → goToStep(N)

### JSONPreview — Diff Mode
Se mode='import' o mode='edit':
- Toggle [Mostra differenze]
- Linee aggiunte: sfondo verde scuro `rgba(16,185,129,0.15)`
- Linee rimosse: sfondo rosso scuro `rgba(239,68,68,0.15)`
- Usa `diff-match-patch` per il calcolo

### ValidationSummary.tsx
Lista errori e warning raggruppati per step:
- ✗ [Step 1] strategy_id: "Identificatore già in uso"
- ⚠ [Step 8] order_type: "Ordine a mercato non raccomandato"
- Click su errore → goToStep(N), scroll al campo

### ConflictDialog.tsx
Modal che appare se il publish riceve 409:
- Testo: "La strategia '{id}' esiste già su Cloudflare."
- Bottoni: [Sovrascrivi] [Scegli nuovo ID] [Annulla]
- Sovrascrivi → re-call publish con overwrite=true
- Scegli nuovo ID → goToStep(1), focus su campo strategy_id

### PublishButton.tsx — stati
- idle: `[🚀 Pubblica su Cloud]` amber
- validating: `[⏳ Validazione...]` spinner
- publishing: `[⬆️ Caricamento...]` progress bar
- success: card verde con info download Supervisor + [📊 Vai alle Strategie] [➕ Crea un'altra]
- error: messaggio rosso + [Riprova] [Annulla]

Download JSON:
- Sempre disponibile (anche prima del publish)
- `URL.createObjectURL(new Blob([jsonStr], {type:'application/json'}))`
- Nome file: `{strategy_id}.json`

## Test
- `TEST-SW-06-01`: StepSummaryCard step 5 — click "Modifica" → goToStep(5) chiamato
- `TEST-SW-06-02`: JSONPreview con draft valido → JSON parseable (`JSON.parse` non lancia)
- `TEST-SW-06-03`: Download button → file Blob creato con nome `{strategy_id}.json`
- `TEST-SW-06-04`: Copy button → `navigator.clipboard.writeText` chiamato
- `TEST-SW-06-05`: ValidationSummary con 2 errori → 2 item cliccabili
- `TEST-SW-06-06`: Publish → 409 → ConflictDialog visibile
- `TEST-SW-06-07`: Publish → 200 → publishStatus='success', publishedStrategyId valorizzato
- `TEST-SW-06-08`: "Crea un'altra" → resetWizard() + navigate('/strategies/new')

## Done Criteria
- [ ] Build pulito
- [ ] Tutti i test TEST-SW-06-XX passano
- [ ] Flusso completo: wizard → publish → success (test manuale con Worker)
- [ ] ConflictDialog testato manualmente con strategy_id duplicato
- [ ] Diff mode visibile se JSON importato modificato

## Stima
~1.5 giorni
