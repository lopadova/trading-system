# T-SW-02 — Zustand Wizard Store + TanStack Router Routes

## Obiettivo
Implementare lo store Zustand globale del wizard con gestione navigazione
step, modalità (new/import/edit/convert), validazione per step, stato
publish, e le route TanStack Router per il wizard.

## Dipendenze
- T-SW-01 (tipi e validatore)

## Files da Creare
- `dashboard/src/store/wizardStore.ts`
- `dashboard/src/store/wizardStore.test.ts`

## Files da Modificare
- `dashboard/src/router.ts` — aggiungere 4 nuove route wizard

## Implementazione

### wizardStore.ts — Store Zustand con immer

```typescript
import { create } from 'zustand'
import { immer } from 'zustand/middleware/immer'
import { set as lodashSet } from 'lodash'

export type WizardMode = 'new' | 'import' | 'edit' | 'convert'
export type PublishStatus = 'idle' | 'validating' | 'publishing' | 'success' | 'error'

// Store con Immer per mutazioni immutabili sicure
export const useWizardStore = create<WizardState>()(
  immer((set, get) => ({
    currentStep: 1,
    totalSteps: 10,
    visitedSteps: [1],
    mode: 'new' as WizardMode,
    draft: createDefaultStrategy(),
    originalJson: null,
    stepErrors: {} as Record<number, ValidationError[]>,
    globalErrors: [] as ValidationError[],
    isDirty: false,

    // EL Conversion state
    elCode: '',
    conversionResult: null,
    conversionLoading: false,
    conversionError: null,

    // Publish state
    publishStatus: 'idle' as PublishStatus,
    publishedStrategyId: null,
    publishError: null,

    // NAVIGAZIONE
    goToStep: (step) => set(state => {
      const isVisited = state.visitedSteps.includes(step)
      const isNext = step === state.currentStep + 1
      if ((isVisited || isNext) && step >= 1 && step <= state.totalSteps) {
        state.currentStep = step
        if (!isVisited) state.visitedSteps.push(step)
      }
    }),

    nextStep: () => {
      const { currentStep, draft } = get()
      const errors = validateStep(currentStep, draft)
      set(state => { state.stepErrors[currentStep] = errors })
      if (errors.some(e => e.severity === 'error')) return false
      get().goToStep(currentStep + 1)
      return true
    },

    prevStep: () => {
      const { currentStep } = get()
      if (currentStep > 1) get().goToStep(currentStep - 1)
    },

    // DATI
    setField: (path, value) => set(state => {
      lodashSet(state.draft, path, value)
      state.isDirty = true
      state.stepErrors[state.currentStep] = validateStep(state.currentStep, state.draft)
    }),

    initFromJson: (json) => {
      try {
        const parsed = JSON.parse(json) as StrategyDraft
        if (!parsed.strategy_id) return { ok: false, errors: ['strategy_id mancante'] }
        set(state => {
          state.draft = { ...createDefaultStrategy(), ...parsed }
          state.mode = 'import'
          state.originalJson = json
          state.isDirty = false
          state.visitedSteps = Array.from({ length: 10 }, (_, i) => i + 1)
        })
        return { ok: true, errors: [] }
      } catch (e) {
        return { ok: false, errors: [`JSON non valido: ${String(e)}`] }
      }
    },

    validateAllSteps: () => {
      const result = validateAll(get().draft)
      set(state => { state.globalErrors = result.errors })
      return result.valid
    },

    // PUBLISH
    publish: async () => {
      set(state => { state.publishStatus = 'validating' })
      if (!get().validateAllSteps()) {
        set(state => { state.publishStatus = 'error'; state.publishError = 'Validazione fallita' })
        return
      }
      set(state => { state.publishStatus = 'publishing' })
      try {
        const res = await fetch('/api/v1/strategies/publish', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', 'X-Api-Key': import.meta.env.VITE_API_KEY },
          body: JSON.stringify({ strategy: get().draft, overwrite: false })
        })
        if (!res.ok) {
          const err = await res.json()
          if (res.status === 409) throw new Error('conflict')
          throw new Error(err.error || `HTTP ${res.status}`)
        }
        const result = await res.json()
        set(state => { state.publishStatus = 'success'; state.publishedStrategyId = result.strategy_id; state.isDirty = false })
      } catch (e) {
        set(state => { state.publishStatus = 'error'; state.publishError = String(e) })
      }
    },

    resetWizard: () => set(state => {
      state.currentStep = 1; state.visitedSteps = [1]; state.mode = 'new'
      state.draft = createDefaultStrategy(); state.originalJson = null
      state.stepErrors = {}; state.globalErrors = []; state.isDirty = false
      state.elCode = ''; state.conversionResult = null
      state.publishStatus = 'idle'; state.publishedStrategyId = null; state.publishError = null
    }),
  }))
)
```

### router.ts — Nuove route

```typescript
// Aggiungere dentro il route tree esistente:
export const strategyWizardRoute = createRoute({
  getParentRoute: () => tradingRoute,
  path: '/strategies/new',
  component: StrategyWizardPage,
  validateSearch: (search) => ({ step: Number(search.step) || 1 }),
})
export const strategyImportRoute = createRoute({
  getParentRoute: () => tradingRoute,
  path: '/strategies/import',
  component: StrategyImportPage,
})
export const strategyConvertRoute = createRoute({
  getParentRoute: () => tradingRoute,
  path: '/strategies/convert',
  component: StrategyConvertPage,
})
export const strategyEditRoute = createRoute({
  getParentRoute: () => tradingRoute,
  path: '/strategies/$strategyId/edit',
  component: StrategyWizardPage,
})
```

## Test

- `TEST-SW-02-01`: `setField('entry_filters.ivts.suspend_threshold', 1.20)` → draft aggiornato al path annidato
- `TEST-SW-02-02`: `nextStep()` con errori step corrente → `false`, `currentStep` invariato
- `TEST-SW-02-03`: `nextStep()` con step valido → `true`, `currentStep` incrementato
- `TEST-SW-02-04`: `goToStep(5)` senza aver visitato 5 → `currentStep` invariato
- `TEST-SW-02-05`: `goToStep(2)` dopo step 1 → naviga (è il successivo)
- `TEST-SW-02-06`: `initFromJson(validJson)` → `mode='import'`, tutti step visitabili
- `TEST-SW-02-07`: `initFromJson('{ invalid }')` → `{ ok: false, errors: [...] }`
- `TEST-SW-02-08`: `validateAllSteps()` con draft completo → `true`
- `TEST-SW-02-09`: `publish()` con validazione fallita → `publishStatus === 'error'`
- `TEST-SW-02-10`: `resetWizard()` → `currentStep === 1`, `mode === 'new'`

## Done Criteria
- [ ] Build pulito (`bun run build` → 0 errori)
- [ ] Tutti i test TEST-SW-02-XX passano
- [ ] Route `/strategies/new` renderizza senza crash (anche con componente stub)
- [ ] `setField` con path annidato profondo aggiorna correttamente il draft
- [ ] `nextStep()` con errori → non avanza (blocco verificato)

## Stima
~1 giorno
