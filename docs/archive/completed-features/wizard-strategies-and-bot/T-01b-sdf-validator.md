# T-SW-01b — SDF v1 Client-Side Validator

## Obiettivo
Implementare validatore client-side con regole di validazione per ogni campo SDF v1,
supporto validazione cross-field, messaggi in italiano, e validazione per step wizard.

## Dipendenze
- T-SW-01a (tipi TypeScript sdf-v1.ts)

## Files da Creare
- `dashboard/src/utils/sdf-validator.ts`
- `dashboard/src/utils/sdf-validator.test.ts`

## Files da Modificare
Nessuno.

## Implementazione

### sdf-validator.ts — Validazione completa

```typescript
import type { StrategyDraft, StrategyDefinition, StrategyLeg, HardStopCondition } from '../types/sdf-v1'
import { get, has } from 'lodash-es'

export interface ValidationError {
  field: string           // dot-path: "entry_filters.ivts.suspend_threshold"
  message: string         // messaggio in italiano
  severity: 'error' | 'warning'
  suggestion?: string     // suggerimento per correggere (opzionale)
}

export interface ValidationResult {
  valid: boolean
  errors: ValidationError[]
  warnings: ValidationError[]
}

/**
 * Valida un singolo campo con path dot-notation.
 * Ritorna null se valido, ValidationError se invalido.
 */
export function validateField(
  path: string,
  value: unknown,
  draft: StrategyDraft
): ValidationError | null {
  // Validazioni specifiche per campo
  
  // Nome strategia
  if (path === 'name') {
    if (!value || typeof value !== 'string') {
      return { field: path, message: 'Nome strategia obbligatorio', severity: 'error' }
    }
    if (value.length < 3) {
      return { field: path, message: 'Nome deve essere almeno 3 caratteri', severity: 'error' }
    }
    if (value.length > 100) {
      return { field: path, message: 'Nome troppo lungo (max 100 caratteri)', severity: 'error' }
    }
  }

  // Strategy ID
  if (path === 'strategy_id') {
    if (!value || typeof value !== 'string') {
      return { field: path, message: 'ID strategia obbligatorio', severity: 'error' }
    }
    if (!/^[a-z0-9-]+$/.test(value)) {
      return { 
        field: path, 
        message: 'ID deve contenere solo lettere minuscole, numeri e trattini',
        severity: 'error',
        suggestion: 'Usa "my-strategy-name" invece di "My Strategy Name"'
      }
    }
    if (value.length > 50) {
      return { field: path, message: 'ID troppo lungo (max 50 caratteri)', severity: 'error' }
    }
  }

  // Delta validation (common per molti campi)
  if (path.includes('target_delta')) {
    if (typeof value !== 'number') {
      return { field: path, message: 'Delta deve essere un numero', severity: 'error' }
    }
    if (value < 0.01 || value > 0.99) {
      return { 
        field: path, 
        message: 'Delta deve essere tra 0.01 e 0.99',
        severity: 'error',
        suggestion: 'Valori tipici: 0.30 per vendite, 0.16 per protezioni'
      }
    }
  }

  // DTE validation
  if (path.includes('target_dte')) {
    if (typeof value !== 'number') {
      return { field: path, message: 'DTE deve essere un numero', severity: 'error' }
    }
    if (value < 0 || value > 365) {
      return { field: path, message: 'DTE deve essere tra 0 e 365', severity: 'error' }
    }
  }

  // Quantity validation
  if (path.includes('quantity')) {
    if (typeof value !== 'number') {
      return { field: path, message: 'Quantità deve essere un numero', severity: 'error' }
    }
    if (value <= 0) {
      return { field: path, message: 'Quantità deve essere maggiore di 0', severity: 'error' }
    }
    if (!Number.isInteger(value)) {
      return { field: path, message: 'Quantità deve essere un numero intero', severity: 'error' }
    }
  }

  // Profit/Loss thresholds
  if (path === 'exit_rules.profit_target_usd') {
    if (typeof value !== 'number' || value <= 0) {
      return { field: path, message: 'Target profitto deve essere maggiore di 0', severity: 'error' }
    }
  }

  if (path === 'exit_rules.stop_loss_usd') {
    if (typeof value !== 'number' || value <= 0) {
      return { field: path, message: 'Stop loss deve essere maggiore di 0', severity: 'error' }
    }
  }

  // IVTS thresholds
  if (path === 'entry_filters.ivts.suspend_threshold') {
    if (typeof value !== 'number' || value <= 0) {
      return { field: path, message: 'Soglia sospensione IVTS deve essere maggiore di 0', severity: 'error' }
    }
  }

  if (path === 'entry_filters.ivts.resume_threshold') {
    if (typeof value !== 'number' || value <= 0) {
      return { field: path, message: 'Soglia ripresa IVTS deve essere maggiore di 0', severity: 'error' }
    }
  }

  return null
}

/**
 * Valida regole cross-field (che coinvolgono più campi).
 */
export function validateCrossField(draft: StrategyDraft): ValidationError[] {
  const errors: ValidationError[] = []

  // IVTS: suspend_threshold > resume_threshold
  const suspendThreshold = get(draft, 'entry_filters.ivts.suspend_threshold')
  const resumeThreshold = get(draft, 'entry_filters.ivts.resume_threshold')
  
  if (typeof suspendThreshold === 'number' && typeof resumeThreshold === 'number') {
    if (suspendThreshold <= resumeThreshold) {
      errors.push({
        field: 'entry_filters.ivts.suspend_threshold',
        message: 'Soglia sospensione deve essere maggiore della soglia ripresa',
        severity: 'error',
        suggestion: `Suggerimento: suspend=${(resumeThreshold + 0.05).toFixed(2)}, resume=${resumeThreshold}`
      })
    }
  }

  // Hard stops: reference_leg_id deve esistere in structure.legs
  const legs = get(draft, 'structure.legs', []) as StrategyLeg[]
  const hardStops = get(draft, 'exit_rules.hard_stop_conditions', []) as HardStopCondition[]
  
  const legIds = new Set(legs.map(l => l.leg_id))
  
  for (const stop of hardStops) {
    if (stop.reference_leg_id && !legIds.has(stop.reference_leg_id)) {
      errors.push({
        field: `exit_rules.hard_stop_conditions[${stop.condition_id}].reference_leg_id`,
        message: `Leg "${stop.reference_leg_id}" non trovato nella struttura`,
        severity: 'error',
        suggestion: `Leg disponibili: ${Array.from(legIds).join(', ')}`
      })
    }
  }

  // Legs: almeno 1 leg richiesto
  if (!legs || legs.length === 0) {
    errors.push({
      field: 'structure.legs',
      message: 'Almeno un leg è obbligatorio',
      severity: 'error',
      suggestion: 'Aggiungi almeno un leg nella sezione Struttura'
    })
  }

  // Legs: leg_id univoci
  const legIdCounts = new Map<string, number>()
  for (const leg of legs) {
    const count = legIdCounts.get(leg.leg_id) || 0
    legIdCounts.set(leg.leg_id, count + 1)
  }
  
  for (const [legId, count] of legIdCounts.entries()) {
    if (count > 1) {
      errors.push({
        field: 'structure.legs',
        message: `Leg ID "${legId}" duplicato (trovato ${count} volte)`,
        severity: 'error',
        suggestion: 'Ogni leg deve avere un ID univoco'
      })
    }
  }

  return errors
}

/**
 * Valida tutti i campi di un draft.
 * Ritorna tutte le validazioni (field + cross-field).
 */
export function validateAll(draft: StrategyDraft): ValidationResult {
  const errors: ValidationError[] = []
  const warnings: ValidationError[] = []

  // Validazioni field-level
  const fieldsToValidate = [
    'name',
    'strategy_id',
    'author',
    'description',
    'exit_rules.profit_target_usd',
    'exit_rules.stop_loss_usd',
    'exit_rules.max_days_in_position',
    'entry_filters.ivts.suspend_threshold',
    'entry_filters.ivts.resume_threshold',
    'campaign_rules.max_active_campaigns',
    'campaign_rules.max_per_rolling_week'
  ]

  for (const field of fieldsToValidate) {
    const value = get(draft, field)
    const error = validateField(field, value, draft)
    if (error) {
      if (error.severity === 'error') {
        errors.push(error)
      } else {
        warnings.push(error)
      }
    }
  }

  // Validazioni legs
  const legs = get(draft, 'structure.legs', []) as StrategyLeg[]
  for (let i = 0; i < legs.length; i++) {
    const leg = legs[i]
    const legErrors = [
      validateField(`structure.legs[${i}].target_delta`, leg.target_delta, draft),
      validateField(`structure.legs[${i}].target_dte`, leg.target_dte, draft),
      validateField(`structure.legs[${i}].quantity`, leg.quantity, draft)
    ].filter(Boolean) as ValidationError[]
    
    errors.push(...legErrors.filter(e => e.severity === 'error'))
    warnings.push(...legErrors.filter(e => e.severity === 'warning'))
  }

  // Validazioni cross-field
  const crossErrors = validateCrossField(draft)
  errors.push(...crossErrors.filter(e => e.severity === 'error'))
  warnings.push(...crossErrors.filter(e => e.severity === 'warning'))

  return {
    valid: errors.length === 0,
    errors,
    warnings
  }
}

/**
 * Valida un singolo step del wizard.
 * Ogni step valida solo i campi pertinenti.
 */
export function validateStep(step: number, draft: StrategyDraft): ValidationError[] {
  const stepFields: Record<number, string[]> = {
    1: ['name', 'strategy_id', 'author', 'description'],
    2: ['structure.legs'],
    3: ['entry_filters.ivts.suspend_threshold', 'entry_filters.ivts.resume_threshold'],
    4: ['exit_rules.profit_target_usd', 'exit_rules.stop_loss_usd', 'exit_rules.hard_stop_conditions'],
    // ... altri step
  }

  const fields = stepFields[step] || []
  const errors: ValidationError[] = []

  for (const field of fields) {
    const value = get(draft, field)
    const error = validateField(field, value, draft)
    if (error && error.severity === 'error') {
      errors.push(error)
    }
  }

  // Aggiungi cross-field se pertinente
  if (step === 3) {
    const crossErrors = validateCrossField(draft)
    errors.push(...crossErrors.filter(e => 
      e.field.startsWith('entry_filters.ivts') && e.severity === 'error'
    ))
  }

  if (step === 2) {
    const crossErrors = validateCrossField(draft)
    errors.push(...crossErrors.filter(e => 
      e.field.startsWith('structure.legs') && e.severity === 'error'
    ))
  }

  return errors
}
```

