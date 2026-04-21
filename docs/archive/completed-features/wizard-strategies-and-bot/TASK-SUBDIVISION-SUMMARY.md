# Task Subdivision Summary — Feature Wizard & Bot

> **Data**: 2026-04-06  
> **Autore**: Claude Code (orchestrated by Lorenzo Padovani)  
> **Motivo**: Suddividere task complessi T-01 e T-07 per migliorare eseguibilità e test incrementale

---

## Motivazione

Lorenzo ha richiesto:
> "T-01 è molto più complesso di quanto il nome suggerisca è tutta la fondazione SDF, non solo identity UI spezzalo per eseguirlo al meglio"
> 
> "Alcuni task potrebbero beneficiare di suddivisione (T-07 è Claude API + UI + validation) suddividi bene"
>
> "Test coverage potrebbe essere incrementale per task (non solo T-08 e T-12 finali) sono daccordo fallo."

---

## T-01 Subdivision

### Task Originale
**T-01**: "SDF v1 TypeScript Types + Client Validator + Defaults"
- 4 file da creare
- Stima: ~1 giorno
- **Problema**: Troppo ampio, mescola concerns diversi (tipi, validazione, utility)

### Nuova Suddivisione

#### T-01a — SDF v1 TypeScript Types Complete
**Scope**: Solo tipi TypeScript, nessuna logica
- File: `sdf-v1.ts`
- Test: 8 test (compilation, type guards, DeepPartial)
- Stima: ~4 ore
- **Focus**: Type safety foundation

#### T-01b — SDF v1 Client-Side Validator
**Scope**: Validazione con regole cross-field
- File: `sdf-validator.ts`, `sdf-validator.test.ts`
- Test: 14 test (field validation, cross-field, step validation)
- Stima: ~6 ore
- **Focus**: Business rules enforcement
- **Dipendenze**: T-01a

#### T-01c — SDF v1 Defaults and Utilities
**Scope**: Generazione valori default e utility functions
- File: `sdf-defaults.ts`, `sdf-defaults.test.ts`
- Test: 20 test (generateStrategyId, createDefaultStrategy, etc.)
- Stima: ~4 ore
- **Focus**: Developer experience utilities
- **Dipendenze**: T-01a

### Benefici Suddivisione T-01
✅ Ogni subtask ha scope chiaro e ben definito  
✅ Parallel execution possibile (T-01b e T-01c possono essere fatti in parallelo dopo T-01a)  
✅ Test incrementale (types → validator → defaults)  
✅ Totale: ~14 ore (vs. ~1 giorno = 8 ore stimato originale, più realistico)

---

## T-07 Subdivision

### Task Originale
**T-07**: "EasyLanguage Converter Panel + Worker AI Endpoint"
- 6 file da creare
- Stima: ~2 giorni
- **Problema**: Mescola frontend, backend, e API integration (troppi moving parts)

### Nuova Suddivisione

#### T-07a — EasyLanguage Editor Panel (Frontend Only)
**Scope**: Solo UI editor, syntax highlighting, layout
- File: `ELCodeEditor.tsx`, `ELConverterPanel.tsx`, `ELSyntaxHighlight.tsx`, `el-examples.ts`
- Test: 12 test (editor behavior, syntax highlight, responsive layout)
- Stima: ~6 ore
- **Focus**: User interface per input EasyLanguage
- **Dipendenze**: T-02 (store placeholder), T-03 (UI components)

#### T-07b — Worker Endpoint + Claude API Integration
**Scope**: Backend Worker endpoint, Claude API call, D1 logging
- File: `strategies-convert.ts`, `el-converter-system.ts`, migration D1
- Test: 12 test (request validation, API integration, D1 logging, graceful degradation)
- Stima: ~8 ore
- **Focus**: AI conversion logic
- **Dipendenze**: T-01a (tipi per validazione response)

#### T-07c — Conversion Result Panel + Apply to Wizard
**Scope**: UI risultati, hook API, integrazione wizard
- File: `ConversionResultPanel.tsx`, `IssuesList.tsx`, `JSONPreview.tsx`, `useELConversion.ts`
- Test: 12 test (result display, apply to wizard, error handling)
- Stima: ~6 ore
- **Focus**: Result visualization e workflow integration
- **Dipendenze**: T-07a, T-07b, T-02 (wizard store)

