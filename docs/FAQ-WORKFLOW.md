# FAQ — Workflow Nuove Feature

> Domande frequenti sul workflow di sviluppo nuove feature

---

## ❓ Chi fa archiviazione e reset `.agent-state.json`?

### Risposta Breve

**`start-new-feature.sh`** (o `.ps1`) fa TUTTO automaticamente:
- ✅ Archivia build precedente
- ✅ Reset `.agent-state.json` a `{}`
- ✅ Crea directory nuova feature

**`run-agents.sh`** (o `.ps1`) popola automaticamente lo state:
- ✅ Rileva i task T-XX.md copiati
- ✅ Popola `.agent-state.json` con tutti i task "pending"

### Risposta Dettagliata

#### STEP 1: `start-new-feature.sh` — Preparazione

```bash
./scripts/start-new-feature.sh "alert-system"
```

**Cosa fa**:

1. **Archivia build precedente**:
   ```
   docs/archive/YYYYMM-build/
   ├── IMPLEMENTATION_REPORT.md       ← dalla root
   ├── SUMMARY.md                     ← da knowledge/
   ├── task-corrections.md            ← da knowledge/
   └── logs/
       ├── T-00-result.md
       ├── T-01-result.md
       └── ...
   ```

2. **Reset `.agent-state.json`**:
   ```json
   {}
   ```
   *(Vuoto perché non sa ancora quanti task ci saranno)*

3. **Crea directory nuova feature**:
   ```
   docs/trading-system-docs/feature-202604-alert-system/
   └── 00-DESIGN.md (template vuoto)

   .claude/agents/feature-202604-alert-system/
   └── T-00-setup.md (template)
   ```

#### STEP 2: Tu copi i file dall'AI

```bash
# Copia design doc
cp /tmp/feature-analysis/00-DESIGN.md \
   docs/trading-system-docs/feature-202604-alert-system/

# Copia task files
cp /tmp/feature-analysis/T-*.md \
   .claude/agents/feature-202604-alert-system/
```

**Risultato**:
```
.claude/agents/feature-202604-alert-system/
├── T-00-setup.md       ← sovrascrive template
├── T-01-impl.md        ← dall'AI
├── T-02-test.md        ← dall'AI
└── T-03-verify.md      ← dall'AI
```

#### STEP 3: `run-agents.sh` — Auto-populate

```bash
./scripts/run-agents.sh feature-202604-alert-system
```

**Cosa fa**:

1. **Rileva i task**:
   ```
   🔍 Auto-detecting tasks...
   ✅ Found 4 tasks (T-00 to T-03)
   ```

2. **Popola `.agent-state.json` automaticamente**:
   ```
   🔧 Populating .agent-state.json with detected tasks...
   ✅ Initialized with 4 tasks
   ```

   **Risultato**:
   ```json
   {
     "T-00": "pending",
     "T-01": "pending",
     "T-02": "pending",
     "T-03": "pending"
   }
   ```

3. **Esegue i task in sequenza**
4. **Sync automatico KB → Rules** alla fine

### Quindi in Sintesi

| Step | Chi | Cosa fa |
|---|---|---|
| 1. Setup | `start-new-feature.sh` | Archivia vecchi file, reset state a `{}`, crea directory |
| 2. Copy | **Tu manualmente** | Copi 00-DESIGN.md e T-XX.md dall'AI |
| 3. Run | `run-agents.sh` | Auto-popola state con task detectati, esegue task, sync KB→Rules |

**Non devi fare nulla manualmente per state/archiviazione!** ✅

---

## ❓ Dove metto i file generati dall'AI?

### Risposta Breve

**LOCATION PREFERITA** (consigliata, più semplice):

```bash
FEATURE_DIR="feature-202604-nome"  # Dal comando start-new-feature

# TUTTI i file nello stesso posto (docs/)
00-DESIGN.md → docs/trading-system-docs/$FEATURE_DIR/00-DESIGN.md
T-*.md       → docs/trading-system-docs/$FEATURE_DIR/T-*.md

# ✅ Più semplice: tutto in una directory!
```

**LOCATION ALTERNATIVA** (legacy, ancora supportata):

```bash
# Design in docs/, task in .claude/
00-DESIGN.md → docs/trading-system-docs/$FEATURE_DIR/00-DESIGN.md
T-*.md       → .claude/agents/$FEATURE_DIR/T-*.md

# ⚠️ Funziona ma meno comodo (due directory diverse)
```

