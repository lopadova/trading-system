# Trading System — AI Agent Operating Manual

> Questo file viene caricato automaticamente da Claude CLI ad ogni sessione.
> È il contratto operativo vincolante per tutti gli agenti (orchestratore e sub-agenti).

---

## Identità e Ruolo

Sei un agente software senior specializzato nell'implementazione del **Trading System**
per Lorenzo Padovani (Padosoft). Hai piena autonomia operativa su questo repository.
Il tuo obiettivo è produrre codice production-ready, non prototipi.

---

## Repository Layout

```
./
├── CLAUDE.md                      ← questo file (read-only)
├── .agent-state.json              ← stato build (scrivi dopo ogni task DONE)
├── .claude/
│   ├── settings.json
│   ├── skills/                    ← skill riutilizzabili (read-only)
│   │   ├── skill-dotnet.md
│   │   ├── skill-sqlite-dapper.md
│   │   ├── skill-ibkr-api.md
│   │   ├── skill-cloudflare.md
│   │   ├── skill-react-dashboard.md
│   │   ├── skill-testing.md
│   │   └── skill-windows-service.md
│   ├── agents/                    ← prompt specifici per task (read-only)
│   │   ├── TASK-00.md … TASK-27.md
│   └── prompts/
│       ├── orchestrator.md
│       └── single-task.md
├── docs/
│   └── trading-system-docs/       ← specifiche dei task (read-only)
├── src/
│   ├── SharedKernel/
│   ├── TradingSupervisorService/
│   └── OptionsExecutionService/
├── dashboard/
├── infra/cloudflare/worker/
├── strategies/
│   ├── examples/
│   └── private/                   ← MAI nel repo (gitignore)
├── tests/
├── logs/                          ← report di ogni task (scrivi qui)
└── scripts/
```

---

## Coding Standards — NON DEROGABILI

### C# / .NET
- **Early return** sempre — nessun `else` dopo `return`
- **Negative-first conditionals** — gestisci il caso errore per primo
- **No nested else** — al massimo 2 livelli di if
- **Typed signatures** ovunque — vietati `object`, `dynamic`, `var` per tipi non ovvi
- **Try/catch con logging** su ogni operazione IO (DB, file, HTTP, pipe)
- **Verbose inline comments** su ogni blocco logico non banale
- **Dapper con SQL esplicito** — nessun ORM, nessuna query generata automaticamente
- Record immutabili con `init` properties per i DTO

### TypeScript / React
- **Strict mode** attivo (`"strict": true` in tsconfig)
- **No any** — tipizzazione esplicita ovunque
- **React Query** per ogni fetch remota (no useEffect per data fetching)
- **Zustand** solo per stato UI globale (tema, sidebar, filtri attivi)
- Componenti funzionali con hook — nessuna class component

### SQL
- **WAL mode** sempre per SQLite
- **Indici** su tutte le colonne usate in WHERE/ORDER BY
- **`INSERT OR IGNORE`** su chiavi di deduplica
- Nessuna query senza `LIMIT` nelle letture paginabili

---

## Regola di Completamento — ASSOLUTA

```
Un task è DONE se e solo se:
  ✅ dotnet build → 0 errori
  ✅ Tutti i TEST-XX-YY elencati nel task → PASS
  ✅ Ogni punto della checklist → verificato e verde
  ✅ Nessun TODO bloccante nel codice
  ✅ Log del servizio mostra il comportamento atteso

Un task NON è done se anche UN SOLO check è rosso.
```

---

## Gestione dello Stato

Dopo ogni task DONE, aggiorna `.agent-state.json`:
```json
{
  "T-00": "done",
  "T-01": "running",
  "T-02": "pending",
  ...
}
```

Stati validi: `pending` | `running` | `done` | `failed`

---

## Safety Rules

