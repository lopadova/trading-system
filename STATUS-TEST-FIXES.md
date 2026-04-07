# Status Fixing Test Legacy .NET

**Data**: 2026-04-07 05:45 AM
**Sessione**: Continuazione post-wizard-strategies-and-bot completion

---

## ✅ Lavoro Completato

### OptionsExecutionService.Tests - 100% RISOLTO

**Compilazione**: ✅ **0 errori**

**Fix applicati:**
1. **RepositoryIntegrationTests.cs** - Riscrittura completa
   - Risolto namespace conflict Campaign (usato alias `using CampaignEntity = OptionsExecutionService.Campaign.Campaign`)
   - Aggiornate API repository:
     - `InsertAsync` → `SaveCampaignAsync`
     - `GetByIdAsync` → `GetCampaignAsync`
     - `ListActiveAsync` → `GetCampaignsByStateAsync(CampaignState.Active)`
     - `UpdateStatusAsync` → Rimosso (usa Campaign.Activate()/Close() + SaveCampaignAsync)
   - Aggiornato modello Campaign:
     - `StrategyName, Status, StrategyParams` → `Strategy (StrategyDefinition), State (CampaignState enum), StateJson`
   - Aggiornato OrderTracking → OrderRecord con API LogOrderAsync/UpdateOrderStatusAsync
   - Creato helper CreateTestStrategy() con StrategyDefinition completo e valido
   - Cambiato OrderStatus.Pending → OrderStatus.PendingSubmit

2. **OrderPlacerTests.cs**
   - MigrationRunner.RunMigrations → RunAsync(migrations, CancellationToken.None)
   - CircuitBreaker_CanBeManuallyReset: void → async Task con await

3. **MigrationIntegrationTests.cs**
   - IMigration[] → IReadOnlyList<IMigration>

4. **.csproj**
   - Aggiunti package references mancanti:
     - Moq 4.20.70
     - SharedKernel.Tests project reference
     - coverlet.collector 6.0.0

5. **ProgramIntegrationTests.cs**
   - MaxRiskPercentOfAccount → MaxPositionPctOfAccount (0.05m invece di 5.0m)
   - CircuitBreakerResetMinutes → CircuitBreakerCooldownMinutes

---

## ⚠️  TradingSupervisorService.Tests - PARZIALMENTE RISOLTO

**Compilazione**: ❌ **58 errori rimanenti** (da 96 iniziali - 40% progresso)

**Fix applicati:**
1. ✅ Namespace fix: `SharedKernel.Tests.Helpers` → `SharedKernel.Tests.Data` (8 files)
2. ✅ MigrationIntegrationTests.cs: IMigration[] → IReadOnlyList<IMigration>
3. ✅ PositionsRepositoryTests.cs: Aggiunto `using Dapper;`
4. ✅ WindowsMachineMetricsCollectorTests.cs: await Assert.ThrowsAsync

**Errori rimanenti (API mismatches legacy):**

### 1. MachineMetrics Type Missing (17 errori)
**File**: RepositoryIntegrationTests.cs, WorkerLifecycleIntegrationTests.cs  
**Problema**: `SharedKernel.Domain.MachineMetrics` non esiste  
**Riferimenti**:
- RepositoryIntegrationTests.cs:52, 67, 71-76
- WorkerLifecycleIntegrationTests.cs:51, 105, 107-108

**Root cause**: Il tipo MachineMetrics è stato probabilmente rinominato o spostato. Test scritti per vecchia API.

### 2. IHeartbeatRepository API Changed (8 errori)
**File**: RepositoryIntegrationTests.cs, WorkerLifecycleIntegrationTests.cs  
**Metodi mancanti**:
- `InsertAsync(MachineMetrics)`
- `GetLatestAsync()`

**Root cause**: API repository cambiata. Serve leggere interfaccia attuale e riscrivere test.

### 3. IMachineMetricsCollector API Changed (3 errori)
**File**: WorkerLifecycleIntegrationTests.cs  
**Metodo mancante**: `Collect()`

**Root cause**: API collector cambiata.

### 4. Worker Constructor Signatures Changed (6 errori)
**File**: WorkerLifecycleIntegrationTests.cs  
**Problemi**:
- HeartbeatWorker: parametri scambiati (IHeartbeatRepository vs IMachineMetricsCollector)
- TelegramWorker: parametri scambiati (IHttpClientFactory vs IConfiguration)
- TelegramWorker: costruttore accetta parametri diversi (era 4, ora altro numero)

**Root cause**: Constructor refactoring. Serve leggere le classi Worker attuali.

### 5. ITelegramAlerter API Changed (1 errore)
**File**: WorkerLifecycleIntegrationTests.cs:170  
**Metodo mancante**: `SendAsync`

### 6. OutboxEvent Type Missing (4 errori)
**File**: RepositoryIntegrationTests.cs:85, 94, 104-108  
**Problema**: Tipo non trovato

**Root cause**: Tipo rimosso o rinominato.

