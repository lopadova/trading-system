# ORCHESTRATOR — Trading System Multi-Agent Build

> Sei l'orchestratore principale. Il tuo unico obiettivo è completare
> tutti i 28 task del Trading System, in ordine, parallelizzando dove
> possibile, garantendo che ogni task sia VERAMENTE done prima di avanzare.

---

## STEP 0 — Inizializzazione (esegui SEMPRE all'avvio)

```bash
# 1. Leggi stato corrente
cat .agent-state.json 2>/dev/null || echo "Nessuno stato, parto da zero"

# 2. Leggi tutti i file knowledge (apprendimento dagli agenti precedenti)
cat knowledge/errors-registry.md   2>/dev/null || echo "Nessun errore registrato"
cat knowledge/lessons-learned.md   2>/dev/null || echo "Nessuna lezione"
cat knowledge/skill-changelog.md   2>/dev/null || echo "Nessun changelog"
cat knowledge/task-corrections.md  2>/dev/null || echo "Nessuna correzione"

# 3. Crea cartelle necessarie
mkdir -p knowledge logs src/SharedKernel src/TradingSupervisorService \
         src/OptionsExecutionService tests dashboard infra/cloudflare/worker \
         strategies/private strategies/examples

# 4. Inizializza .agent-state.json se non esiste
if [ ! -f .agent-state.json ]; then
  cat > .agent-state.json << 'JSON'
{
  "T-00":"pending","T-01":"pending","T-02":"pending","T-03":"pending",
  "T-04":"pending","T-05":"pending","T-06":"pending","T-07":"pending",
  "T-08":"pending","T-09":"pending","T-10":"pending","T-11":"pending",
  "T-12":"pending","T-13":"pending","T-14":"pending","T-15":"pending",
  "T-16":"pending","T-17":"pending","T-18":"pending","T-19":"pending",
  "T-20":"pending","T-21":"pending","T-22":"pending","T-23":"pending",
  "T-24":"pending","T-25":"pending","T-26":"pending","T-27":"pending"
}
JSON
fi

# 5. Inizializza file knowledge se non esistono
for f in errors-registry lessons-learned skill-changelog task-corrections; do
  [ -f "knowledge/${f}.md" ] || cat > "knowledge/${f}.md" << MD
# ${f} — Trading System Build
*Auto-generato dagli agenti durante il build.*
---
MD
done
```

---

## Wave di Esecuzione

Esegui le wave in sequenza. DENTRO ogni wave, usa `Task` per parallelizzare.
Prima di ogni wave, leggi `.agent-state.json` e salta i task già `"done"`.

```
WAVE 1:  [T-00]
WAVE 2:  [T-01, T-06]
WAVE 3:  [T-02, T-12, T-15]
WAVE 4:  [T-03, T-07, T-13]
WAVE 5:  [T-04, T-05, T-08, T-10, T-14, T-16, T-17, T-20]
WAVE 6:  [T-09, T-11, T-18]
WAVE 7:  [T-19, T-23]
WAVE 8:  [T-21]
WAVE 9:  [T-22]
WAVE 10: [T-24]
WAVE 11: [T-25]
WAVE 12: [T-26]
WAVE 13: [T-27]
```

---

## Loop di Esecuzione per ogni Wave

```
PER OGNI wave:
  leggi stato → filtra task "done" → salta
  
  PARALLEL (usa strumento Task per ogni task):
    segna task come "running" in .agent-state.json
    lancia sub-agente con prompt da .claude/agents/TASK-XX.md
    attendi risultato
    
    SE risultato contiene "✅ DONE":
      segna "done" in .agent-state.json
      leggi knowledge/ per aggiornamenti nuovi
    
    SE risultato contiene "❌ FAILED":
      primo fallimento: riprova (max 3 tentativi per task)
      dopo 3 fallimenti: segna "failed", logga in logs/BLOCKED.md
      SE task è nel percorso critico: HALT (blocca wave successiva)
      SE task è opzionale: continua con gli altri
  
  VERIFICA WAVE: tutti i task "done" → avanza
  SE qualche task "failed" nel percorso critico → HALT + report
```

---

## Percorso Critico (blocca il build se fallisce)

```
T-00 → T-01 → T-02 → T-08 → T-09 → T-12 → T-13 → T-14
→ T-16 → T-18 → T-19 → T-21 → T-22 → T-24 → T-26
```

Task non-bloccanti (warning ma non halt): T-05, T-10, T-11, T-23, T-25, T-27

---

## Prompt da passare a ogni Sub-Agente (via strumento Task)

```
Leggi il tuo prompt specifico da: .claude/agents/{TASK_ID}.md
Poi esegui il task seguendo esattamente le istruzioni in quel file.
```

Usa esattamente questa stringa come input al tool Task, sostituendo {TASK_ID}.

---

## Consolidamento Knowledge dopo ogni Wave

Dopo ogni wave completata, esegui:
```bash
echo "=== Wave N completata $(date) ===" >> knowledge/wave-summary.md
cat .agent-state.json >> knowledge/wave-summary.md
echo "Errors registrati: $(grep -c '^## ERR' knowledge/errors-registry.md 2>/dev/null || echo 0)"
echo "Lezioni apprese: $(grep -c '^## LL' knowledge/lessons-learned.md 2>/dev/null || echo 0)"
echo "Skill aggiornati: $(grep -c '^## ' knowledge/skill-changelog.md 2>/dev/null || echo 0)"
```

---

## Report Finale

Alla fine di tutte le wave, genera `./IMPLEMENTATION_REPORT.md`:

```markdown
# Trading System — Implementation Report

**Data**: {timestamp}
**Durata totale**: {elapsed}

## Sommario

| Task | Status | Iterazioni | Test Passati | Test Falliti |
|------|--------|-----------|--------------|--------------|
| T-00 | done   | 1         | 7/7          | 0            |
...

## Blockers (se presenti)
...

## Knowledge generata
- Errori registrati: N
- Lezioni apprese: N
- Skill aggiornati: N
- Correzioni task: N

## Prossimi passi manuali
1. Eseguire E2E su IBKR Paper (T-26 scenari REQUIRES_PAPER)
2. Aggiungere API key reale: wrangler secret put API_KEY
3. Configurare Telegram bot token in appsettings.json
4. Eseguire install-supervisor.ps1 e install-options-engine.ps1
5. Prima apertura live: impostare TradingMode=live con piena consapevolezza
```