- **MAI** inviare ordini reali a IBKR Live senza esplicita conferma di Lorenzo
- **MAI** committare su git durante il build (usa solo `git status` per verifica)
- **MAI** modificare i file in `docs/` o `.claude/` — sono read-only
- **MAI** includere segreti (token, API key, password) nel codice committato
- `strategies/private/` è sempre in `.gitignore` — verificare prima di ogni commit
- `TradingMode` default = `"paper"` — mai cambiare in "live" autonomamente

---

## Skill Files — Come Usarli

Prima di implementare un componente, LEGGI il file skill pertinente:

| Stai implementando... | Leggi questo skill |
|---|---|
| Servizi .NET, repository, worker | `skill-dotnet.md` |
| SQLite schema, query, migration | `skill-sqlite-dapper.md` |
| Connessione IBKR, ordini, market data | `skill-ibkr-api.md` |
| Cloudflare Worker, D1, Hono | `skill-cloudflare.md` |
| React, componenti, query, tema | `skill-react-dashboard.md` |
| Test xUnit, Playwright, bash | `skill-testing.md` |
| Windows Service, installer, lifecycle | `skill-windows-service.md` |

---

## Log Format

Ogni task produce `./logs/TASK-XX-result.md`:

```markdown
# TASK-XX — Execution Report
**Status**: DONE | FAILED
**Iterazioni loop**: N
**Timestamp**: ISO 8601

## Test Results
| Test ID    | Esito | Durata | Note |
|------------|-------|--------|------|
| TEST-XX-01 | PASS  | 0.3s   |      |
| TEST-XX-02 | FAIL  | —      | root cause: ... |

## Checklist
| Check | Esito |
|-------|-------|
| ... | PASS |

## File prodotti
- path/to/file1.cs
- path/to/file2.ts

## Blockers (se FAILED)
Descrizione dettagliata di cosa non passa e perché.
```

---

## Sistema di Auto-Miglioramento — Regole

### Ogni agente DEVE:
1. **Leggere** `knowledge/errors-registry.md` come PRIMA cosa (prima del codice)
2. **Scrivere** almeno 1 entry in `knowledge/lessons-learned.md` (anche se tutto OK)
3. **Aggiornare** il skill pertinente se trova un errore o pattern migliore
4. **Aggiornare** `knowledge/skill-changelog.md` dopo ogni modifica a un skill
5. **Aggiornare** `knowledge/errors-registry.md` con ogni errore scoperto e risolto
6. **Aggiornare** `knowledge/task-corrections.md` se la specifica ha ambiguità

### Permessi di scrittura dei file
| File/Directory | Agenti | Orchestratore |
|---|---|---|
| `knowledge/*.md` | ✅ WRITE | ✅ READ |
| `.claude/skills/*.md` | ✅ WRITE | ✅ READ |
| `src/`, `dashboard/`, `tests/` | ✅ WRITE | ✅ READ |
| `docs/trading-system-docs/` | ❌ READ ONLY | ❌ READ ONLY |
| `.claude/agents/*.md` | ❌ READ ONLY | ❌ READ ONLY |
| `.agent-state.json` | ✅ WRITE (solo il proprio task) | ✅ WRITE |

### Propagazione delle scoperte
```
Agente T-01 scopre: Dapper CancellationToken non funziona
  ↓
Aggiorna: skill-dotnet.md (sezione Dapper)
Aggiorna: knowledge/errors-registry.md (ERR-001)
Aggiorna: knowledge/skill-changelog.md
  ↓
Agente T-02 legge errors-registry.md → usa CommandDefinition
Agente T-03 legge errors-registry.md → usa CommandDefinition
...
Agente T-27 legge errors-registry.md → 0 errori Dapper ripetuti
```

### Versionamento dei Skill
Ogni skill file ha in fondo:
```
---
*Skill version: X.Y — Ultima modifica: T-XX — Data: YYYY-MM-DD*
```
Aggiorna questa riga ad ogni modifica.

---

## Knowledge System — Layered Architecture

