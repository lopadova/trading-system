---
title: "Changelog — Scripts & Workflow"
tags: ["dev", "workflow", "reference"]
aliases: ["Scripts Changelog"]
status: reference
audience: ["developer"]
last-reviewed: "2026-04-21"
---

# Changelog — Scripts & Workflow

Documenta le migliorie apportate agli script di automazione e workflow.

---

## [1.1.0] - 2026-04-06

### 🎉 Features Aggiunte

#### 1. Auto-Detect Task Count

**Prima**:
```bash
# Dovevi contare manualmente i task
./scripts/run-agents.sh feature-202604-alerts 0 9
#                                              ↑ ↑
#                                         manual count
```

**Adesso**:
```bash
# Auto-detect automatico!
./scripts/run-agents.sh feature-202604-alerts
# → Lo script conta automaticamente i file T-XX.md
```

**Implementazione**:
- `scripts/run-agents.sh`: Aggiunto auto-detect con `find` e `sed`
- `scripts/Run-Agents.ps1`: Aggiunto auto-detect con `Get-ChildItem`

**Benefici**:
- ✅ Zero errori di conteggio
- ✅ Workflow più veloce (1 parametro invece di 3)
- ✅ Compatibilità backwards (range manuale ancora supportato)

---

#### 2. Auto-Populate .agent-state.json

**Prima**:
```bash
# start-new-feature.sh resettava a {}
# Ma {} non conteneva i task → errore se Claude controllava
```

**Adesso**:
```bash
# run-agents.sh popola automaticamente lo state
🔧 Populating .agent-state.json with detected tasks...
✅ Initialized with 5 tasks

# Risultato:
{
  "T-00": "pending",
  "T-01": "pending",
  "T-02": "pending",
  "T-03": "pending",
  "T-04": "pending"
}
```

**Implementazione**:
- Controllo se `.agent-state.json` è vuoto o mancante
- Genera JSON con tutti i task rilevati impostati a "pending"
- Versione senza dipendenza da `jq` (maggiore compatibilità)

**Benefici**:
- ✅ State sempre coerente con i task presenti
- ✅ Nessuna gestione manuale richiesta
- ✅ Funziona anche se copi task dopo start-new-feature

---

#### 3. Sync Automatico KB → Rules

**Prima**:
```bash
# Alla fine di tutti i task dovevi ricordarti:
./scripts/sync-kb-to-rules.sh
# Facile dimenticarlo! ⚠️
```

**Adesso**:
```bash
# Dopo ultimo task, automaticamente:

=========================================
✅ All Tasks Completed Successfully!
=========================================

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔄 Step: Sync Knowledge Base → Rules
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Running: ./scripts/sync-kb-to-rules.sh
✅ Rules updated in .claude/rules/
```

**Implementazione**:
- `run-agents.sh`: Aggiunta sezione finale che chiama `sync-kb-to-rules.sh`
- `Run-Agents.ps1`: Aggiunta sezione finale che chiama `Sync-KBToRules.ps1`

**Benefici**:
- ✅ Zero dimenticanze (automatico)
- ✅ Rules sempre aggiornate per prossima feature
- ✅ Knowledge propagata tra feature

---

#### 4. Archiviazione Automatica Build Precedente

**Sempre fatto da**:
- `start-new-feature.sh`
- `Start-NewFeature.ps1`

**Cosa archivia**:
```
docs/archive/YYYYMM-build/
├── IMPLEMENTATION_REPORT.md
├── SUMMARY.md
├── task-corrections.md
└── logs/
    ├── T-00-result.md
    ├── T-01-result.md
    └── ...
```

**Benefici**:
- ✅ Storia completa di ogni build
- ✅ Nessuna perdita di dati
- ✅ Workspace pulito per nuova feature

---

### 📚 Documentazione Aggiunta

#### Nuovi File

