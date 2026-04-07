# Errors Registry — Trading System Build
*Auto-aggiornato dagli agenti. Leggi questo file all'inizio di ogni task.*

---

## ERR-TEMPLATE — Come aggiungere un errore
```
## ERR-NNN — [titolo descrittivo]
**Scoperto da**: T-XX | **Data**: YYYY-MM-DD
**Sintomo**: [errore visibile]
**Root cause**: [perché]
**Fix**: [codice corretto]
**Skill aggiornato**: [nome file]
**Impatto sui task futuri**: [T-XX, ...]
```

---

## ERR-001 — .NET SDK not installed in environment

**Scoperto da**: T-00
**Data**: 2026-04-05
**Sintomo**: `dotnet --version` returns "No .NET SDKs were found", `dotnet new` and `dotnet build` fail
**Root cause**: .NET 8 SDK is not installed on the Windows system. The dotnet runtime exists at `/c/Program Files/dotnet/dotnet` but no SDKs are available (`dotnet --list-sdks` returns empty)
**Fix**: Install .NET 8 SDK from https://dotnet.microsoft.com/download/dotnet/8.0 before attempting to build. Alternatively, all project files can be created manually (as done in T-00) and will compile once SDK is installed.
**Workaround applied in T-00**: Created all .csproj, .sln, and .cs files manually with correct structure. Files are ready to build once SDK is available.
**Skill aggiornato**: N/A (environmental issue, not code pattern)
**Impatto sui task futuri**: T-01 through T-27 will need .NET 8 SDK installed to compile and test code. User must install SDK before proceeding with implementation tasks.

---

## ERR-002 — vitest-pool-workers fails on Windows paths with spaces

**Scoperto da**: T-10
**Data**: 2026-04-07
**Sintomo**: `npm test` fails with error: `No such module "C:/Users/lopad/Documents/DocLore/Visual Basic/_NET/Applicazioni/trading-system/infra/cloudflare/worker/node_modules/vitest/dist/file:/C:/Users/lopad/Documents/DocLare/Visual%20Basic/_NET/Applicazioni/trading-system/infra/cloudflare/worker/node_modules/vitest/dist/workers/threads.js"`
**Root cause**: @cloudflare/vitest-pool-workers has a known issue with Windows paths containing spaces. The repository path contains "Visual Basic" which causes URL encoding issues in the worker runtime module resolution.
**Fix**: Move repository to path without spaces (e.g., `C:\trading-system`) OR run tests in CI/CD environment (GitHub Actions) OR use WSL/Linux.
**Workaround applied in T-10**: Verified code via TypeScript compilation (`npm run build`) and manual code review instead of automated tests. Build passed with 0 errors.
**Skill aggiornato**: N/A (environmental issue)
**Impatto sui task futuri**: Any Cloudflare Worker tasks (T-06 through T-12) cannot run automated tests in current Windows environment. Use TypeScript build + code review OR move to environment without path spaces.
**Cloudflare Documentation**: https://developers.cloudflare.com/workers/testing/vitest-integration/known-issues/#module-resolution

---

## ERR-003 — Campaign namespace conflict (class and namespace same name)

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Sintomo**: `CS0101: The namespace 'OptionsExecutionService.Campaign' already contains a definition for 'Campaign'`. Test code cannot use `Campaign` type without qualification because both namespace and class share the same name.
**Root cause**: OptionsExecutionService has namespace `OptionsExecutionService.Campaign` and class `Campaign` inside it, creating ambiguity when using `using OptionsExecutionService.Campaign;` - compiler cannot determine if you mean the namespace or the type.
**Fix**: Use type alias to disambiguate:
```csharp
using CampaignEntity = OptionsExecutionService.Campaign.Campaign;

// Then use CampaignEntity instead of Campaign in test code
CampaignEntity campaign = new() { ... };
await _campaignRepo.SaveCampaignAsync(campaign, CancellationToken.None);
```
**Skill aggiornato**: skill-testing.md (namespace conflict patterns)
**Impatto sui task futuri**: Any test code referencing Campaign type from OptionsExecutionService.Campaign namespace must use alias pattern. Consider renaming namespace in future refactoring (e.g., `OptionsExecutionService.Campaigns` plural).

---

## ERR-004 — Repository API evolution broke all integration tests

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Severity**: CRITICAL
**Sintomo**: 96 compilation errors in OptionsExecutionService.Tests, 128 in TradingSupervisorService.Tests. Methods like `InsertAsync`, `GetByIdAsync`, `ListActiveAsync`, `UpdateStatusAsync` do not exist on repository interfaces.
**Root cause**: Repository pattern evolved from generic CRUD to domain-driven design. Method names changed to be domain-specific:
- `InsertAsync(entity)` → `SaveCampaignAsync(entity)` (upsert pattern)
- `GetByIdAsync(id)` → `GetCampaignAsync(id)` (domain-specific name)
- `ListActiveAsync()` → `GetCampaignsByStateAsync(CampaignState.Active)` (state-based query)
- `UpdateStatusAsync(id, status)` → Removed (use domain model `campaign.Activate()` + `SaveCampaignAsync`)
- `EnqueueAsync(OutboxEvent)` → `InsertAsync(OutboxEntry)` (type renamed)
- `GetPendingSyncAsync(limit)` → `GetPendingAsync(limit)` (simplified name)
- `GetLatestAsync()` → `GetAllAsync()` (HeartbeatRepository returns all, not just latest)
- `InsertAsync(MachineMetrics)` → `UpsertAsync(ServiceHeartbeat)` (type changed)