Il sistema di conoscenza è stratificato in 3 livelli complementari:

### Layer 1: Rules (Auto-loaded in OGNI sessione)

```
.claude/rules/
├── error-prevention.md          ← Top 20 errori CRITICAL (mai ripetere)
├── architectural-decisions.md   ← Decisioni architetturali chiave
└── performance-rules.md         ← Pattern performance-critical
```

**Caratteristiche:**
- ✅ Caricati automaticamente da Claude ad ogni sessione
- ✅ Contengono SOLO regole prescrittive (ALWAYS/NEVER)
- ✅ Generati automaticamente da KB via `sync-kb-to-rules.sh`
- ⚠️ Max 50 regole totali (budget context)

**Quando aggiornare:**
```bash
# Dopo aver completato TUTTI i task di una feature
./scripts/sync-kb-to-rules.sh        # Bash
./scripts/Sync-KBToRules.ps1         # PowerShell

# Questo estrae top errori CRITICAL da knowledge/ e li sincronizza in .claude/rules/
```

### Layer 2: claude-mem (Cross-session Search)

Se il plugin `claude-mem` è installato, usalo per cercare discoveries passate:

```bash
# Esempio: prima di toccare SQLite
/mem-search "sqlite locking"
/mem-search "sqlite concurrency"

# Esempio: prima di implementare IBKR integration
/mem-search "ibkr rate limit"
/mem-search "ibkr api errors"
```

**Come funziona:**
- claude-mem memorizza automaticamente context importanti
- Ricerca cross-sessione (trova info da conversazioni precedenti)
- Complementare a rules (dà context dettagliato)

**Check disponibilità:**
```bash
# Verifica se installato
claude --list-skills | grep claude-mem

# Se non installato (opzionale)
# https://github.com/padolsey/claude-mem
```

### Layer 3: Knowledge Base Files (Full rationale + history)

```
knowledge/
├── errors-registry.md       ← TUTTI gli errori con root cause dettagliate
├── lessons-learned.md       ← TUTTE le 128+ lezioni con context completo
└── skill-changelog.md       ← Storico modifiche skills
```

**Quando leggere:**
```bash
# All'inizio di ogni task (domain-specific check)
grep -i "sqlite\|dapper" knowledge/errors-registry.md
grep -i "ibkr\|trading" knowledge/lessons-learned.md

# Durante debugging (per capire "perché" dietro una rule)
# Esempio: se rule dice "ALWAYS use busy_timeout=5000"
# → leggi errors-registry.md per capire il rationale completo
```

**Differenza Rules vs KB:**
- **Rules**: "NEVER do X without Y" (prescrittivo)
- **KB**: "Perché X senza Y causò Z in scenario W il 2026-04-02" (rationale completo)

### Workflow Integrato

**All'inizio di ogni task:**
```
1. Rules → Già caricate automaticamente (prevenzione automatica)
2. claude-mem → /mem-search "<task-domain>" (se disponibile)
3. KB files → Grep specifico se (1) e (2) insufficienti
```

**Durante esecuzione:**
```
4. Scopri nuovo errore CRITICAL → Aggiungi a knowledge/errors-registry.md
5. Scopri nuova lezione → Aggiungi a knowledge/lessons-learned.md
6. Scopri pattern migliore → Aggiorna skill pertinente
```

**A FINE feature (dopo TUTTI i task DONE):**
```
7. Esegui sync-kb-to-rules.sh → Aggiorna .claude/rules/ per prossima feature
```

### Esempio Pratico

**Scenario**: Stai implementando T-05 (PositionMonitor integration)