### 7. IOutboxRepository API Changed (3 errori)
**File**: RepositoryIntegrationTests.cs  
**Metodi mancanti**:
- `EnqueueAsync(OutboxEvent)`
- `GetPendingSyncAsync()`

### 8. IvtsSnapshot Property Missing (1 errore)
**File**: RepositoryIntegrationTests.cs:203  
**Property mancante**: `ImpliedVolatility`

**Root cause**: Property rinominata. Serve verificare struttura IvtsSnapshot attuale.

### 9. TradingMode Type Conversion (1 errore)
**File**: ProgramIntegrationTests.cs:330  
**Problema**: `string` → `TradingMode` implicit conversion

**Fix**: Usare `TradingMode.Paper` o `TradingMode.Live` invece di stringa.

---

## 📊 Summary Statistiche

### Compilation Status
| Project | Errors Before | Errors After | Status |
|---------|--------------|--------------|--------|
| OptionsExecutionService.Tests | 96 | **0** | ✅ RISOLTO |
| TradingSupervisorService.Tests | 96 | **58** | ⚠️  40% progresso |
| **TOTAL** | **192** | **58** | **70% risolto** |

### Test Coverage (dalle sessioni precedenti)
- Dashboard tests: 177/177 PASS ✅
- Worker tests: 94 implementati, verificati via TypeScript ✅
- .NET tests legacy: 58 errori da risolvere ⚠️

---

## 🔧 Next Steps Per Completamento

Per risolvere i 58 errori rimanenti serve:

1. **Investigare API attuali** (30 min)
   - Leggere IHeartbeatRepository interface attuale
   - Leggere IMachineMetricsCollector interface attuale
   - Leggere IOutboxRepository interface attuale
   - Leggere Worker constructors attuali
   - Cercare MachineMetrics/OutboxEvent tipi attuali o equivalenti

2. **Rewrite test files** (2-2.5 ore)
   - RepositoryIntegrationTests.cs: ~30 errori → riscrivere sezioni Heartbeat/Outbox
   - WorkerLifecycleIntegrationTests.cs: ~20 errori → riscrivere worker instantiation e assertions
   - ProgramIntegrationTests.cs: 1 errore → fix TradingMode conversion
   - WindowsMachineMetricsCollectorTests.cs: verify compilazione

3. **Verifica finale** (15 min)
   - `dotnet build TradingSystem.sln` → 0 errori
   - `dotnet test TradingSystem.sln` → verifica pass rate

**Stima totale tempo**: ~3 ore

---

## 📝 Note Implementative

### Pattern osservati durante i fix

**1. Namespace Migration**
```csharp
// OLD (wrong)
using SharedKernel.Tests.Helpers;  // Versione vecchia con costruttore che richiede SqliteConnection

// NEW (correct)
using SharedKernel.Tests.Data;     // Versione nuova con costruttore parameterless
```

**2. Repository API Pattern**
```csharp
// OLD API (non più valida)
await repo.InsertAsync(entity);
await repo.GetByIdAsync(id);
await repo.UpdateStatusAsync(id, status);

// NEW API (corrente)
await repo.SaveAsync(entity);        // Upsert
await repo.GetAsync(id);              // GetByIdAsync → GetAsync
// Update via domain model: entity = entity.WithStatus(newStatus); await repo.SaveAsync(entity);
```

**3. Migration Runner**
```csharp
// OLD
IMigration[] migrations = SomeMigrations.All;
await runner.RunMigrations(migrations);

// NEW
IReadOnlyList<IMigration> migrations = SomeMigrations.All;
await runner.RunAsync(migrations, CancellationToken.None);
```

**4. Test Async Best Practices**
```csharp
// WRONG (xUnit1031 analyzer error)
public void TestMethod() {
    someAsyncMethod().GetAwaiter().GetResult();
}

// CORRECT
public async Task TestMethod() {
    await someAsyncMethod();
}
```

---

## 🎯 Raccomandazioni

1. **Priority fix**: I 58 errori in TradingSupervisorService.Tests sono tutti legacy API mismatches. Richiedono investigazione + rewrite.

2. **Pattern-based approach**: Molti errori sono ripetizioni dello stesso problema (es. MachineMetrics mancante appare 17 volte). Fixando un'istanza, le altre si risolvono automaticamente.

3. **Domain knowledge needed**: Alcuni fix richiedono capire intent originale del test:
   - Se MachineMetrics non esiste più, qual è il tipo equivalente?
   - Se IHeartbeatRepository.InsertAsync non esiste, qual è il metodo attuale?

4. **Test rewrite vs delete**: Valutare se alcuni test obsoleti vadano eliminati invece che riscritti (es. se testano funzionalità non più presente).

---

**Status finale sessione**:
- ✅ Tutti i task T-01b → T-12 completati
- ✅ OptionsExecutionService.Tests: compilazione riuscita
- ⚠️  TradingSupervisorService.Tests: 58 errori rimanenti (legacy API mismatches)
- 📊 Progresso totale: 70% test legacy risolti (134/192 errori fixati)
