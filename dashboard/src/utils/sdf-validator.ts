/**
 * SDF v1 Client-Side Validator
 *
 * Provides comprehensive validation for Strategy Definition Format v1.
 * Supports field-level, cross-field, and wizard step validation.
 * All messages in Italian as per specification.
 */

import type { StrategyDraft, StrategyLeg, HardStopCondition } from '../types/sdf-v1'
import { get } from 'lodash-es'

// ============================================================================
// TYPES
// ============================================================================

export interface ValidationError {
  /** Dot-path to the field (e.g., "entry_filters.ivts.suspend_threshold") */
  field: string
  /** Error message in Italian */
  message: string
  /** Error severity */
  severity: 'error' | 'warning'
  /** Optional suggestion to fix the error */
  suggestion?: string
  /** Step number where error occurred (1-10 for wizard, 0 for global) */
  step?: number
}

export interface ValidationResult {
  /** True if no errors exist */
  valid: boolean
  /** List of validation errors */
  errors: ValidationError[]
  /** List of validation warnings */
  warnings: ValidationError[]
}

// ============================================================================
// FIELD-LEVEL VALIDATION
// ============================================================================

/**
 * Validates a single field with dot-notation path.
 * Returns null if valid, ValidationError if invalid.
 *
 * @param path - Dot-notation path to field (e.g., "entry_filters.ivts.suspend_threshold")
 * @param value - Value to validate
 * @param draft - Complete draft for context
 * @returns ValidationError if invalid, null if valid
 */