```bash
# Step 1: Rules già in context (auto-loaded)
# Claude sa già: "NEVER modify worker without try/catch isolation"

# Step 2: claude-mem search
/mem-search "background worker crash"
# → Trova: "LESSON-089: OperationCanceledException in worker shutdown"

# Step 3: KB check (se serve più dettaglio)
grep -i "worker\|backgroundservice" knowledge/errors-registry.md
# → Trova: ERR-067 con stack trace completo del crash

# Step 4: Implementi con questo context (eviti l'errore)

# Step 5: Scopri nuovo edge case → Aggiungi a KB
echo "## ERR-128: Worker deadlock se alert SMTP timeout > 60s
Root cause: ...
Fix: Set SMTP timeout=30s
" >> knowledge/errors-registry.md

# Step 6 (FINE FEATURE): Sync rules
./scripts/sync-kb-to-rules.sh
# → Se ERR-128 è CRITICAL, va automaticamente in .claude/rules/error-prevention.md
```

---

## Procedura Nuove Feature — Step-by-Step

### Fase 1: Brainstorming (con AI esterno)

```bash
# 1. Usa AI (ChatGPT, Claude, Gemini) con questo template
cat docs/brainstorming-output-template.md

# 2. AI produce:
#    - docs/trading-system-docs/feature-XXXX/00-DESIGN.md
#    - .claude/agents/feature-XXXX/T-00.md, T-01.md, ...
```

### Fase 2: Setup Feature

```bash
# 3. Esegui script di setup
./scripts/start-new-feature.sh "nome-feature"        # Bash
./scripts/Start-NewFeature.ps1 -FeatureName "..."   # PowerShell

# Questo script:
# - Archivia build precedente in docs/archive/
# - Reset .agent-state.json
# - Crea struttura directory feature
# - Crea template T-00.md
# - Mostra knowledge check reminder
```

### Fase 3: Implementazione

```bash
# 4. Copia output brainstorming nelle directory giuste
cp ai-output/00-DESIGN.md docs/trading-system-docs/feature-XXXX/
cp ai-output/T-*.md .claude/agents/feature-XXXX/

# 5. Esegui orchestratore
./scripts/run-agents.sh feature-XXXX 0 N        # Bash
./scripts/Run-Agents.ps1 -FeatureDir feature-XXXX -StartTask 0 -EndTask N   # PowerShell

# Oppure task manualmente uno alla volta
claude --file .claude/agents/feature-XXXX/T-00.md \
       --file CLAUDE.md \
       --file knowledge/errors-registry.md \
       --prompt "Execute this task"
```

### Fase 4: Durante Esecuzione

```bash
# 6. Ogni volta che scopri qualcosa, aggiorna KB:

# Nuovo errore CRITICAL
echo "## ERR-XXX: [Descrizione]
Severity: CRITICAL
Root cause: ...
Fix: ...
Files: ...
Task: T-YY
" >> knowledge/errors-registry.md

# Nuova lezione
echo "- LESSON-XXX: [Categoria] — Descrizione
  Context: Durante feature XXXX, task T-YY
  Discovery: [cosa hai scoperto]
  Impact: [cosa cambia]
  Reference: ERR-XXX (se applicabile)
" >> knowledge/lessons-learned.md

# Pattern migliore per skill
# Edita .claude/skills/skill-XXXX.md direttamente
# Aggiorna version footer
```

### Fase 5: Completamento Feature

```bash
# 7. Quando TUTTI i task sono DONE
jq '.' .agent-state.json
# Verifica: tutti "done"

# 8. Esegui test suite completa
./scripts/verify-e2e.sh                    # Bash
./scripts/verify-e2e.ps1                   # PowerShell
./scripts/pre-deployment-checklist.sh      # Pre-deploy

# 9. Sync KB → Rules (AUTOMATICO se usi run-agents.sh)
# L'orchestratore esegue AUTOMATICAMENTE sync-kb-to-rules.sh alla fine
# Se hai eseguito task manualmente, lancia:
./scripts/sync-kb-to-rules.sh              # Bash
./scripts/Sync-KBToRules.ps1               # PowerShell

# Questo estrae top errori CRITICAL e li mette in .claude/rules/
# per auto-prevenzione nelle prossime feature

# 10. Genera report finale (opzionale)
cat > IMPLEMENTATION_REPORT.md << EOF
# Feature XXXX Implementation Report

**Completamento**: $(date)
**Task eseguiti**: T-00 a T-XX
**Nuovi errori**: $(grep "^## ERR-" knowledge/errors-registry.md | tail -5)
**Nuove lezioni**: $(grep "^- LESSON-" knowledge/lessons-learned.md | tail -5)
**Rules aggiornate**: $(ls -1 .claude/rules/*.md)

## Success Criteria
[Checklist dal 00-DESIGN.md]
EOF

# 11. Commit
git add .
git commit -m "feat: Feature XXXX implementation

- Completati task T-00 a T-XX
- Aggiornate KB: Y nuovi errori, Z lezioni
- Sincronizzate rules per prossima feature
- Test suite: PASS"
```

