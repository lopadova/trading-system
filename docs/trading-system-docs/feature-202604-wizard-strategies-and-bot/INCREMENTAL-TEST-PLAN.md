# Incremental Test Coverage Plan — Feature Wizard & Bot

> **Obiettivo**: Test coverage incrementale per task, non solo alla fine (T-08, T-12).
> Ogni task ha test specifici che devono passare prima di procedere al successivo.

---

## Principio Generale

Ogni task T-XX include:
1. **Unit tests** per funzioni/componenti isolati
2. **Integration tests** per interazioni tra componenti
3. **E2E tests** (selettivi) solo per flussi critici

**Done Criteria**: Task non è DONE se anche un solo test fallisce.

---

## Task-by-Task Test Coverage

### T-00 (Setup)
✅ **Già completato**
- Build .NET pulito (0 errori)
- Dashboard build pulito
- Worker build pulito

### T-01a (SDF Types)
**Unit Tests**:
- TypeScript compilation (`tsc --strict`) → 0 errori
- Type guards (`isStrategyDefinition`, `isStrategyLeg`)
- DeepPartial permette draft parziali

**Coverage Target**: 100% dei type guards

**Test Framework**: Vitest

**Done Gate**: Tutti i test TEST-SW-01a-XX passano

---

### T-01b (SDF Validator)
**Unit Tests**:
- `validateField()` per ogni campo critico
  - name, strategy_id, delta, DTE, quantity, profit/loss
- `validateCrossField()` per regole cross-field
  - IVTS suspend > resume
  - hard_stop reference_leg_id esistente
  - legs array non vuoto
  - leg_id univoci
- `validateAll()` su draft vuoto e completo
- `validateStep()` per almeno 3 step

**Coverage Target**: 90%+ dei validatori

**Test Framework**: Vitest

**Done Gate**: Tutti i test TEST-SW-01b-XX passano

---

### T-01c (SDF Defaults)
**Unit Tests**:
- `generateStrategyId()` con casi edge (unicode, lunghezza, speciali)
- `createDefaultStrategy()` genera draft valido
- `createDefaultLeg()` valori corretti per action
- `createDefaultHardStop()` valori per type
- `cloneStrategy()` genera nuovo ID e version
- `incrementVersion()` semver corretto

**Integration Test**:
- `validateAll(createDefaultStrategy())` → valid=true

**Coverage Target**: 100% delle utility functions

**Test Framework**: Vitest

**Done Gate**: Tutti i test TEST-SW-01c-XX passano + integration test

---

### T-02 (Wizard Store)
**Unit Tests**:
- Zustand store actions (setDraft, updateField, nextStep, prevStep)
- Validazione chiamata prima di nextStep
- applyConversionResult aggiorna draft correttamente

**Integration Tests**:
- Store + Validator: nextStep bloccato se step ha errori
- Store persist/hydrate

**Coverage Target**: 85%+ dello store

**Test Framework**: Vitest + @testing-library/react

**Done Gate**: Tutti i test TEST-SW-02-XX passano

---

### T-03 (Wizard UI Components)
**Unit Tests**:
- Rendering dei componenti base (WizardContainer, StepIndicator)
- Navigation step → step

**Integration Tests**:
- Store integrato con componenti UI
- Validation errors mostrati in UI

**Visual Regression** (opzionale):
- Screenshot step 1-10 con Playwright

**Coverage Target**: 75%+ componenti UI

**Test Framework**: Vitest + @testing-library/react + Playwright (E2E)

**Done Gate**: Tutti i test TEST-SW-03-XX passano + UI rendering OK

---

### T-04 (Step Identity)
**Unit Tests**:
- Step01Identity form validation
- generateStrategyId chiamato on blur

**Integration Tests**:
- Form submit aggiorna store
- Errors visualizzati correttamente

**Coverage Target**: 80%+ step component

**Test Framework**: Vitest + @testing-library/react

**Done Gate**: Tutti i test TEST-SW-04-XX passano

---

### T-05 (Step Legs)
**Unit Tests**:
- LegBuilder add/remove/reorder legs
- DeltaSlider aggiorna target_delta

