# Skill: Self-Improvement Protocol — Meta-Learning degli Agenti

> **Questo è il file più importante del sistema.**
> Ogni agente DEVE leggere questo file all'inizio e seguire questo protocollo
> per garantire che gli errori non vengano mai ripetuti.

---

## Filosofia

Il sistema impara durante il build. Ogni errore scoperto, ogni pattern
migliorato, ogni test che fallisce per un motivo non previsto → diventa
conoscenza permanente che tutti gli agenti successivi erediteranno.

```
Agente X scopre errore
  ↓
Scrive in knowledge/errors-registry.md
  ↓
Aggiorna il file skill pertinente
  ↓
Aggiorna knowledge/skill-changelog.md
  ↓
Agente Y (task successivo) legge il skill aggiornato
  ↓
Agente Y non ripete l'errore
```

---

## Protocollo Obbligatorio — Inizio di Ogni Task

**STEP 0 — Prima di implementare qualsiasi cosa:**

```bash
# 1. Leggi il registro errori noti
cat ./knowledge/errors-registry.md

# 2. Leggi il registro lessons learned
cat ./knowledge/lessons-learned.md

# 3. Leggi il skill changelog per sapere cosa è cambiato
cat ./knowledge/skill-changelog.md
```

Se questi file non esistono ancora, creali con il template in fondo a questo file.

---

## Trigger per Self-Improvement

Aggiorna i file knowledge E il skill pertinente quando:

| Situazione | Azione |
|---|---|
| Test fallisce per API incompatibile (versione NuGet, Bun, etc.) | Aggiorna skill con versione corretta |
| Pattern genera errore di compilazione | Aggiungi anti-pattern al skill |
| SQL query non funziona come documentato | Correggi nel skill-sqlite-dapper.md |
| IBKR restituisce dati in formato diverso da quello atteso | Aggiorna skill-ibkr-api.md |
| Configurazione wrangler richiede campo non documentato | Aggiorna skill-cloudflare.md |
| Test xUnit non funziona per motivo infrastrutturale | Aggiorna skill-testing.md |
| Windows Service crash per ragione specifica | Aggiorna skill-windows-service.md |
| Qualsiasi pattern C# che causa errore ricorrente | Aggiorna skill-dotnet.md |
| Nuova best practice scoperta durante implementazione | Aggiorna skill pertinente |
| Checklist task ambigua o errata | Scrivi in knowledge/task-corrections.md |

---

## Come Aggiornare un Skill File

### 1. Formato aggiornamento

Aggiungi in cima alla sezione pertinente del skill file:

```markdown
> ⚠️ AGGIORNATO da TASK-XX in data YYYY-MM-DD
> Motivo: [descrizione breve del problema]
> Fix: [cosa è stato cambiato]
```

Poi modifica il contenuto del skill con il pattern corretto.

### 2. Aggiorna knowledge/skill-changelog.md

```markdown
## YYYY-MM-DD — TASK-XX

**Skill modificato**: skill-dotnet.md (o altro)
**Sezione**: Pattern Repository con Dapper
**Problema riscontrato**: ExecuteAsync non accetta CancellationToken in Dapper 2.x
**Fix applicato**: Usare `CommandDefinition` con CancellationToken
**Impatto**: Tutti i repository che usano Dapper devono usare CommandDefinition
```

### 3. Aggiorna knowledge/errors-registry.md

```markdown
## ERR-XXX — [titolo breve]

**Scoperto da**: TASK-XX
**Data**: YYYY-MM-DD
**Sintomo**: [cosa si vede: errore di compilazione, test che fallisce, runtime crash]
**Root cause**: [perché succede]
**Fix**: [codice corretto]
**Skill aggiornato**: [nome file]
**Impatto sui task futuri**: [quali task devono prestare attenzione]
```

### 4. Aggiorna knowledge/lessons-learned.md

```markdown
## LL-XXX — [titolo]

**Task**: T-XX
**Categoria**: [pattern | performance | compatibility | tooling | testing]
**Scoperta**: [descrizione della scoperta]
**Applicazione**: [come usare questa conoscenza]
**Rilevante per task**: [T-XX, T-YY, ...]
```

---

## Come Correggere un Task File

I file in `docs/trading-system-docs/` sono **read-only** (specifiche originali).
Le correzioni vanno in `knowledge/task-corrections.md`:

```markdown
## CORR-XXX — TASK-YY: [descrizione]

**Task originale**: TASK-YY-nome.md
**Sezione**: [sezione del file]
**Problema**: [cosa è ambiguo o errato nella specifica]
**Correzione proposta**: [testo corretto]
**Motivo**: [perché la specifica originale non funziona]
**Priorità**: CRITICAL | HIGH | LOW
```

Gli agenti che eseguono quel task devono leggere `knowledge/task-corrections.md`
e applicare le correzioni note prima di procedere.

---

## Protocollo Fine Task

Alla fine di ogni task (DONE o FAILED), scrivi in `knowledge/lessons-learned.md`
ALMENO una entry, anche se il task è andato bene:

```markdown
## LL-XXX — T-XX: [una cosa imparata]
```

Se il task è FAILED, scrivi obbligatoriamente anche in `knowledge/errors-registry.md`.

---

## Template File knowledge/ (se non esistono)

### knowledge/errors-registry.md
```markdown
# Errors Registry — Trading System Build

Ogni errore scoperto durante l'implementazione.
Gli agenti leggono questo file all'inizio di ogni task.

---
<!-- Le entry vengono aggiunte qui dagli agenti -->
```

### knowledge/lessons-learned.md
```markdown
# Lessons Learned — Trading System Build

Pattern, scoperte e ottimizzazioni emerse durante l'implementazione.

---
<!-- Le entry vengono aggiunte qui dagli agenti -->
```

### knowledge/skill-changelog.md
```markdown
# Skill Changelog — Aggiornamenti ai file di skill

Traccia ogni modifica ai file .claude/skills/*.md

---
<!-- Le entry vengono aggiunte qui dagli agenti -->
```

### knowledge/task-corrections.md
```markdown
# Task Corrections — Correzioni alle specifiche originali

Correzioni a doc/trading-system-docs/ (i file originali sono read-only).
Gli agenti applicano queste correzioni quando eseguono il task corrispondente.

---
<!-- Le entry vengono aggiunte qui dagli agenti -->
```

---

## Regole di Qualità per gli Aggiornamenti

1. **Un errore = una entry** — non aggregare errori diversi
2. **Codice sempre verificato** — il fix scritto nel skill deve compilare
3. **Retrocompatibilità** — quando cambi un pattern nel skill, indica se
   il codice dei task precedenti va aggiornato
4. **No opinioni** — scrivi solo fatti verificati con test che passano
5. **Data sempre presente** — ogni entry ha data e task di origine
6. **Impatto sui task futuri** — indica sempre quali task successivi
   devono leggere l'aggiornamento

---

## Esempio Completo

**Scenario**: Durante T-01, si scopre che `Dapper` 2.1.x non supporta
`CancellationToken` direttamente in `ExecuteAsync`.

**Azioni dell'agente T-01:**

1. Aggiorna `skill-dotnet.md` sezione "Pattern Repository":
```markdown
> ⚠️ AGGIORNATO da TASK-01 — 2025-04-04
> Dapper 2.1.x non supporta CancellationToken diretto.
> Usa CommandDefinition wrapper.

// ✅ CORRETTO per Dapper 2.1.x
var cmd = new CommandDefinition(sql, parameters, cancellationToken: ct);
await conn.ExecuteAsync(cmd);

// ❌ NON funziona in Dapper 2.1.x
await conn.ExecuteAsync(sql, parameters, cancellationToken: ct);
```

2. Scrive in `knowledge/errors-registry.md`:
```markdown
## ERR-001 — Dapper CancellationToken incompatibilità

**Scoperto da**: TASK-01
**Data**: 2025-04-04
**Sintomo**: CS1503 - Argument cannot convert CancellationToken to IDbTransaction
**Root cause**: Dapper 2.1.x non ha overload ExecuteAsync(sql, params, ct)
**Fix**: Usare CommandDefinition(sql, params, cancellationToken: ct)
**Skill aggiornato**: skill-dotnet.md, skill-sqlite-dapper.md
**Impatto**: TUTTI i repository da T-01 in poi devono usare CommandDefinition
```

3. Scrive in `knowledge/skill-changelog.md`:
```markdown
## 2025-04-04 — TASK-01
**Skill**: skill-dotnet.md
**Fix**: CancellationToken con CommandDefinition in Dapper
```

**Risultato**: Tutti gli agenti T-02, T-03, ..., T-27 che leggono il skill
trovano il pattern corretto e non ripetono l'errore.
