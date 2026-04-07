# T-SW-05 — Steps 6-9 del Wizard (Selection → Monitoring)

## Obiettivo
Implementare gli step 6, 7, 8, 9 del wizard: Filtri Selezione Opzioni,
Regole di Uscita (con HardStopBuilder), Regole Esecuzione (repricing),
Monitoring e Notifiche.

## Dipendenze
- T-SW-04 (step 5 — reference_leg_id in step 7 dipende dai leg definiti in step 5)

## Files da Creare
- `dashboard/src/components/strategy-wizard/steps/Step06SelectionFilters.tsx`
- `dashboard/src/components/strategy-wizard/steps/Step07ExitRules.tsx`
- `dashboard/src/components/strategy-wizard/steps/Step08ExecutionRules.tsx`
- `dashboard/src/components/strategy-wizard/steps/Step09Monitoring.tsx`
- `dashboard/src/components/strategy-wizard/shared/HardStopBuilder.tsx`
- `dashboard/src/components/strategy-wizard/shared/HardStopCard.tsx`

## Implementazione

### Step06SelectionFilters.tsx
- `min_open_interest`: number 0–10000 (default 100)
- `max_spread_pct_of_mid`: slider 0–20% — track verde(0–5%) → giallo(5–10%) → rosso(10–20%)
- `scoring_method`: radio con descrizione per ogni opzione:
  - min_delta_distance: "Seleziona il contratto con delta più vicino al target (raccomandato)"
  - max_oi: "Preferisce i contratti con maggiore liquidità"
  - min_spread: "Preferisce i contratti con spread bid-ask più stretto"

### Step07ExitRules.tsx
Sezione "TARGET E STOP":
- `profit_target_usd`: currency input ($ prefix, > 0)
- `stop_loss_usd`: currency input ($ prefix, > 0)
- `max_days_in_position`: number 1–365

HardStopBuilder (array di condizioni):
- Bottone "+ Aggiungi Hard Stop" (rosso outline)
- Per ogni HardStopCard:
  - `condition_id`: auto + modificabile
  - `type`: radio (underlying_vs_leg_strike / portfolio_greek / pnl_threshold)
  - `reference_leg_id`: select da `structure.legs[].leg_id` [se type=underlying_vs_leg_strike]
  - `greek`: select delta|theta|gamma|vega [se type=portfolio_greek]
  - `operator`: select lt|gt|lte|gte
  - `threshold`: number
  - `severity`: radio critical|high|medium
  - `close_sequence`: radio all_legs|combo_only|hedge_only

Preview "Scenari di uscita":
Card che riepiloga: ✅ Target +$2000 | 🛑 Stop -$5000 | ⏰ Max 60gg | 🚨 Hard stop N condizioni

### Step08ExecutionRules.tsx
- `order_type`: radio (limit_mid / limit_ask / limit_bid / market)
  - market → warning prominente "⚠️ Non raccomandato per opzioni SPX"
- Card "REPRICING" (tutti i campi disabled se repricing.enabled = false):
  - `repricing.enabled`: toggle
  - `repricing.max_attempts`: number 1–10
  - `repricing.interval_seconds`: slider 10–120
  - `repricing.step_pct_of_half_spread`: slider 1–50%
  - `repricing.max_slippage_pct_from_first_mid`: slider 1–20%
  - `repricing.fallback_on_max_attempts`: radio
- `opening_sequence`: radio (combo_first / hedge_first / simultaneous)
- `margin_buffer_pct`: slider 5–50%
- `what_if_check_enabled`: toggle
- `gtc_target_order.enabled`: toggle
- `gtc_target_order.submit_immediately_after_fill`: toggle [se gtc enabled]

### Step09Monitoring.tsx
- `greeks_snapshot_interval_minutes`: slider 5–60
- `risk_check_interval_minutes`: slider 1–30
- Griglia di toggle notifiche:
  - on_campaign_opened, on_target_hit, on_stop_loss_hit
  - on_hard_stop_triggered (non disabilitabile → warning se tentativo)
  - on_max_days_close, on_ivts_state_change

## Test
- `TEST-SW-05-01`: Step7 type=underlying_vs_leg_strike → `reference_leg_id` select visibile
- `TEST-SW-05-02`: Step7 type=portfolio_greek → `greek` select visibile, reference_leg_id nascosto
- `TEST-SW-05-03`: Step7 preview aggiornata quando `profit_target_usd` cambia
- `TEST-SW-05-04`: Step8 repricing.enabled=false → `max_attempts` campo disabilitato
- `TEST-SW-05-05`: Step8 order_type=market → banner warning visibile
- `TEST-SW-05-06`: Step9 toggle hard_stop OFF → warning "non disabilitabile", toggle torna ON
- `TEST-SW-05-07`: Step7 HardStop con reference_leg_id non in legs → errore validazione

## Done Criteria
- [ ] Build pulito
- [ ] Tutti i test TEST-SW-05-XX passano
- [ ] reference_leg_id si popola con i leg definiti in Step 5 (accesso allo store)
- [ ] Campi condizionali (greek, reference_leg_id) mostrati/nascosti correttamente

## Stima
~1.5 giorni