**Integration Tests**:
- Legs aggiunti al draft
- Validazione cross-field (leg_id univoci)

**Coverage Target**: 80%+ step component

**Test Framework**: Vitest + @testing-library/react

**Done Gate**: Tutti i test TEST-SW-05-XX passano

---

### T-06 (Step Filters)
**Unit Tests**:
- IVTS filter form
- Safe execution window toggle

**Integration Tests**:
- Cross-field validation IVTS (suspend > resume)

**Coverage Target**: 80%+ step component

**Test Framework**: Vitest + @testing-library/react

**Done Gate**: Tutti i test TEST-SW-06-XX passano

---

### T-07a (EL Editor Panel)
**Unit Tests**:
- ELCodeEditor Tab key → 4 spazi
- Syntax highlighting keywords/strings/numbers
- "Incolla esempio" button

**Coverage Target**: 85%+ editor component

**Test Framework**: Vitest + @testing-library/react

**Done Gate**: Tutti i test TEST-SW-07a-XX passano

---

### T-07b (Worker Claude API)
**Unit Tests**:
- Request validation (body vuoto, troppo grande)
- JSON parsing da Claude response
- Graceful degradation senza API key

**Integration Tests**:
- Worker endpoint /convert-el → 200 con result
- D1 log creato dopo conversione
- Anthropic API mock per test senza consumo crediti

**Coverage Target**: 90%+ endpoint logic

**Test Framework**: Vitest + miniflare (Cloudflare Worker test env)

**Done Gate**: Tutti i test TEST-SW-07b-XX passano + D1 schema migrated

---

### T-07c (Conversion Result Panel)
**Unit Tests**:
- ConversionResultPanel rendering per ogni stato (loading/success/error)
- IssuesList visualizza issues correttamente
- applyConversionResult chiamato su click

**Integration Tests**:
- useELConversion hook → fetch API → result
- Apply to wizard → draft aggiornato + navigate

**Coverage Target**: 85%+ result panel

**Test Framework**: Vitest + @testing-library/react + MSW (API mocking)

**Done Gate**: Tutti i test TEST-SW-07c-XX passano

---

### T-08 (E2E Wizard)
**E2E Tests** (Playwright):
- Flusso completo: apri wizard → compila tutti gli step → review → download JSON
- Import JSON → wizard precompilato
- EL converter → apply → wizard precompilato
- Validazione blocca nextStep se errori

**Coverage Target**: 3-4 flussi principali

**Test Framework**: Playwright

**Done Gate**: Tutti i test E2E-W-XX passano + no regression

---

### T-09 (Bot Setup)
**Unit Tests**:
- Webhook signature verification (HMAC Telegram, Ed25519 Discord)
- Whitelist check
- Router command parsing

**Integration Tests**:
- Webhook handler → query D1 → risposta bot
- Log comando in D1

**Coverage Target**: 90%+ bot core logic

**Test Framework**: Vitest + miniflare

**Done Gate**: Tutti i test TEST-SW-09-XX passano

---

### T-10 (Bot Commands)
**Unit Tests**:
- Formatter messaggi Markdown
- Semafori 🔴🟡🟢 logic
- i18n IT/EN

**Integration Tests**:
- Command /portfolio → query D1 → risposta formattata
- Command /risk → semafori corretti

**Coverage Target**: 85%+ command handlers

**Test Framework**: Vitest + D1 mock

**Done Gate**: Tutti i test TEST-SW-10-XX passano

---

### T-11 (Bot Whitelist)
**Unit Tests**:
- Whitelist check true/false
- Unauthorized response

**Integration Tests**:
- User non in whitelist → 403 response

**Coverage Target**: 95%+ whitelist logic

**Test Framework**: Vitest

**Done Gate**: Tutti i test TEST-SW-11-XX passano

---

### T-12 (E2E Bot)
**E2E Tests**:
- Telegram bot: send /start → ricevi menu
- Telegram bot: send /portfolio → ricevi PnL
- Discord bot: send /risk → ricevi semafori
- User non autorizzato → blocked

**Coverage Target**: 4-5 flussi principali bot

**Test Framework**: Playwright + bot simulators

**Done Gate**: Tutti i test E2E-B-XX passano + no regression