**`run-agents.sh` prova automaticamente entrambe le location** (docs/ ha priorità).

### Risposta Dettagliata

#### Output dell'AI (da brainstorming)

L'AI produce questi file:
```
/tmp/feature-analysis/
├── 00-DESIGN.md
├── T-00-setup.md
├── T-01-database.md
├── T-02-logic.md
├── T-03-ui.md
└── T-04-test.md
```

#### Dove copiarli

**DOPO** aver eseguito `start-new-feature.sh "nome-feature"`:

```bash
# 1. Identifica FEATURE_DIR dall'output dello script
# Output: "Feature: feature-202604-nome"
FEATURE_DIR="feature-202604-nome"

# 2. Copia design document
cp /tmp/feature-analysis/00-DESIGN.md \
   docs/trading-system-docs/$FEATURE_DIR/00-DESIGN.md

# 3. Copia TUTTI i task files
cp /tmp/feature-analysis/T-*.md \
   .claude/agents/$FEATURE_DIR/
```

**Risultato finale**:
```
trading-system/
├── docs/
│   └── trading-system-docs/
│       └── feature-202604-nome/
│           └── 00-DESIGN.md ✅
│
└── .claude/
    └── agents/
        └── feature-202604-nome/
            ├── T-00-setup.md ✅
            ├── T-01-database.md ✅
            ├── T-02-logic.md ✅
            ├── T-03-ui.md ✅
            └── T-04-test.md ✅
```

### Checklist Verifica

Dopo aver copiato i file:

```bash
# Verifica design doc
ls -la docs/trading-system-docs/$FEATURE_DIR/
# Expected: 00-DESIGN.md

# Verifica task files
ls -la .claude/agents/$FEATURE_DIR/
# Expected: T-00.md, T-01.md, T-02.md, ...

# Conta task
ls .claude/agents/$FEATURE_DIR/T-*.md | wc -l
# Expected: numero task (es: 5)
```

**Se vedi i file → sei pronto per `run-agents.sh`!** ✅

---

## ❓ Posso modificare `.agent-state.json` manualmente?

### Risposta Breve

**SÌ**, ma **NON è necessario**.

`run-agents.sh` lo popola automaticamente al primo avvio.

### Quando modificare manualmente

**Scenario 1**: Riprendere da task specifico (es: T-05 fallito)

```json
{
  "T-00": "done",
  "T-01": "done",
  "T-02": "done",
  "T-03": "done",
  "T-04": "done",
  "T-05": "failed",  ← Cambia a "pending" per re-run
  "T-06": "pending",
  "T-07": "pending"
}
```

**Scenario 2**: Skipare task (NON consigliato)

```json
{
  "T-00": "done",
  "T-01": "done",
  "T-02": "skip",    ← Marco come done per saltare
  "T-03": "pending"
}
```

### Stati Validi

| Stato | Significato |
|---|---|
| `"pending"` | Task non ancora eseguito |
| `"running"` | Task in esecuzione (temporaneo) |
| `"done"` | Task completato con successo |
| `"failed"` | Task fallito (blocca orchestratore) |

---

## ❓ Cosa succede se aggiungo task dopo aver iniziato?

### Risposta

**Scenario**: Hai già eseguito T-00 a T-03, poi aggiungi T-04.md

**Soluzione**:

```bash
# 1. Aggiungi il nuovo task file
cp new-task.md .claude/agents/feature-202604-nome/T-04-new.md

# 2. Aggiungi entry in .agent-state.json
# Edita manualmente o usa:
jq '. + {"T-04": "pending"}' .agent-state.json > tmp && mv tmp .agent-state.json

# 3. Esegui solo il nuovo task
./scripts/run-agents.sh feature-202604-nome 4 4
```

**Oppure** (più semplice):

```bash
# Esegui Claude manualmente sul task
claude --file .claude/agents/feature-202604-nome/T-04-new.md \
       --file CLAUDE.md \
       --prompt "Execute this task"
```

---

## ❓ Posso eseguire task in ordine diverso?

### Risposta Breve

**SÌ**, ma **NON consigliato** (dipendenze tra task).

### Come fare

