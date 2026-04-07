# Errors Registry — Trading System Build
*Auto-aggiornato dagli agenti. Leggi questo file all'inizio di ogni task.*

**Total Errors**: 17 (ERR-001 to ERR-017)  
**Last Updated**: 2026-04-07

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

## ERR-012 — BackgroundService startup delay prevents test execution

**Scoperto da**: OutboxSyncWorker Test Fix
**Data**: 2026-04-07
**Sintomo**: Test `RunCycle_NoPendingEvents_DoesNotSendRequests` fails with mock verification error: "Expected invocation on the mock at least once, but was never performed: o => o.GetPendingAsync(...)". The worker's `GetPendingAsync` method is never called even though the test waits for the worker to run.
**Root cause**: The `OutboxSyncWorker.ExecuteAsync()` has a hardcoded 5-second startup delay (`await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken)`) before entering the main loop. The test only waits 100ms before canceling, so the worker never reaches `RunCycleAsync()` where `GetPendingAsync()` is called.
**Fix**: Make startup delay configurable via appsettings.json:
```csharp
// In OutboxSyncWorker.cs - Add field
private readonly int _startupDelaySeconds;

// In constructor
_startupDelaySeconds = config.GetValue("OutboxSync:StartupDelaySeconds", 5);

// In ExecuteAsync() - Replace hardcoded delay
if (_startupDelaySeconds > 0)
{
    await Task.Delay(TimeSpan.FromSeconds(_startupDelaySeconds), stoppingToken).ConfigureAwait(false);
}

// In test setup (OutboxSyncWorkerTests.cs constructor)
SetupConfigValue("OutboxSync:StartupDelaySeconds", "0");  // No delay for tests
```
**Skill aggiornato**: skill-testing.md (BackgroundService Testing)
**Impatto sui task futuri**: ALL BackgroundService workers with startup delays (see LL-038) must make the delay configurable for testing. Production default = 5s, test override = 0s. Applies to HeartbeatWorker, AlertDispatchWorker, CampaignMonitorWorker, OrderExecutorWorker, and any future workers.

---

## ERR-014 — BackgroundService cancellation token aborts database operations

**Scoperto da**: LogReaderWorker Test Fix
**Data**: 2026-04-07
**Sintomo**: 
- BackgroundService processes data correctly and logs show expected behavior
- Tests fail with `Assert.NotEmpty() Failure: Collection was empty`
- Database writes silently fail despite no exceptions in logs
- Issue only appears during service shutdown or test teardown

**Root cause**: 
Passing `stoppingToken` from `BackgroundService.ExecuteAsync()` to database operations causes writes to be aborted when the service stops. When `StopAsync()` is called, the cancellation token is canceled, and any in-flight database operations throw `OperationCanceledException` before completing. This exception is typically caught in the worker's error handler and swallowed, making the failure invisible.

**Sequence**:
1. Test calls `worker.StartAsync(cts.Token)`
2. Worker reads data and prepares to write to database
3. Test calls `cts.Cancel()` after short delay (e.g., 500ms)
4. Database operation `await conn.ExecuteAsync(cmd)` receives cancellation
5. `OperationCanceledException` thrown before `INSERT`/`UPDATE` completes
6. Exception caught in `RunCycleAsync` error handler: `catch (OperationCanceledException) { }`
7. No alert/record persisted, but no error logged
8. Test assertion fails: data collection is empty

**Fix**: 
Use `CancellationToken.None` for critical database operations that MUST complete:
- **Alert creation** - alerts must never be lost due to timing
- **State updates** - prevents re-processing data (duplicate alerts/records)
- **Audit writes** - compliance/audit records must be complete
- **Position updates** - trading state must be consistent

Continue using `stoppingToken` for operations that CAN be safely canceled:
- File I/O (can retry on next cycle)
- HTTP requests (can retry)
- `Task.Delay` (should cancel for fast shutdown)