### Benefici Suddivisione T-07
✅ Frontend e backend separati (diversi sviluppatori possono lavorare in parallelo)  
✅ T-07a può essere testato senza API key (mock API)  
✅ T-07b isolato, testabile con mock Claude API  
✅ T-07c integra tutto ma con chiara ownership  
✅ Totale: ~20 ore (vs. ~2 giorni = 16 ore, più realistico per complessità)

---

## Incremental Test Plan

### Principio
Ogni task ha:
- **Unit tests** per logica isolata
- **Integration tests** per interazioni
- **E2E tests** (selettivi) solo per flussi critici

### Coverage Targets per Layer
| Layer | Target |
|---|---|
| TypeScript Types | 100% type guards |
| Utility Functions | 95%+ |
| Store Logic | 85%+ |
| UI Components | 75%+ |
| API Endpoints | 90%+ |
| E2E Critical Paths | 4-5 flussi |

### Test Execution per Task
```bash
npm run test:unit -- --filter=T-01a
npm run test:unit -- --filter=T-01b
npm run test:unit -- --filter=T-01c
# ... per ogni task
```

### E2E Solo ai Milestone
- **T-08**: E2E Wizard completo (4 flussi principali)
- **T-12**: E2E Bot completo (4-5 comandi)

### Done Criteria per Task
Task è DONE se e solo se:
- ✅ Build pulito (0 errori)
- ✅ Tutti i test del task passano
- ✅ Coverage >= target
- ✅ No regression su test esistenti

---

## Task Dependency Graph (Updated)

```
T-00 (Setup)
  └─► T-01a (Types)
        ├─► T-01b (Validator)
        │     └─► T-02 (Store)
        │           ├─► T-03 (UI Components)
        │           │     ├─► T-04 (Step Identity)
        │           │     ├─► T-05 (Step Legs)
        │           │     ├─► T-06 (Step Filters)
        │           │     └─► T-07a (EL Editor)
        │           │           └─► T-07c (Result Panel) ◄─┐
        │           │                                       │
        │           └─► T-07c (Result Panel) ◄─────────────┤
        │                                                   │
        └─► T-01c (Defaults)                               │
              └─► T-02 (Store)                             │
                                                           │
T-00 (Setup)                                               │
  └─► T-07b (Worker API) ──────────────────────────────────┘
        └─► T-09 (Bot Setup)
              ├─► T-10 (Bot Commands)
              └─► T-11 (Bot Whitelist)

Milestone Tests:
  T-08 (E2E Wizard) ◄─ dopo T-04, T-05, T-06, T-07c
  T-12 (E2E Bot) ◄─ dopo T-10, T-11
```

---

## Files Structure (Updated)

```
docs/trading-system-docs/feature-202604-wizard-strategies-and-bot/
├── 00-DESIGN.md                           # Design originale
├── T-00-setup.md                          # Setup (invariato)
├── T-01-wizard-identity.md                # ❌ DEPRECATO → sostituito da T-01a/b/c
├── T-01a-sdf-types.md                     # ✅ NUOVO
├── T-01b-sdf-validator.md                 # ✅ NUOVO
├── T-01c-sdf-defaults.md                  # ✅ NUOVO
├── T-02-wizard-legs.md                    # Store (invariato, ma ora dipende da T-01a/b/c)
├── T-03-wizard-filters.md                 # UI Components (invariato)
├── T-04-wizard-review.md                  # Step Identity (invariato)
├── T-05-wizard-import.md                  # Step Legs (invariato)
├── T-06-wizard-el-converter.md            # ❌ DEPRECATO → sostituito da T-07a/b/c
├── T-07-wizard-publish.md                 # ❌ DEPRECATO → era EL converter
├── T-07a-el-editor-panel.md               # ✅ NUOVO
├── T-07b-worker-claude-api.md             # ✅ NUOVO
├── T-07c-conversion-result-panel.md       # ✅ NUOVO
├── T-08-wizard-e2e.md                     # E2E Wizard (invariato)
├── T-09-bot-setup.md                      # Bot setup (invariato)
├── T-10-bot-commands.md                   # Bot commands (invariato)
├── T-11-bot-whitelist.md                  # Bot whitelist (invariato)
├── T-12-bot-e2e.md                        # E2E Bot (invariato)
├── INCREMENTAL-TEST-PLAN.md               # ✅ NUOVO
└── TASK-SUBDIVISION-SUMMARY.md            # ✅ NUOVO (questo file)
```