---

## Test Execution Workflow

### Per ogni task T-XX:

```bash
# Step 1: Run unit tests
npm run test:unit -- --filter=T-XX

# Step 2: Run integration tests (se presenti)
npm run test:integration -- --filter=T-XX

# Step 3: Verify coverage
npm run test:coverage -- --filter=T-XX

# Step 4: Se T-08 o T-12, run E2E
npm run test:e2e -- --filter=E2E-W  # Wizard E2E
npm run test:e2e -- --filter=E2E-B  # Bot E2E
```

### Continuous Integration (CI)

```yaml
# .github/workflows/test-feature.yml
name: Test Feature Wizard & Bot

on:
  push:
    paths:
      - 'dashboard/src/**'
      - 'infra/cloudflare/worker/**'
      - 'tests/**'

jobs:
  test-unit:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - run: npm ci
      - run: npm run test:unit
      
  test-integration:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - run: npm ci
      - run: npm run test:integration
      
  test-e2e:
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - uses: actions/checkout@v3
      - run: npm ci
      - run: npx playwright install
      - run: npm run test:e2e
```

---

## Coverage Targets Summary

| Layer | Target | Framework |
|---|---|---|
| TypeScript Types | 100% type guards | Vitest |
| Utility Functions | 95%+ | Vitest |
| Store Logic | 85%+ | Vitest |
| UI Components | 75%+ | Vitest + RTL |
| API Endpoints | 90%+ | Vitest + Miniflare |
| E2E Critical Paths | 4-5 flussi | Playwright |

---

## Test Data & Fixtures

### Fixtures directory structure:

```
tests/
├── fixtures/
│   ├── sdf-v1/
│   │   ├── valid-strategy.json
│   │   ├── invalid-strategy.json
│   │   ├── partial-draft.json
│   │   └── complex-strategy.json
│   ├── easylanguage/
│   │   ├── iron-condor.el
│   │   ├── put-spread.el
│   │   └── unsupported-features.el
│   └── bot/
│       ├── telegram-webhook.json
│       └── discord-webhook.json
├── mocks/
│   ├── anthropic-api.ts       # Mock Claude API responses
│   ├── d1-database.ts          # Mock Cloudflare D1
│   └── ibkr-client.ts          # Mock IBKR (se necessario)
└── helpers/
    ├── test-utils.tsx          # RTL custom render
    └── setup.ts                # Global test setup
```

---

## Done Criteria — Feature Level

Feature è DONE quando:
- [ ] Tutti i task T-00 a T-12 hanno status "done"
- [ ] Tutti i test unitari passano (coverage >= target)
- [ ] Tutti i test integrazione passano
- [ ] Tutti i test E2E passano (T-08, T-12)
- [ ] No regression su test esistenti di altre feature
- [ ] CI pipeline verde
- [ ] Manuale smoke test su staging:
  - [ ] Wizard completo creazione strategia
  - [ ] Import JSON funzionante
  - [ ] EL converter funzionante (con API key reale)
  - [ ] Bot Telegram risponde a comandi
  - [ ] Bot Discord risponde a comandi
  - [ ] Whitelist blocca utenti non autorizzati

---

## Test Anti-Patterns da Evitare

❌ **NO**:
- Test che dipendono da ordine esecuzione
- Test che modificano stato globale senza cleanup
- Test che fanno chiamate API reali (no mock)
- Test E2E per ogni singolo campo form (troppo lento)
- Test con sleep/timeout arbitrari

✅ **SÌ**:
- Test isolati e indipendenti
- Mock per dipendenze esterne (API, DB)
- Test E2E solo per critical paths
- Fixtures riutilizzabili
- Fast feedback loop (unit < 1s, integration < 5s, E2E < 30s)

---

## Benefici Incremental Testing

1. **Early feedback**: Errori scoperti immediatamente, non a fine feature
2. **Safe refactoring**: Ogni task ha test che proteggono da regressioni
3. **Confidence**: Task DONE = test PASS = quality assured
4. **Documentation**: Test sono documentazione eseguibile
5. **CI/CD ready**: Ogni push può essere testato automaticamente

---

**Last updated**: 2026-04-06