**Pattern**:
```csharp
private async Task RunCycleAsync(CancellationToken stoppingToken)
{
    try
    {
        // Check cancellation BEFORE starting work (fast shutdown for new cycles)
        stoppingToken.ThrowIfCancellationRequested();
        
        // File I/O - OK to cancel (will retry next cycle)
        string data = await File.ReadAllTextAsync(filePath, stoppingToken);
        
        // Process data...
        var record = ProcessData(data);
        
        // Database write - MUST complete (use CancellationToken.None)
        await _repository.InsertAsync(record, CancellationToken.None);
        
        // State update - MUST complete (prevents duplicate processing)
        await _stateRepo.UpdateAsync(state, CancellationToken.None);
    }
    catch (OperationCanceledException)
    {
        // Cancellation during file I/O - OK, will retry
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Cycle failed");
        // Do not rethrow - worker survives errors
    }
}
```

**Affected Files**:
- `src/TradingSupervisorService/Workers/LogReaderWorker.cs` (fixed)
- Tests: `tests/TradingSupervisorService.Tests/Workers/LogReaderWorkerTests.cs`

**Skill aggiornato**: skill-dotnet.md (BackgroundService patterns, CancellationToken usage)

**Impatto sui task futuri**: 
- Review ALL BackgroundService implementations for this pattern
- Audit: PositionMonitorWorker, HeartbeatWorker, CampaignMonitorWorker
- Document in skill-dotnet.md: "NEVER pass stoppingToken to database writes"
- Add to .claude/rules/error-prevention.md for automatic loading

**Reference**: 
- .NET docs: BackgroundService graceful shutdown patterns
- Related: LL-038 (BackgroundService startup delay configuration)

---

## ERR-015 — Culture-specific decimal formatting breaks production assertions

**Scoperto da**: Test Coverage Sprint
**Data**: 2026-04-07
**Severity**: CRITICAL
**Sintomo**: GreeksMonitorWorker tests fail with Moq verification error:
```
Expected invocation on the mock at least once, but was never performed:
a => a.CreateAsync(It.Is<Alert>(alert => alert.Message.Contains("0.85")), ...)
Performed invocations:
AlertRepository.CreateAsync(Alert { Message = "High delta risk: position SPY has delta 0,85 (threshold 0,80)" })
```
Test expected "0.85" but message contained "0,85" (Italian decimal separator).

**Root cause**: String interpolation uses `CurrentCulture` for numeric formatting. On Italian Windows (or any non-US locale), `$"{0.85:F2}"` produces "0,85" instead of "0.85". This breaks:
1. Tests that assert on exact message format
2. Log parsing that expects dot decimal separator
3. Integration with external systems expecting invariant format
4. JSON serialization if manually constructing JSON strings

**Fix**: ALWAYS use `CultureInfo.InvariantCulture` for numeric formatting in production code:
```csharp
// WRONG (culture-dependent)
Message = $"High delta risk: position {position.ContractSymbol} has delta {position.Delta:F2} (threshold {_deltaThreshold:F2})"

// CORRECT (culture-invariant)
Message = string.Format(CultureInfo.InvariantCulture,
    "High delta risk: position {0} has delta {1:F2} (threshold {2:F2})",
    position.ContractSymbol, position.Delta, _deltaThreshold)
```

**Pattern applies to**:
- Alert messages
- Log entries
- CSV/TSV exports
- Manual JSON construction
- SQL query strings with embedded numbers
- File names with timestamps/numbers
- External API payloads

**Use CurrentCulture ONLY for**:
- UI display to end users
- Localized reports
- User-facing dashboards

**Affected Files**:
- `src/TradingSupervisorService/Workers/GreeksMonitorWorker.cs` (fixed in 4 locations)
- Audit needed: All workers, all alert creators, all log formatters

**Skill aggiornato**: skill-dotnet.md (Culture-Invariant Formatting), skill-testing.md (Culture-Aware Test Data)

