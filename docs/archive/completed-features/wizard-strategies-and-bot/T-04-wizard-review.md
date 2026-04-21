# T-SW-04 — Steps 1-5 del Wizard (Identity → Structure)

## Obiettivo
Implementare i primi 5 step del wizard: Identità Strategia, Strumento
Finanziario, Filtri di Ingresso, Regole Campagna, Struttura Legs (con
LegBuilder drag & drop). Sono i più complessi per interazioni UI.

## Dipendenze
- T-SW-03 (UI Shell e componenti base)

## Files da Creare
- `dashboard/src/components/strategy-wizard/steps/Step01Identity.tsx`
- `dashboard/src/components/strategy-wizard/steps/Step02Instrument.tsx`
- `dashboard/src/components/strategy-wizard/steps/Step03EntryFilters.tsx`
- `dashboard/src/components/strategy-wizard/steps/Step04CampaignRules.tsx`
- `dashboard/src/components/strategy-wizard/steps/Step05Structure.tsx`
- `dashboard/src/components/strategy-wizard/shared/LegBuilder.tsx`
- `dashboard/src/components/strategy-wizard/shared/LegCard.tsx`

## Implementazione

### Step01Identity.tsx — Campi principali
- `strategy_id`: input text, regex `[a-z0-9-]`, auto-generato da `name` (debounce 300ms), check unicità API (debounce 500ms via `GET /api/v1/strategies/check-id/{id}`)
- `name`: input text, max 100 char, onChange trigger auto-generazione strategy_id
- `description`: textarea, max 500 char, facoltativo
- `author`: input text (default da config)
- `author_url`: input url, facoltativo
- `license`: select (private / MIT / Apache-2.0)
- `tags`: multi-select con tag predefiniti (put-spread, spx, 0dte, 30dte, ecc.) + input custom
- `enabled_default`: toggle (default ON)

### Step02Instrument.tsx — Strumento
- `underlying_symbol`: autocomplete (SPX, SPY, QQQ, NDX, RUT, custom) — onChange suggerisce `sec_type` e `exchange`
- `underlying_sec_type`: radio (IND / STK / ETF)
- `underlying_exchange`: select (CBOE / NYSE / SMART)
- `options_exchange`: select (SMART / CBOE / ISE)
- `currency`: select (USD / EUR)
- `multiplier`: number input (default 100 per SPX)
- `option_right`: radio (PUT / CALL / BOTH)

### Step03EntryFilters.tsx — Filtri IVTS e orari
Card "IVTS Filter" (collassabile):
- `ivts.enabled`: toggle (se OFF → campi grigi/disabled)
- `ivts.formula`: display read-only "VIX / VIX3M"
- `ivts.suspend_threshold`: slider 1.00–2.00 (step 0.01)
- `ivts.resume_threshold`: slider 1.00–2.00 (step 0.01)
  - Validazione cross-field: resume DEVE essere < suspend → errore immediato
- `ivts.staleness_max_minutes`: number 1–60
- `ivts.fallback_behavior`: select (block / warn / allow)
- `market_hours_only`: toggle
- `safe_execution_window.enabled`: toggle
- `safe_execution_window.exclude_first_minutes`: number 0–60
- `safe_execution_window.exclude_last_minutes`: number 0–60

Preview live sotto: "Con queste impostazioni il sistema può aprire dal lun-ven tra le 09:45 e le 15:45 ET quando IVTS < 1.15"

### Step04CampaignRules.tsx
- `max_active_campaigns`: slider 1–20 + number input
- `max_per_rolling_week`: number 1–5
- `week_start_day`: radio (Lunedì / Domenica)
- `overlap_check_enabled`: toggle

### Step05Structure.tsx + LegBuilder.tsx
Layout:
- Sezione "COMBO LEGS": lista LegCard + bottone "+ Aggiungi Combo Leg"
- Template rapidi: [Sell Put OTM 30Δ] [Buy Put Hedge 16Δ] [Bull Put Spread]
- Sezione "PROTECTION LEGS": stessa struttura

LegCard (drag & drop via `@hello-pangea/dnd`):
- Header: drag handle + leg_id + collapse/expand + delete
- Body (espanso): tutti i campi del leg (action, right, target_dte, dte_tolerance, target_delta (DeltaSlider), delta_tolerance, quantity, settlement_preference, exclude_expiry_within_days, role, order_group)
- Bordo sinistro colorato: blu (sell), verde (buy), viola (protection)

## Test
- `TEST-SW-04-01`: Step1 name="My Strat" → strategy_id="my-strat" generato automaticamente
- `TEST-SW-04-02`: Step1 strategy_id="mia strategia" → errore (spazi non consentiti)
- `TEST-SW-04-03`: Step3 suspend=1.10, resume=1.10 → errore cross-field
- `TEST-SW-04-04`: Step3 suspend=1.15, resume=1.10 → nessun errore
- `TEST-SW-04-05`: Step5 senza legs → `nextStep()` → false (errore "min 1 leg")
- `TEST-SW-04-06`: Step5 template "Sell Put OTM" → leg con delta=0.30 e action='sell'
- `TEST-SW-04-07`: Step5 drag leg da pos 1 a pos 2 → array legs riordinato
- `TEST-SW-04-08`: Step5 delete leg con confirm → leg rimosso da array

## Done Criteria
- [ ] Build pulito
- [ ] Tutti i test TEST-SW-04-XX passano
- [ ] Preview live Step 3 aggiornata in real-time al cambio soglie
- [ ] Drag & drop legs funzionante (test manuale)
- [ ] Check unicità strategy_id via API con debounce (test manuale)

## Stima
~2 giorni