export function validateField(
  path: string,
  value: unknown,
  _draft: StrategyDraft
): ValidationError | null {
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
        suggestion: 'Usa "my-strategy-name" invece di "My Strategy Name"',
      }
    }
    if (value.length > 50) {
      return { field: path, message: 'ID troppo lungo (max 50 caratteri)', severity: 'error' }
    }
  }

  // Author
  if (path === 'author') {
    if (!value || typeof value !== 'string') {
      return { field: path, message: 'Autore obbligatorio', severity: 'error' }
    }
  }

  // Description
  if (path === 'description') {
    if (!value || typeof value !== 'string') {
      return { field: path, message: 'Descrizione obbligatoria', severity: 'error' }
    }
  }

  // Delta validation (common for many fields)
  if (path.includes('target_delta')) {
    if (typeof value !== 'number') {
      return { field: path, message: 'Delta deve essere un numero', severity: 'error' }
    }
    if (value < 0.01 || value > 0.99) {
      return {
        field: path,
        message: 'Delta deve essere tra 0.01 e 0.99',
        severity: 'error',
        suggestion: 'Valori tipici: 0.30 per vendite, 0.16 per protezioni',
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

  if (path === 'exit_rules.max_days_in_position') {
    if (typeof value !== 'number' || value < 0) {
      return {
        field: path,
        message: 'Giorni massimi in posizione deve essere 0 o maggiore',
        severity: 'error',
      }
    }
  }

  // IVTS thresholds
  if (path === 'entry_filters.ivts.suspend_threshold') {
    if (typeof value !== 'number' || value <= 0) {
      return {
        field: path,
        message: 'Soglia sospensione IVTS deve essere maggiore di 0',
        severity: 'error',
      }
    }
  }

  if (path === 'entry_filters.ivts.resume_threshold') {
    if (typeof value !== 'number' || value <= 0) {
      return {
        field: path,
        message: 'Soglia ripresa IVTS deve essere maggiore di 0',
        severity: 'error',
      }
    }
  }

  // Campaign rules
  if (path === 'campaign_rules.max_active_campaigns') {
    if (typeof value !== 'number' || value <= 0 || !Number.isInteger(value)) {
      return {
        field: path,
        message: 'Massimo campagne attive deve essere un numero intero maggiore di 0',
        severity: 'error',
      }
    }
  }

  if (path === 'campaign_rules.max_per_rolling_week') {
    if (typeof value !== 'number' || value <= 0 || !Number.isInteger(value)) {
      return {
        field: path,
        message: 'Massimo campagne per settimana deve essere un numero intero maggiore di 0',
        severity: 'error',
      }
    }
  }

  return null
}

// ============================================================================
// CROSS-FIELD VALIDATION
// ============================================================================

/**
 * Validates rules that involve multiple fields.
 * Returns array of ValidationErrors.
 *
 * @param draft - Strategy draft to validate
 * @returns Array of validation errors
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
        suggestion: `Suggerimento: suspend=${(resumeThreshold + 0.05).toFixed(2)}, resume=${resumeThreshold}`,
      })
    }
  }

  // Hard stops: reference_leg_id must exist in structure.legs
  const legs = get(draft, 'structure.legs', []) as StrategyLeg[]
  const hardStops = get(draft, 'exit_rules.hard_stop_conditions', []) as HardStopCondition[]

  const legIds = new Set(legs.map((l) => l.leg_id))

  for (const stop of hardStops) {
    if (stop.reference_leg_id && !legIds.has(stop.reference_leg_id)) {
      errors.push({
        field: `exit_rules.hard_stop_conditions[${stop.condition_id}].reference_leg_id`,
        message: `Leg "${stop.reference_leg_id}" non trovato nella struttura`,
        severity: 'error',
        suggestion: `Leg disponibili: ${Array.from(legIds).join(', ')}`,
      })
    }
  }

  // Legs: almeno 1 leg richiesto
  if (!legs || legs.length === 0) {
    errors.push({
      field: 'structure.legs',
      message: 'Almeno un leg è obbligatorio',
      severity: 'error',
      suggestion: 'Aggiungi almeno un leg nella sezione Struttura',
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
        suggestion: 'Ogni leg deve avere un ID univoco',
      })
    }
  }

  return errors
}

// ============================================================================
// COMPLETE VALIDATION
// ============================================================================

/**
 * Validates all fields of a draft.
 * Returns all validation results (field + cross-field).
 *
 * @param draft - Strategy draft to validate
 * @returns Validation result with errors and warnings
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
    'campaign_rules.max_per_rolling_week',
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
    if (!leg) continue

    const legErrors = [
      validateField(`structure.legs[${i}].target_delta`, leg.target_delta, draft),
      validateField(`structure.legs[${i}].target_dte`, leg.target_dte, draft),
      validateField(`structure.legs[${i}].quantity`, leg.quantity, draft),
    ].filter(Boolean) as ValidationError[]

    errors.push(...legErrors.filter((e) => e.severity === 'error'))
    warnings.push(...legErrors.filter((e) => e.severity === 'warning'))
  }

  // Validazioni cross-field
  const crossErrors = validateCrossField(draft)
  errors.push(...crossErrors.filter((e) => e.severity === 'error'))
  warnings.push(...crossErrors.filter((e) => e.severity === 'warning'))

  return {
    valid: errors.length === 0,
    errors,
    warnings,
  }
}

// ============================================================================
// WIZARD STEP VALIDATION
// ============================================================================

/**
 * Validates a single step of the wizard.
 * Each step validates only the fields pertinent to that step.
 *
 * @param step - Step number (1-based)
 * @param draft - Strategy draft to validate
 * @returns Array of validation errors for this step
 */
export function validateStep(step: number, draft: StrategyDraft): ValidationError[] {
  const stepFields: Record<number, string[]> = {
    1: ['name', 'strategy_id', 'author', 'description'],
    2: ['structure.legs'],
    3: ['entry_filters.ivts.suspend_threshold', 'entry_filters.ivts.resume_threshold'],
    4: ['exit_rules.profit_target_usd', 'exit_rules.stop_loss_usd', 'exit_rules.hard_stop_conditions'],
    // Additional steps can be added here
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

  // Add cross-field validations for specific steps
  if (step === 3) {
    const crossErrors = validateCrossField(draft)
    errors.push(
      ...crossErrors.filter((e) => e.field.startsWith('entry_filters.ivts') && e.severity === 'error')
    )
  }

  if (step === 2) {
    const crossErrors = validateCrossField(draft)
    errors.push(
      ...crossErrors.filter((e) => e.field.startsWith('structure.legs') && e.severity === 'error')
    )
  }

  if (step === 4) {
    const crossErrors = validateCrossField(draft)
    errors.push(
      ...crossErrors.filter(
        (e) => e.field.startsWith('exit_rules.hard_stop_conditions') && e.severity === 'error'
      )
    )
  }

  return errors
}