## Test

### sdf-validator.test.ts

- `TEST-SW-01b-01`: `validateAll(emptyDraft)` → `valid=false`, `errors.length > 5`
- `TEST-SW-01b-02`: `validateAll(completeDraft)` → `valid=true`, `errors.length === 0`
- `TEST-SW-01b-03`: suspend=1.10, resume=1.10 → cross-field error su suspend_threshold
- `TEST-SW-01b-04`: suspend=1.15, resume=1.10 → nessun errore cross-field
- `TEST-SW-01b-05`: `target_delta = 1.5` → error "deve essere tra 0.01 e 0.99"
- `TEST-SW-01b-06`: `target_delta = 0.30` → nessun errore
- `TEST-SW-01b-07`: hard_stop con reference_leg_id="leg-999" non esistente → error descrittivo
- `TEST-SW-01b-08`: hard_stop con reference_leg_id esistente → nessun errore
- `TEST-SW-01b-09`: legs array vuoto → error "almeno un leg obbligatorio"
- `TEST-SW-01b-10`: legs con leg_id duplicati → error "leg ID duplicato"
- `TEST-SW-01b-11`: `validateField('name', 'ab', draft)` → error "almeno 3 caratteri"
- `TEST-SW-01b-12`: `validateField('strategy_id', 'My Strategy', draft)` → error "solo minuscole"
- `TEST-SW-01b-13`: `validateStep(1, draftWithoutName)` → errors contiene error su 'name'
- `TEST-SW-01b-14`: `validateStep(1, draftWithName)` → nessun errore

## Done Criteria

- [ ] `tsc --strict` compila senza errori
- [ ] Tutti i test TEST-SW-01b-XX passano
- [ ] No regression su test esistenti
- [ ] Messaggi errore tutti in italiano
- [ ] Ogni messaggio ha suggestion quando applicabile
- [ ] validateStep testato per almeno 3 step diversi

## Stima

~6 ore
