# T-SW-08 — Worker Publish Endpoints + D1 Schema + StrategyDownloadWorker

## Obiettivo
Implementare il ciclo completo server-side: endpoint Worker per publish/pending/
confirm/check-id, migration D1, e StrategyDownloadWorker in C# che fa polling
D1 e scrive il file in strategies/private/ con write atomico.

## Dipendenze
- T-SW-01 (tipi — schema SDF v1 per validazione server)
- T-08 del progetto principale (Worker e D1 esistenti)
- T-15 del progetto principale (StrategyDefinitionLoader — FileSystemWatcher)

## Files da Creare
- `infra/cloudflare/worker/migrations/0002_strategies_wizard.sql`
- `infra/cloudflare/worker/src/routes/strategies-publish.ts`
- `src/TradingSupervisorService/Workers/StrategyDownloadWorker.cs`

## Files da Modificare
- `infra/cloudflare/worker/src/index.ts` — registrare nuove route
- `src/TradingSupervisorService/Program.cs` — AddHostedService<StrategyDownloadWorker>
- `src/SharedKernel/Alerting/AlertBuilder.cs` — aggiungere StrategyDownloaded()
- `src/TradingSupervisorService/appsettings.json` — sezione StrategyDownload

## Implementazione

### 0002_strategies_wizard.sql (idempotente)
```sql
CREATE TABLE IF NOT EXISTS strategies_pending (
  id            INTEGER PRIMARY KEY AUTOINCREMENT,
  strategy_id   TEXT UNIQUE NOT NULL,
  json_content  TEXT NOT NULL,
  schema_version INTEGER NOT NULL DEFAULT 1,
  created_at    TEXT NOT NULL DEFAULT (datetime('now')),
  downloaded_at TEXT,
  confirmed_at  TEXT,
  overwrite     INTEGER NOT NULL DEFAULT 0,
  file_hash     TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_pending_not_downloaded
  ON strategies_pending(created_at) WHERE downloaded_at IS NULL;

CREATE TABLE IF NOT EXISTS el_conversion_log (
  id INTEGER PRIMARY KEY AUTOINCREMENT,
  converted_at TEXT NOT NULL DEFAULT (datetime('now')),
  el_code_hash TEXT NOT NULL,
  convertible TEXT NOT NULL,
  confidence REAL,
  result_strategy_id TEXT,
  issues_json TEXT,
  published INTEGER NOT NULL DEFAULT 0
);
```

### strategies-publish.ts — 5 endpoint
```
POST   /api/v1/strategies/publish            → upsert in strategies_pending
GET    /api/v1/strategies/pending            → lista non downloaded (per Supervisor)
PATCH  /api/v1/strategies/:id/confirm-download → aggiorna downloaded_at + confirmed_at
GET    /api/v1/strategies/check-id/:id       → { exists: boolean }
```

Validazione server-side minima (non fidarsi del client):
- strategy_id non vuoto
- schema_version === 1
- structure.legs.length >= 1

Conflict handling:
- Se strategy_id esiste e overwrite=false → 409 { error: 'conflict', strategy_id }
- Se overwrite=true → UPDATE con ON CONFLICT

### StrategyDownloadWorker.cs
```
Loop ogni StrategDownload:PollIntervalSeconds (default 60s):
  GET /api/v1/strategies/pending
  Per ogni strategia:
    1. Valida JSON (IsValidSdfJson → controlla strategy_id, schema_version, legs)
    2. Se overwrite=false e file esiste → conferma senza scrivere (no loop)
    3. Write atomico:
       File.WriteAllText(tmpPath, json)
       File.Move(tmpPath, filePath, overwrite: strategy.Overwrite)
    4. PATCH /api/v1/strategies/:id/confirm-download
    5. Alert: AlertBuilder.StrategyDownloaded(strategyId, filePath)
    6. Log INFO "Strategy {id} written to {path}"
  On exception per singola strategia: log ERROR, continua con le altre
```

Write atomico (.NET):
```csharp
string tmpPath = filePath + ".tmp";
await File.WriteAllTextAsync(tmpPath, strategy.JsonContent, ct);
File.Move(tmpPath, filePath, overwrite: strategy.Overwrite);
// File.Move è atomica a livello OS sullo stesso filesystem
```

## Test
- `TEST-SW-08-01`: POST /publish body valido → 200, record in D1
- `TEST-SW-08-02`: POST /publish duplicate, overwrite=false → 409
- `TEST-SW-08-03`: POST /publish duplicate, overwrite=true → 200, record aggiornato
- `TEST-SW-08-04`: GET /pending → solo record con downloaded_at = NULL
- `TEST-SW-08-05`: PATCH /confirm-download → downloaded_at e confirmed_at valorizzati
- `TEST-SW-08-06`: GET /check-id/{id esistente} → `{ exists: true }`
- `TEST-SW-08-07`: GET /check-id/{id non esistente} → `{ exists: false }`
- `TEST-SW-08-08`: StrategyDownloadWorker: strategia pending → file in strategies/private/
- `TEST-SW-08-09`: StrategyDownloadWorker: JSON non valido → NO file scritto, alert inviato
- `TEST-SW-08-10`: Write atomico: mock crash durante write → nessun file .tmp rimasto

## Done Criteria
- [ ] `wrangler d1 migrations apply` → tabelle create senza errori
- [ ] Tutti i test TEST-SW-08-XX passano
- [ ] Write atomico verificato (nessun file corrotto)
- [ ] PATCH confirm chiamato dopo ogni download riuscito
- [ ] FileSystemWatcher del StrategyDefinitionLoader carica il nuovo file automaticamente (test manuale)
- [ ] Alert "📥 Nuova strategia caricata" ricevuto su Telegram dopo download

## Stima
~2 giorni