**Task singolo**:
```bash
# Esegui solo T-05
./scripts/run-agents.sh feature-202604-nome 5 5
```

**Range custom**:
```bash
# Esegui solo T-03 a T-07
./scripts/run-agents.sh feature-202604-nome 3 7
```

**Attenzione**: Task spesso hanno dipendenze:
- T-01 crea DB schema → T-02 usa il DB
- T-03 implementa logic → T-04 testa logic

**Eseguire out-of-order può causare fallimenti!**

---

## ❓ `sync-kb-to-rules.sh` è automatico?

### Risposta Breve

**SÌ**, se usi `run-agents.sh`.  
**NO**, se esegui task manualmente.

### Dettagli

**Con orchestratore** (automatico ✅):
```bash
./scripts/run-agents.sh feature-202604-nome
# ...esegue tutti i task...
# ✅ Alla fine: esegue sync-kb-to-rules.sh automaticamente
```

**Senza orchestratore** (manuale ⚠️):
```bash
# Esegui task uno alla volta
claude --file .claude/agents/.../T-00.md
claude --file .claude/agents/.../T-01.md
# ...

# DEVI eseguire sync manualmente
./scripts/sync-kb-to-rules.sh
```

### Quando viene eseguito

```
Task T-00 → DONE
Task T-01 → DONE
Task T-02 → DONE
Task T-03 → DONE (ultimo)
    ↓
=========================================
✅ All Tasks Completed Successfully!
=========================================
    ↓
🔄 Automatic Sync: KB → Rules
    ↓
./scripts/sync-kb-to-rules.sh
    ↓
✅ Rules updated in .claude/rules/
```

**È l'ultimo step automatico dell'orchestratore**.

---

## ❓ Devo cancellare `.claude/rules/` manualmente?

### Risposta Breve

**NO, mai!**

`.claude/rules/` contiene la "memoria" del sistema.  
Cancellare = perdere tutte le lezioni apprese.

### Come funziona

**Build 1** (feature alerts):
```bash
# Scopri: ERR-050 (SQLite timeout issue)
# Aggiungi a: knowledge/errors-registry.md

# Sync crea:
.claude/rules/error-prevention.md
  → "ALWAYS set busy_timeout=5000"
```

**Build 2** (feature monitoring):
```bash
# Claude carica automaticamente:
.claude/rules/error-prevention.md

# → Evita ERR-050 automaticamente! ✅
```

**Se cancelli `.claude/rules/`**:
```bash
# Build 2 NON sa di ERR-050
# → Rischio di ripetere lo stesso errore ❌
```

### Aggiornamento Rules

**Automatico** dopo ogni feature:
```bash
./scripts/run-agents.sh feature-202604-new
# → Sync automatico aggiorna .claude/rules/
```

**Manuale** se necessario:
```bash
# Dopo aver aggiunto errori in knowledge/
./scripts/sync-kb-to-rules.sh
```

---

## 📚 Quick Reference

### Workflow Completo

```bash
# 1. Brainstorming con AI
# → Salva output in /tmp/feature-analysis/

# 2. Setup feature
./scripts/start-new-feature.sh "nome-feature"
# → Nota FEATURE_DIR dall'output

# 3. Copia files
cp /tmp/feature-analysis/00-DESIGN.md docs/trading-system-docs/$FEATURE_DIR/
cp /tmp/feature-analysis/T-*.md .claude/agents/$FEATURE_DIR/

# 4. Run (AUTO tutto!)
./scripts/run-agents.sh $FEATURE_DIR
# → Auto-detect task
# → Auto-populate state
# → Esegue task
# → Sync automatico

# 5. Ship
./scripts/verify-e2e.sh
git commit -m "feat: ..."
```

### Script Disponibili

| Script | Scopo | Quando usare |
|---|---|---|
| `start-new-feature.sh` | Setup feature | Inizio ogni feature |
| `run-agents.sh` | Esegui orchestratore | Dopo aver copiato files |
| `sync-kb-to-rules.sh` | Sync KB → Rules | Automatico (o manuale se task singoli) |
| `verify-e2e.sh` | Test suite completa | Prima di commit |
| `pre-deployment-checklist.sh` | Checklist deploy | Prima di deploy |

---

**Versione**: 1.0  
**Data**: 2026-04-06  
**Maintainer**: Trading System Team