**Fix**: Update all repository method calls to match current API. Always check repository interface definition before writing integration tests. Use domain model methods (Activate(), Close(), etc.) instead of direct status updates.

**Skill aggiornato**: skill-dotnet.md (Repository Patterns), skill-testing.md (Repository Integration Tests)
**Impatto sui task futuri**: ALWAYS verify repository API before writing tests. Domain-driven repositories use entity-specific method names, not generic CRUD.

---

## ERR-005 — Worker constructor parameter order changes broke lifecycle tests

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Severity**: CRITICAL
**Sintomo**: 
```
CS1503: Argument 2: cannot convert from 'IHeartbeatRepository' to 'IMachineMetricsCollector'
CS1503: Argument 3: cannot convert from 'Mock<IMachineMetricsCollector>' to 'IConfiguration'
```
**Root cause**: Worker constructors changed parameter order between initial design and current implementation:

**HeartbeatWorker**:
- OLD: `(ILogger, IHeartbeatRepository, IMachineMetricsCollector, IConfiguration)`
- NEW: `(ILogger, IMachineMetricsCollector, IHeartbeatRepository, IConfiguration)`

**OutboxSyncWorker**:
- OLD: `(ILogger, IOutboxRepository, IHttpClientFactory, IConfiguration)`
- NEW: `(ILogger, IOutboxRepository, IConfiguration, IHttpClientFactory)`

**TelegramWorker**:
- OLD: `(ILogger, IAlertRepository, ITelegramAlerter, IConfiguration)`
- NEW: `(ILogger, ITelegramAlerter, IConfiguration)` (removed IAlertRepository dependency)

**Fix**: ALWAYS verify worker constructor signature from actual implementation before writing tests:
```csharp
// Read the actual constructor from source code
public HeartbeatWorker(
    ILogger<HeartbeatWorker> logger,
    IMachineMetricsCollector collector,  // ← collector BEFORE repo
    IHeartbeatRepository repository,
    IConfiguration configuration)
```

**Skill aggiornato**: skill-testing.md (Worker Constructor Patterns)
**Impatto sui task futuri**: NEVER assume constructor parameter order. Always verify from implementation code. Constructor parameter changes are breaking changes requiring test updates.

---

## ERR-006 — DTO type renames not propagated to tests

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Severity**: CRITICAL
**Sintomo**: `CS0246: The type or namespace name 'OutboxEvent' could not be found`, same for `Alert`, `LogReaderState`, `MachineMetrics`.
**Root cause**: DTO types were renamed with explicit "Record" suffix for clarity:
- `OutboxEvent` → `OutboxEntry` (also changed from event to entry semantics)
- `Alert` → `AlertRecord`
- `LogReaderState` → `LogReaderStateRecord`
- `MachineMetrics` (SharedKernel.Domain) → `ServiceHeartbeat` (TradingSupervisorService.Repositories)

Additionally, properties changed:
- OutboxEntry: `Payload` → `PayloadJson`, `Synced` bool → `Status` string, added `CreatedAt`/`SentAt` timestamps
- ServiceHeartbeat: added `ServiceName`, `LastSeenAt`, `TradingMode`, `Version`, timestamps as strings (ISO 8601)
- LogReaderStateRecord: `ServiceName` → `FilePath` (changed primary key)

**Fix**: 
1. Find-replace old type names with new ones
2. Verify property names match current DTO definition
3. Update all property accesses (especially `Status == "pending"` instead of `Synced == false`)

**Skill aggiornato**: skill-dotnet.md (DTO Naming Convention)
**Impatto sui task futuri**: DTO types MUST have explicit "Record" suffix. Document property renames in migration guide. Always verify DTO definition before writing repository tests.

---

## ERR-007 — xUnit async test methods marked as void

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Severity**: HIGH
**Sintomo**: `xUnit1031: Do not use blocking task operations in test method. Use async and await instead.` warning when test method signature is `public void TestMethod()` but calls `await` operations.
**Root cause**: xUnit requires async test methods to have return type `async Task`, not `void`. Using `.GetAwaiter().GetResult()` or similar blocking calls triggers xUnit analyzer warning.

**Fix**:
```csharp
// WRONG
[Fact]
public void Dispose_CanBeCalledMultipleTimes()
{
    _collector.Dispose();
    _collector.Dispose();
    Assert.ThrowsAsync<ObjectDisposedException>(async () => 
        await _collector.CollectAsync(CancellationToken.None)).GetAwaiter().GetResult();
}

// CORRECT
[Fact]
public async Task Dispose_CanBeCalledMultipleTimes()
{
    _collector.Dispose();
    _collector.Dispose();
    await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        await _collector.CollectAsync(CancellationToken.None));
}
```