---

## Stima Tempo (Before vs. After)

### Before Subdivision
| Task | Stima | Note |
|---|---|---|
| T-01 | ~1 giorno (8h) | Types + Validator + Defaults |
| T-07 | ~2 giorni (16h) | Editor + API + Result |
| **Totale** | **24h** | |

### After Subdivision
| Task | Stima | Note |
|---|---|---|
| T-01a | ~4h | Types only |
| T-01b | ~6h | Validator only |
| T-01c | ~4h | Defaults only |
| T-07a | ~6h | Editor only |
| T-07b | ~8h | Worker API only |
| T-07c | ~6h | Result panel only |
| **Totale** | **34h** | |

**Differenza**: +10 ore (+42%)  
**Motivo**: Stima originale sottovalutava complessità. Nuova stima è più realistica.  
**Beneficio**: Maggiore accuratezza nella pianificazione, meno rischio di blocco mid-task.

---

## Next Steps

1. **Aggiornare .agent-state.json**:
   ```json
   {
     "T-00": "done",
     "T-01a": "pending",
     "T-01b": "pending",
     "T-01c": "pending",
     "T-02": "pending",
     "T-03": "pending",
     "T-04": "pending",
     "T-05": "pending",
     "T-06": "pending",
     "T-07a": "pending",
     "T-07b": "pending",
     "T-07c": "pending",
     "T-08": "pending",
     "T-09": "pending",
     "T-10": "pending",
     "T-11": "pending",
     "T-12": "pending"
   }
   ```

2. **Copiare nuovi task in .claude/agents/**:
   ```bash
   cp docs/trading-system-docs/feature-202604-wizard-strategies-and-bot/T-01a-sdf-types.md .claude/agents/feature-202604-wizard-strategies-and-bot/
   cp docs/trading-system-docs/feature-202604-wizard-strategies-and-bot/T-01b-sdf-validator.md .claude/agents/feature-202604-wizard-strategies-and-bot/
   cp docs/trading-system-docs/feature-202604-wizard-strategies-and-bot/T-01c-sdf-defaults.md .claude/agents/feature-202604-wizard-strategies-and-bot/
   cp docs/trading-system-docs/feature-202604-wizard-strategies-and-bot/T-07a-el-editor-panel.md .claude/agents/feature-202604-wizard-strategies-and-bot/
   cp docs/trading-system-docs/feature-202604-wizard-strategies-and-bot/T-07b-worker-claude-api.md .claude/agents/feature-202604-wizard-strategies-and-bot/
   cp docs/trading-system-docs/feature-202604-wizard-strategies-and-bot/T-07c-conversion-result-panel.md .claude/agents/feature-202604-wizard-strategies-and-bot/
   ```

3. **Iniziare esecuzione da T-01a**:
   ```bash
   ./scripts/run-agents.sh feature-202604-wizard-strategies-and-bot
   ```

---

## Changelog

**2026-04-06**:
- ✅ T-01 suddiviso in T-01a, T-01b, T-01c
- ✅ T-07 suddiviso in T-07a, T-07b, T-07c
- ✅ Creato INCREMENTAL-TEST-PLAN.md
- ✅ Creato TASK-SUBDIVISION-SUMMARY.md
- ✅ README.md aggiornato con setup API keys (Telegram, Discord, Anthropic)
- ✅ Verificato graceful degradation TelegramAlerter (già implementato)

**Next**: Implementazione T-01a → T-01b → T-01c → T-02 → ...

---

**Status**: ✅ Planning completo, ready for execution