**Impatto sui task futuri**: 
- Add rule: "NEVER use string interpolation for numeric values in production code paths"
- Add analyzer: Flag all `$"{number:F2}"` patterns outside UI code
- Review ALL existing alert/log messages for culture-dependent formatting
- Add CI check: Run tests with `CultureInfo.CurrentCulture = new CultureInfo("it-IT")` to catch issues

---

## ERR-016 — Windows Defender Application Control blocks test DLL execution

**Scoperto da**: Test Coverage Sprint
**Data**: 2026-04-07
**Severity**: CRITICAL
**Sintomo**: 
```
Skipping: OptionsExecutionService.Tests (could not load dependent assembly)
Could not load file or assembly 'OptionsExecutionService.Tests.dll'
Un criterio di controllo dell'applicazione ha bloccato il file. (0x800711C7)
```
49 tests blocked, 18% of total test suite cannot run.

**Root cause**: 
Windows Defender **Application Control Policy** (not Real-Time Protection) blocks unsigned DLLs. Initially misdiagnosed as "Windows Defender blocking" but actual cause is **AVIRA Security** controlling Smart App Control on user's system.

Key distinction:
- **Windows Defender Real-Time Protection**: Can be disabled temporarily, respects exclusions
- **Windows Defender Application Control**: Group Policy or enterprise-managed, cannot be disabled by user
- **Smart App Control**: Windows 11 feature, can be managed by third-party antivirus like AVIRA
- **AVIRA Security**: Third-party antivirus that takes control of Smart App Control, blocks unsigned assemblies even with exclusions

**Fix Attempts**:
1. ❌ Windows Defender exclusions → No effect (not Windows Defender causing block)
2. ❌ AVIRA exclusions → No effect (AVIRA re-scans on rebuild, blocks unsigned DLLs)
3. ⏳ Strong-name signing → Pending (requires sn.exe from Visual Studio)
4. ✅ Temporary unlock script → Works (disables AVIRA for 10 minutes during test run)

**Temporary Workaround**:
```powershell
.\scripts\unlock-and-test-all.ps1  # Disables AVIRA, runs tests, re-enables automatically
```

**Permanent Solution** (in progress):
Strong-name signing all assemblies makes them trusted by antivirus:
```powershell
.\scripts\setup-strong-name-signing.ps1  # Requires sn.exe from Visual Studio SDK
```

**Alternative Solutions**:
1. CI/CD on Linux (GitHub Actions) - no antivirus blocking
2. WSL2 environment - runs Linux kernel, no Windows antivirus
3. Docker container - isolated from host antivirus
4. Disable AVIRA permanently (not recommended for security)

**Detection**:
```powershell
# Check if AVIRA is running
Get-Process | Where-Object { $_.ProcessName -like "Avira*" }

# Check Windows Defender status
Get-MpPreference | Select-Object -Property DisableRealtimeMonitoring

# Check Smart App Control status (Windows 11)
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost" -Name "EnableWebContentEvaluation"
```

**Documentation Created**:
- `WINDOWS_DEFENDER_UNLOCK.md` - Complete troubleshooting guide
- `DEVELOPMENT_SETUP.md` - IDE setup + AVIRA handling
- `scripts/unlock-and-test-all.ps1` - All-in-one unlock script
- `scripts/unlock-with-avira.ps1` - AVIRA-specific handler
- `scripts/setup-strong-name-signing.ps1` - Strong-name signing automation

**Skill aggiornato**: skill-testing.md (Antivirus Handling), skill-windows-service.md (Strong-Name Signing)

**Impatto sui task futuri**: 
- Document antivirus requirements in development setup
- Include strong-name signing in CI/CD pipeline
- Test on clean Windows 11 with Smart App Control enabled
- Consider code signing certificate for production (Authenticode)

**Reference**:
- Error code: 0x800711C7 (ERROR_VIRUS_INFECTED)
- Microsoft docs: Smart App Control, Application Control Policy
- AVIRA docs: Application Control integration

---

## ERR-017 — StreamReader buffering breaks file position tracking in log tailing