**Skill aggiornato**: skill-testing.md (xUnit Async Patterns)
**Impatto sui task futuri**: ALL test methods that use `await` MUST be `async Task`, never `void`. Enable xUnit analyzers to catch this at compile time.

---

## ERR-008 — MigrationRunner API breaking change

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Severity**: HIGH
**Sintomo**: `CS1061: 'MigrationRunner' does not contain a definition for 'RunMigrations'`. Method signature changed.
**Root cause**: MigrationRunner API changed:
- OLD: `Task RunMigrations(IMigration[] migrations)`
- NEW: `Task RunAsync(IReadOnlyList<IMigration> migrations, CancellationToken cancellationToken)`

Changes:
1. Method renamed: `RunMigrations` → `RunAsync`
2. Parameter type: `IMigration[]` → `IReadOnlyList<IMigration>`
3. Added `CancellationToken` parameter

**Fix**:
```csharp
// OLD
await runner.RunMigrations(OptionsMigrations.All);

// NEW
await runner.RunAsync(OptionsMigrations.All, CancellationToken.None);
```

**Skill aggiornato**: skill-sqlite-dapper.md (Migration Runner)
**Impatto sui task futuri**: All migration runner calls must use `RunAsync` with `CancellationToken`. Migration collections should be `IReadOnlyList<IMigration>` not arrays.

---

## ERR-009 — Moq async setup requires ReturnsAsync, not Returns

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Severity**: HIGH
**Sintomo**: Test runtime error: `System.InvalidCastException: Unable to cast object of type 'Task<MachineMetrics>' to type 'MachineMetrics'` when mock method returns `Task<T>`.
**Root cause**: When mocking async methods that return `Task<T>`, must use `.ReturnsAsync(value)`, not `.Returns(value)`. Using `Returns` wraps the value in a Task, but the mock framework cannot unwrap it correctly.

**Fix**:
```csharp
// WRONG
_mockCollector.Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
    .Returns(new MachineMetrics { ... });

// CORRECT
_mockCollector.Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(new MachineMetrics { ... });
```

Also applies to `ITelegramAlerter.SendImmediateAsync`:
```csharp
// CORRECT
mockTelegram.Setup(x => x.SendImmediateAsync(It.IsAny<TelegramAlert>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync(true);
```

**Skill aggiornato**: skill-testing.md (Moq Async Patterns)
**Impatto sui task futuri**: ALWAYS use `.ReturnsAsync(value)` for async mock methods. Use `It.IsAny<CancellationToken>()` as parameter matcher for CancellationToken.

---

## ERR-010 — Nullable type assertion without null check

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Severity**: MEDIUM
**Sintomo**: `CS1061: 'double?' does not contain a definition for comparison operators`. Cannot directly use `Assert.Equal(0.45, snapshot.IvrPercentile, precision: 2)` when property is `double?`.
**Root cause**: xUnit `Assert.Equal` with `precision` parameter requires non-nullable `double`, but `IvrPercentile` is `double?` (nullable).

**Fix**:
```csharp
// WRONG
Assert.Equal(0.45, snapshot.IvrPercentile, precision: 2);

// CORRECT
Assert.NotNull(snapshot.IvrPercentile);  // Verify not null first
Assert.Equal(0.45, snapshot.IvrPercentile.Value, precision: 2);  // Then access .Value
```

**Skill aggiornato**: skill-testing.md (xUnit Nullable Assertions)
**Impatto sui task futuri**: For nullable value type assertions with precision/tolerance, ALWAYS check `Assert.NotNull()` first, then access `.Value` property.

---

## ERR-011 — Namespace migration broke all test imports

**Scoperto da**: Legacy Tests Fix
**Data**: 2026-04-07
**Severity**: HIGH
**Sintomo**: `CS0234: The type or namespace name 'Helpers' does not exist in the namespace 'SharedKernel.Tests'`. 8 test files failed to compile.
**Root cause**: Namespace was renamed from `SharedKernel.Tests.Helpers` → `SharedKernel.Tests.Data` for better semantic meaning (contains test data factories like `InMemoryConnectionFactory`, not just "helpers").

**Fix**: Find-replace in all affected test files:
```csharp
// OLD
using SharedKernel.Tests.Helpers;

// NEW
using SharedKernel.Tests.Data;
```

Also required adding explicit imports in some files:
```csharp
using SharedKernel.Domain;  // For TradingMode enum
using TradingSupervisorService.Collectors;  // For MachineMetrics type
using Dapper;  // For Dapper extension methods in PositionsRepositoryTests
```

**Skill aggiornato**: skill-dotnet.md (Namespace Organization)
**Impatto sui task futuri**: Use semantic namespace names (`Data`, `Repositories`, `Workers`) instead of generic names (`Helpers`, `Utils`). Document namespace changes in migration guides.

---
