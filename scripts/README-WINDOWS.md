---
title: "Scripts per Windows"
tags: ["dev", "reference", "workflow"]
aliases: ["scripts-windows"]
status: current
audience: ["developer"]
last-reviewed: "2026-04-21"
related:
  - "[[Trading System Scripts|Scripts README]]"
---

# Scripts per Windows

Questo repository include script PowerShell (`.ps1`) equivalenti agli script Bash (`.sh`) per l'uso su Windows.

## Prerequisiti

- **PowerShell 5.1+** (incluso in Windows 10/11)
- **Python 3.x** installato e disponibile nel PATH
- **Claude CLI** installato ([claude.ai/code](https://claude.ai/code))

## Script Disponibili

### 1. run-agents.ps1 — Launcher Multi-Agent

Avvia l'orchestratore o singoli task agents.

```powershell
# Full build (tutti i 28 task in wave)
.\scripts\run-agents.ps1

# Riparti da T-09 (dopo un'interruzione)
.\scripts\run-agents.ps1 -From T-09

# Esegui solo un task specifico
.\scripts\run-agents.ps1 -Task T-14
```

**Parametri:**
- `-From T-XX`: Riprendi da un task specifico (marca i precedenti come done)
- `-Task T-XX`: Esegui solo un singolo task

**Variabili d'ambiente:**
- `ORCHESTRATOR_MODEL`: Override del modello per l'orchestratore (default: `claude-opus-4-5`)
- `WORKER_MODEL`: Override del modello per i worker agents (default: `claude-sonnet-4-5`)

---

### 2. check-knowledge.ps1 — Status Knowledge Base

Mostra statistiche sulla knowledge base accumulata dagli agenti.

```powershell
.\scripts\check-knowledge.ps1
```

Output:
- Numero di errori registrati (`errors-registry.md`)
- Numero di lezioni apprese (`lessons-learned.md`)
- Numero di aggiornamenti skill (`skill-changelog.md`)
- Numero di correzioni task (`task-corrections.md`)
- Stato attuale dei 28 task (done/running/failed/pending)

---

### 3. reset-task.ps1 — Reset Task

Resetta un task a `pending` per ri-eseguirlo (utile dopo un fallimento).

```powershell
.\scripts\reset-task.ps1 T-07
```

Questo script:
- Cambia lo stato del task in `.agent-state.json` a `pending`
- Archivia il report precedente (`logs/T-07-result.md` → `logs/T-07-result.YYYYMMDDHHMMSS.bak.md`)

---

## Execution Policy

Se PowerShell blocca l'esecuzione degli script con errore:

```
cannot be loaded because running scripts is disabled on this system
```

Esegui come amministratore:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Oppure, per una singola sessione (non persistente):

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
```

---

## Differenze rispetto agli script .sh

| Script Bash | Script PowerShell | Note |
|---|---|---|
| `run-agents.sh` | `run-agents.ps1` | Logica identica |
| `check-knowledge.sh` | `check-knowledge.ps1` | Logica identica |
| `reset-task.sh` | `reset-task.ps1` | Logica identica |

Gli script `.sh` rimangono disponibili per ambienti Unix/Linux/WSL.

---

## Troubleshooting

### Python non trovato

Se lo script fallisce con `python3: command not found`:

1. Verifica che Python sia installato: `python --version`
2. Se Python è installato come `python` invece di `python3`, puoi:
   - Creare un alias: `Set-Alias python3 python` (solo per la sessione corrente)
   - Oppure modificare gli script per usare `python` invece di `python3`

### Claude CLI non trovato

Se `claude` non è riconosciuto:

1. Installa Claude CLI da [claude.ai/code](https://claude.ai/code)
2. Verifica installazione: `claude --version`
3. Aggiungi al PATH se necessario

### Encoding UTF-8

Gli script PowerShell gestiscono automaticamente UTF-8 per i file di configurazione e log.

---

## File Generati

Gli script creano automaticamente:

```
./
├── .agent-state.json              ← stato task (pending/running/done/failed)
├── logs/
│   ├── orchestrator-TIMESTAMP.log ← log orchestratore
│   └── T-XX-TIMESTAMP.log         ← log task specifici
└── knowledge/
    ├── errors-registry.md         ← errori scoperti e risolti
    ├── lessons-learned.md         ← lezioni apprese
    ├── skill-changelog.md         ← modifiche agli skill
    └── task-corrections.md        ← correzioni alle specifiche
```

---

## Esempi di Uso

### Scenario 1: Primo build completo

```powershell
# Assicurati di essere nella root del progetto
cd C:\Users\lopad\Documents\DocLore\Visual Basic\_NET\Applicazioni\trading-system

# Lancia il build completo
.\scripts\run-agents.ps1
```

### Scenario 2: Un task è fallito (T-07)

```powershell
# Controlla lo stato
.\scripts\check-knowledge.ps1

# Output: T-07 risulta "failed"

# Leggi il report del fallimento
Get-Content .\logs\T-07-result.md

# Correggi manualmente il problema, poi resetta il task
.\scripts\reset-task.ps1 T-07

# Ri-esegui da T-07 in poi
.\scripts\run-agents.ps1 -From T-07
```

### Scenario 3: Esegui solo T-14 per testare

```powershell
.\scripts\run-agents.ps1 -Task T-14

# Controlla il log
Get-Content .\logs\T-14-*.log | Select-Object -Last 50
```

---

## Script di Verifica (verify-*.ps1)

Alcuni task richiedono la creazione di script di verifica specifici (es. `verify-e2e.ps1`, `verify-task.ps1`).

Quando un agent genera uno script `.sh` di verifica:

1. Usa `_template-verify.ps1` come base per la versione PowerShell
2. Converti la logica bash in PowerShell seguendo i pattern degli script esistenti
3. Salva entrambe le versioni (`.sh` per Unix, `.ps1` per Windows)

Il template include helper comuni come:
- `Write-TestResult` — formattazione output test (PASS/FAIL/SKIP)
- `Invoke-DotNetTest` — esecuzione test xUnit con filtri
- Gestione errori e exit codes

---

## Note Importanti

- **Non modificare** `.agent-state.json` manualmente (usa `reset-task.ps1`)
- I log sono **append-only** — vecchi log non vengono sovrascritti
- La knowledge base (`knowledge/*.md`) è **write-shared** tra tutti gli agenti
- Gli script **NON** committano su git automaticamente (usa `git status` per verificare)
- **Per nuovi script di verifica**: crea sempre entrambe le versioni (`.sh` + `.ps1`)