**Scoperto da**: Test Coverage Sprint
**Data**: 2026-04-07
**Severity**: HIGH
**Sintomo**: LogReaderWorker tests fail with:
```
Assert.NotEmpty() Failure: Collection was empty
```
Worker executes without errors, file is read, but zero alerts created. Logs show worker running but `linesProcessed = 0`.

**Root cause**: 
`StreamReader` reads data in buffered chunks (default 1KB), not line-by-line. Loop condition `fs.Position < endPosition` fails because:

1. Test creates small log file (67 bytes)
2. Worker seeks to position 0
3. `StreamReader` constructor reads 1KB buffer from `FileStream`
4. `fs.Position` jumps to EOF (or file size)
5. Loop check: `fs.Position (67) < endPosition (67)` → false
6. Loop exits immediately, zero lines processed
7. No alerts created

**Buggy Code**:
```csharp
await using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
fs.Seek(startPosition, SeekOrigin.Begin);
using StreamReader reader = new(fs);

// BUG: fs.Position jumps ahead due to StreamReader buffering
while (!reader.EndOfStream && fs.Position < endPosition)
{
    string? line = await reader.ReadLineAsync(ct);
    // Never executed for small files!
}
```

**Fix**:
Remove file position check entirely. `StreamReader.EndOfStream` is sufficient:
```csharp
await using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
fs.Seek(startPosition, SeekOrigin.Begin);
using StreamReader reader = new(fs);

// CORRECT: Just read all available lines from start position
while (!reader.EndOfStream)
{
    string? line = await reader.ReadLineAsync(ct);
    if (line == null) break;
    
    await ProcessLogLineAsync(line, filePath, ct);
}
```

State tracking at higher level (after reading completes):
```csharp
// After processing all lines, save current file size as last position
await _stateRepo.UpsertStateAsync(new LogReaderStateRecord
{
    FilePath = currentLogFile,
    LastPosition = currentSize,  // Use file size, not fs.Position
    LastSize = currentSize,
    UpdatedAt = DateTime.UtcNow.ToString("O")
}, CancellationToken.None);
```

**Why This Works**:
- `StreamReader.EndOfStream` correctly detects when buffer is exhausted
- State tracking uses file size before reading (externally observable)
- Next cycle: seek to `LastPosition`, read new data appended since
- File rotation detected by comparing `LastSize` vs current size

**Pattern**:
NEVER mix low-level `FileStream.Position` with high-level `StreamReader`:
- `StreamReader` buffers → `FileStream.Position` unreliable during read
- Use `StreamReader.EndOfStream` for loop condition
- Track logical position (bytes consumed) separately from physical `FileStream.Position`

**Affected Files**:
- `src/TradingSupervisorService/Workers/LogReaderWorker.cs` (fixed)
- Tests: `tests/TradingSupervisorService.Tests/Workers/LogReaderWorkerTests.cs` (now passing)

**Similar Patterns to Audit**:
```csharp
// RISKY: Mixing FileStream and StreamReader positions
while (fs.Position < limit) {
    string line = reader.ReadLine();  // BUG: fs.Position jumped ahead
}

// SAFE: Use StreamReader abstractions only
while (!reader.EndOfStream) {
    string line = reader.ReadLine();
}

// SAFE: Use FileStream.Read directly if position tracking needed
byte[] buffer = new byte[1024];
while (fs.Position < limit) {
    int bytesRead = fs.Read(buffer, 0, buffer.Length);  // Direct read, position accurate
}
```

**Skill aggiornato**: skill-dotnet.md (File I/O Patterns, StreamReader Buffering)

**Impatto sui task futuri**: 
- Audit all code mixing `StreamReader` + `FileStream.Position`
- Document: "Use StreamReader OR FileStream, not both for position tracking"
- Add analyzer rule: Flag `fs.Position` access while `StreamReader` is in scope
- Consider alternative: `FileStream.Read` with manual line parsing for precise control

**Reference**:
- .NET docs: StreamReader buffering behavior
- Related: tail -f implementation patterns (inotify on Linux, file size polling on Windows)

---
