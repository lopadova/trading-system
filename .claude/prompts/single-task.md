# SINGLE TASK AGENT — Template Universale

> Questo file descrive il comportamento comune a TUTTI i sub-agenti.
> Il prompt specifico del task è in .claude/agents/TASK-XX.md

---

## STEP 0 — Bootstrap (OBBLIGATORIO prima di qualsiasi codice)

```bash
# 1. Leggi il protocollo di self-improvement
cat .claude/skills/skill-self-improvement.md

# 2. Leggi gli errori noti (CRITICO — evita di ripetere errori già scoperti)
cat knowledge/errors-registry.md

# 3. Leggi lezioni apprese
cat knowledge/lessons-learned.md

# 4. Leggi correzioni ai task
cat knowledge/task-corrections.md

# 5. Leggi il tuo prompt specifico
cat .claude/agents/$TASK_ID.md

# 6. Leggi la specifica completa del task
cat docs/trading-system-docs/$TASK_FILE
```

---

## Loop di Implementazione (max 5 iterazioni)

```
ITERATION=1

WHILE ITERATION <= 5:

  ## STEP 1 — LETTURA CONTESTO
  - Rileggi errors-registry.md (potrebbe essere aggiornato da un altro agente)
  - Rileggi task-corrections.md per correzioni al tuo task
  - Identifica skill pertinenti e leggili

  ## STEP 2 — PIANIFICAZIONE
  Lista i file da creare/modificare con percorso esatto.
  Identifica le dipendenze tra sotto-task.
  Ordina per priorità: compila prima, testa dopo.

  ## STEP 3 — IMPLEMENTAZIONE
  Per ogni sotto-task:
    a. Scrivi il codice completo (NO stub, NO placeholder, NO TODO bloccanti)
    b. Salva il file nel percorso corretto
    c. Verifica compilazione:
       - .NET: `dotnet build --no-restore 2>&1 | tail -20`
       - Bun/TS: `cd dashboard && bun run build 2>&1 | tail -20`
    d. SE non compila:
       - Analizza l'errore ESATTO
       - Controlla se è un errore NOTO in errors-registry.md
       - Se è nuovo → aggiungi a errors-registry.md dopo fix
       - Correggi il codice
       - Ricompila
       - NON passare al sotto-task successivo finché non compila

  ## STEP 4 — TESTING
  Per ogni TEST-XX-YY nella lista:
    a. Scrivi il test (se non già esistente)
    b. Eseguilo: `dotnet test --filter "TestId=TEST-XX-YY" 2>&1`
    c. Registra: "TEST-XX-YY: PASS | FAIL (motivo)"
    d. SE FAIL:
       - Determina se è un bug nel codice o nel test
       - Se bug nel codice: correggi e ri-esegui
       - Se bug nel test: correggi il test (e documenta perché in lessons-learned)
       - Se dipende da infrastruttura non disponibile (IBKR paper): SKIP con nota

  ## STEP 5 — CHECKLIST VERIFICATION
  Per ogni item nella checklist del task:
    Esegui il check (bash, dotnet, sqlite3, curl, etc.)
    Registra: "CHECK: descrizione → PASS | FAIL"

  ## STEP 6 — VALUTAZIONE

  SE tutti TEST PASS (o SKIP giustificato) E tutta checklist PASS:

    ### STEP 6a — SELF-IMPROVEMENT (obbligatorio prima di DONE)
    Rifletti su ciò che hai imparato durante questo task:
    
    - Hai trovato errori o incompatibilità? → aggiorna errors-registry.md + skill pertinente
    - Hai scoperto un pattern migliore? → aggiorna il skill pertinente
    - La checklist del task aveva ambiguità? → aggiorna task-corrections.md
    - Hai usato una versione NuGet/npm diversa da quella nel skill? → aggiorna skill
    - C'è qualcosa che il PROSSIMO agente deve sapere? → aggiorna lessons-learned.md
    
    Anche se TUTTO è andato perfettamente, scrivi ALMENO:
    ```markdown
    ## LL-XXX — T-XX: [cosa hai imparato]
    **Task**: T-XX
    **Categoria**: [pattern|performance|compatibility|tooling|testing]
    **Scoperta**: Tutto OK. Pattern X funziona come documentato nel skill Y.
    **Applicazione**: Conferma che skill Y è aggiornato e affidabile.
    ```

    ### STEP 6b — REPORT
    Scrivi ./logs/TASK-XX-result.md (usa template da CLAUDE.md)

    ### STEP 6c — STATE UPDATE
    ```bash
    # Aggiorna .agent-state.json
    python3 -c "
    import json
    with open('.agent-state.json', 'r') as f: state = json.load(f)
    state['$TASK_ID'] = 'done'
    with open('.agent-state.json', 'w') as f: json.dump(state, f, indent=2)
    "
    ```

    Stampa: "✅ DONE: $TASK_ID — {N} test PASS, {M} check PASS, {K} lezioni registrate"
    BREAK

  ALTRIMENTI (almeno un FAIL):
    Analizza root cause di ogni FAIL
    Aggiorna errors-registry.md con ogni nuovo errore scoperto
    Correggi il codice
    ITERATION++

SE ITERATION > 5 E non DONE:
  Scrivi ./logs/TASK-XX-result.md con dettaglio fallimenti
  Aggiorna errors-registry.md con tutti gli errori non risolti
  Aggiorna task-corrections.md se la specifica sembra errata
  python3 -c "
  import json
  with open('.agent-state.json') as f: s = json.load(f)
  s['$TASK_ID'] = 'failed'
  with open('.agent-state.json', 'w') as f: json.dump(s, f, indent=2)
  "
  Stampa: "❌ FAILED: $TASK_ID — vedi logs/TASK-XX-result.md e knowledge/"
```

---

## Regole Non Derogabili

1. MAI avanzare a sotto-task successivo se il codice non compila
2. MAI segnare DONE con anche un solo test FAIL non giustificato
3. MAI modificare files in docs/ o .claude/ (read-only)
4. MAI committare su git
5. MAI omettere il STEP 6a (self-improvement): anche se tutto va bene, scrivi una lezione
6. MAI usare TODO bloccanti nel codice prodotto
7. SEMPRE leggere errors-registry.md prima di implementare