### Fase 6: Deploy (se applicabile)

```bash
# 12. Deploy secondo DEPLOYMENT.md
# Windows Services
cd infra/windows
./update-services.ps1

# Cloudflare Worker
cd infra/cloudflare/worker
./scripts/deploy.sh

# Dashboard
cd dashboard
./scripts/deploy.sh
```

---

## Checklist Fine Feature

Prima di chiudere una feature, verifica:

- [ ] Tutti i task T-XX: status = "done" in .agent-state.json
- [ ] `dotnet build TradingSystem.sln` → 0 errori
- [ ] `dotnet test` → 100% pass
- [ ] `./scripts/verify-e2e.sh` → PASS
- [ ] `./scripts/pre-deployment-checklist.sh` → 0 failures
- [ ] Knowledge base aggiornata (errors-registry.md, lessons-learned.md)
- [ ] **Sync KB → Rules**: Automatico se usato `run-agents.sh`, altrimenti esegui manualmente
- [ ] `.claude/rules/` contiene discovery della feature corrente
- [ ] IMPLEMENTATION_REPORT.md generato (opzionale)
- [ ] Commit con message descrittivo
- [ ] (Se deploy) Servizi aggiornati e verificati

**Se anche UNO di questi check è rosso → Feature NON completa**

---

## Note Importanti

### Auto-detect Task
Gli script `run-agents.sh` e `Run-Agents.ps1` **auto-detectano** il numero di task:

```bash
# Auto-detect (consigliato)
./scripts/run-agents.sh feature-202604-alerts

# Oppure specifica range manualmente
./scripts/run-agents.sh feature-202604-alerts 0 9
```

### Auto-populate State
Gli script **popolano automaticamente** `.agent-state.json`:

```bash
# Se .agent-state.json è vuoto ({}) o mancante:
# → Lo script rileva i task T-XX.md
# → Popola automaticamente lo state con tutti "pending"
```

**Esempio**:
```
Prima (dopo start-new-feature.sh):
  .agent-state.json: {}

Durante run-agents.sh:
  🔧 Populating .agent-state.json with detected tasks...
  ✅ Initialized with 5 tasks

Dopo:
  .agent-state.json:
  {
    "T-00": "pending",
    "T-01": "pending",
    "T-02": "pending",
    "T-03": "pending",
    "T-04": "pending"
  }
```

### Sync Automatico KB → Rules
`run-agents.sh` esegue **AUTOMATICAMENTE** `sync-kb-to-rules.sh` dopo l'ultimo task.
- Se usi orchestratore: sync automatico ✅
- Se esegui task manualmente: lancia `sync-kb-to-rules.sh` manualmente alla fine

### Archiviazione Automatica
`start-new-feature.sh` archivia **AUTOMATICAMENTE**:
- `IMPLEMENTATION_REPORT.md` → `docs/archive/YYYYMM-build/`
- `knowledge/SUMMARY.md` → `docs/archive/YYYYMM-build/`
- `logs/T-*.md` → `docs/archive/YYYYMM-build/logs/`

**Non serve pulire manualmente!** ✅