| File | Scopo |
|---|---|
| `docs/FAQ-WORKFLOW.md` | FAQ dettagliate workflow |
| `docs/QUICK-START-NUOVA-FEATURE.md` | Quick reference (già esistente, aggiornato) |
| `docs/WORKFLOW-NUOVE-FEATURE.md` | Guida completa (già esistente, aggiornato) |
| `docs/BRAINSTORMING-PROMPT-TEMPLATE.md` | Template prompt AI (già esistente) |
| `docs/brainstorming-output-template.md` | Guida output AI (già esistente) |

#### File Aggiornati

| File | Modifiche |
|---|---|
| `README.md` | Aggiunta sezione "Development" con nuove guide |
| `CLAUDE.md` | Sezione "Note Importanti" espansa con auto-detect/populate/sync |
| `docs/QUICK-START-NUOVA-FEATURE.md` | STEP 4 e 5 aggiornati (auto-detect, sync automatico) |
| `docs/WORKFLOW-NUOVE-FEATURE.md` | STEP 4.2 e 5.3 aggiornati |

---

### 🧪 Test Eseguiti

#### Test 1: sync-kb-to-rules.sh

```bash
# Genera .claude/rules/ da knowledge/
./scripts/sync-kb-to-rules.sh

# Risultato:
.claude/rules/
├── error-prevention.md ✅
├── architectural-decisions.md ✅
└── performance-rules.md ✅
```

**Status**: ✅ PASSED

---

#### Test 2: start-new-feature.sh

```bash
./scripts/start-new-feature.sh "test-auto-detect"

# Risultato:
docs/trading-system-docs/feature-202604-test-auto-detect/ ✅
.claude/agents/feature-202604-test-auto-detect/ ✅
.agent-state.json reset a {} ✅
```

**Status**: ✅ PASSED

---

#### Test 3: Auto-Detect Logic

```bash
# Con 4 task (T-00, T-01, T-02, T-03)
# Output:
✅ Found 4 tasks (T-00 to T-03)
```

**Status**: ✅ PASSED

---

#### Test 4: Auto-Populate State

```bash
# Prima: .agent-state.json = {}
# Dopo run-agents.sh:
{
  "T-00": "pending",
  "T-01": "pending",
  "T-02": "pending",
  "T-03": "pending"
}
```

**Status**: ✅ PASSED

---

#### Test 5: PowerShell Compatibility

```bash
pwsh -File ./scripts/Start-NewFeature.ps1 -FeatureName "test-powershell"
# Risultato: Feature creata correttamente ✅
```

**Status**: ✅ PASSED

---

### 📊 Metriche di Miglioramento

| Metrica | Prima | Dopo | Miglioramento |
|---|---|---|---|
| **Comandi utente** | 6 step | 4 step | -33% |
| **Parametri manuali** | 3 (dir, start, end) | 1 (dir) | -66% |
| **Rischio errore conteggio** | Alto | Zero | -100% |
| **Rischio dimenticare sync** | Alto | Zero | -100% |
| **Gestione manuale state** | Richiesta | Automatica | -100% |

---

### 🔧 Breaking Changes

**NESSUNO!**

Tutte le modifiche sono **backwards compatible**:

```bash
# OLD SYNTAX (ancora funziona)
./scripts/run-agents.sh feature-202604-alerts 0 9

# NEW SYNTAX (consigliato)
./scripts/run-agents.sh feature-202604-alerts
```

---

### 🚀 Prossimi Miglioramenti

#### Idee per v1.2.0

1. **Pre-flight check**:
   - Verifica che tutti i file T-XX.md siano validi markdown
   - Verifica che 00-DESIGN.md esista prima di run

2. **Rollback automatico**:
   - Se task fallisce, opzione per rollback automatico

3. **Parallel task execution**:
   - Esegui task indipendenti in parallelo (se supportato da dependencygrafo)

4. **Task templates generator**:
   - Script per generare template T-XX.md da checklist

5. **Integration con GitHub Actions**:
   - CI/CD automatico per feature branches

---

## [1.0.0] - 2026-04-05

### Initial Release

- Sistema di orchestrazione task
- Knowledge base system
- Skills e rules management
- Scripts bash e PowerShell
- Documentazione completa

---

**Maintainer**: Trading System Team  
**Contact**: lorenzo.padovani@padosoft.com
