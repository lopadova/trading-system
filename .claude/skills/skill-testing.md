# Skill: Testing — xUnit (.NET) + Script bash + Playwright
> Aggiornabile dagli agenti. Controlla knowledge/errors-registry.md per fix noti.

---

## xUnit — Pattern Base

```csharp
public sealed class HeartbeatRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _conn = default!;
    private IDbConnectionFactory _db = default!;
    private HeartbeatRepository _sut = default!;

    public async Task InitializeAsync()
    {
        // DB in-memory per test — isolato, veloce
        _conn = new SqliteConnection("Data Source=:memory:");
        await _conn.OpenAsync();
        _db   = new InMemoryConnectionFactory(_conn);
        _sut  = new HeartbeatRepository(_db, NullLogger<HeartbeatRepository>.Instance);
        await new MigrationRunner(_db, NullLogger<MigrationRunner>.Instance)
            .RunAsync(SupervisorMigrations.All, CancellationToken.None);
    }

    public async Task DisposeAsync() => await _conn.DisposeAsync();

    [Fact]
    public async Task InsertAsync_ThenGetLatest_ReturnsInsertedRecord()
    {
        // Arrange
        MachineMetrics metrics = new()
        {
            Hostname      = "TEST-PC",
            CpuPercent    = 42.5,
            RamPercent    = 65.0,
            DiskFreeGb    = 120.0,
            UptimeSeconds = 3600,
            TradingMode   = "paper"
        };

        // Act
        await _sut.InsertAsync(metrics, CancellationToken.None);
        MachineMetrics? result = await _sut.GetLatestAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("TEST-PC",   result.Hostname);
        Assert.Equal(42.5,        result.CpuPercent, precision: 1);
        Assert.Equal("paper",     result.TradingMode);
    }

    [Fact]
    public async Task InsertAsync_SameDedupeKey_SecondInsertIgnored()
    {
        // Arrange
        string dedupeKey = "HEARTBEAT:TEST-PC:2025-01-01T10:00";

        // Act — inserisci due volte stesso dedupe_key
        await _sut.InsertWithDedupeAsync(BuildEvent(dedupeKey), CancellationToken.None);
        await _sut.InsertWithDedupeAsync(BuildEvent(dedupeKey), CancellationToken.None);
        int count = await CountRows();

        // Assert — solo 1 riga
        Assert.Equal(1, count);
    }
}
```

## InMemoryConnectionFactory per Test

> ⚠️ AGGIORNATO da TASK-01 — 2026-04-05
> Aggiunto pattern completo con keep-alive connection per in-memory SQLite
> Fix: In-memory databases require a persistent connection or they get destroyed

```csharp
// Pattern completo con keep-alive connection (implementato in SharedKernel.Tests)
// Location: tests/SharedKernel.Tests/Data/InMemoryConnectionFactory.cs
public sealed class InMemoryConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;

    public InMemoryConnectionFactory()
    {
        // Unique database name for isolation between test instances
        string dbName = $"TestDb_{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        // Keep-alive connection prevents database destruction
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
        ApplyPragmaSettings(_keepAliveConnection);
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        SqliteConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        ApplyPragmaSettings(conn);
        return conn;
    }

    private static void ApplyPragmaSettings(SqliteConnection conn)
    {
        // In-memory databases do not support WAL mode
        conn.Execute("PRAGMA journal_mode=DELETE;");
        conn.Execute("PRAGMA synchronous=NORMAL;");
        conn.Execute("PRAGMA foreign_keys=ON;");
        conn.Execute("PRAGMA cache_size=-32000;");
    }

    public async ValueTask DisposeAsync()
    {
        await _keepAliveConnection.DisposeAsync();
    }
}

// Usage in tests:
await using InMemoryConnectionFactory factory = new();
MigrationRunner runner = new(factory, loggerMock.Object);
await runner.RunAsync(migrations, CancellationToken.None);
// Database remains alive until factory is disposed
```

## Test Checklist Automatica (bash script)

```bash
#!/usr/bin/env bash
# scripts/verify-task.sh <TASK_ID>
# Esegue tutti i test del task e riporta PASS/FAIL per ogni TEST-XX-YY

TASK_ID="$1"
PASSED=0
FAILED=0
RESULTS_FILE="./logs/${TASK_ID}-test-results.txt"

run_test() {
    local test_id="$1"
    local description="$2"
    shift 2
    if "$@" > /dev/null 2>&1; then
        echo "✅ ${test_id}: PASS — ${description}" | tee -a "$RESULTS_FILE"
        ((PASSED++))
    else
        echo "❌ ${test_id}: FAIL — ${description}" | tee -a "$RESULTS_FILE"
        ((FAILED++))
    fi
}

# Esempio per T-00
run_test "TEST-00-01" "dotnet build" dotnet build TradingSystem.sln --no-restore
run_test "TEST-00-02" "dotnet test" dotnet test TradingSystem.sln --no-build
run_test "TEST-00-06" "gitignore strategies/private" \
    bash -c 'echo "test" > strategies/private/test.json && git check-ignore -q strategies/private/test.json && rm strategies/private/test.json'

echo ""
echo "Results: ${PASSED} PASS, ${FAILED} FAIL"
if [ "$FAILED" -gt 0 ]; then exit 1; fi
```

## Pattern Assert Comuni

```csharp
// Verifica range
Assert.InRange(metrics.CpuPercent, 0.0, 100.0);

// Verifica UUID
Assert.True(Guid.TryParse(campaign.CampaignId, out _),
    $"CampaignId '{campaign.CampaignId}' is not a valid UUID");

// Verifica che eccezione venga lanciata
await Assert.ThrowsAsync<DuplicateOrderException>(
    () => _sut.SubmitAsync(sameIdempotencyKey, ct));

// Verifica timing (con tolleranza)
TimeSpan elapsed = DateTime.UtcNow - startTime;
Assert.True(elapsed < TimeSpan.FromSeconds(5),
    $"Operation too slow: {elapsed.TotalSeconds}s");

// Verifica file esistente
Assert.True(File.Exists("strategies/private/.gitkeep"),
    "strategies/private/.gitkeep must exist");
```

## Test di Integrazione SQLite

```csharp
// Verifica WAL mode
[Fact]
public async Task Database_ShouldUseWalMode()
{
    await using SqliteConnection conn = await _db.OpenAsync(CancellationToken.None);
    string mode = await conn.QuerySingleAsync<string>("PRAGMA journal_mode");
    Assert.Equal("wal", mode);
}

// Verifica busy_timeout
[Fact]
public async Task Database_ShouldHaveBusyTimeout()
{
    await using SqliteConnection conn = await _db.OpenAsync(CancellationToken.None);
    int timeout = await conn.QuerySingleAsync<int>("PRAGMA busy_timeout");
    Assert.Equal(5000, timeout);
}
```

## Nominazione Test (TAG)

Ogni test nel progetto deve avere il nome esatto del TEST-ID dal file task:
```csharp
[Fact]
[Trait("TaskId", "T-01")]
[Trait("TestId", "TEST-01-03")]
public async Task TEST_01_03_InsertHeartbeat_GetLatest_ReturnsRecord() { ... }
```

Questo permette di filtrare i test per task:
```bash
dotnet test --filter "Trait=TaskId,T-01"
```

---

## xUnit Async Patterns — CRITICAL

> ⚠️ AGGIORNATO da Legacy Tests Fix — 2026-04-07
> Async test methods MUST return `async Task`, never `void`
> Reference: ERR-007, LESSON-150

**RULE: If a test method uses `await`, it MUST be `async Task`, not `void`**

```csharp
// ❌ WRONG - causes xUnit1031 warning
[Fact]
public void MyTest()
{
    var result = someAsyncMethod().GetAwaiter().GetResult();  // Blocking!
    Assert.Equal(expected, result);
}

// ❌ WRONG - async void is never correct for tests
[Fact]
public async void MyTest()
{
    var result = await someAsyncMethod();
    Assert.Equal(expected, result);
}

// ✅ CORRECT - async Task
[Fact]
public async Task MyTest()
{
    var result = await someAsyncMethod();
    Assert.Equal(expected, result);
}

// ✅ CORRECT - async exception assertion
[Fact]
public async Task MyTest_ThrowsException()
{
    await Assert.ThrowsAsync<InvalidOperationException>(
        async () => await failingMethod());
}

// ✅ CORRECT - dispose test with async
[Fact]
public async Task Dispose_CanBeCalledMultipleTimes()
{
    _sut.Dispose();
    _sut.Dispose();  // Should not throw
    
    // Verify disposed state
    await Assert.ThrowsAsync<ObjectDisposedException>(
        async () => await _sut.SomeAsyncMethod(CancellationToken.None));
}
```

**Prevention**: Enable xUnit analyzers in `.csproj`:
```xml
<PackageReference Include="xunit.analyzers" Version="1.18.0" />
```

---

## Moq Async Patterns — CRITICAL

> ⚠️ AGGIORNATO da Legacy Tests Fix — 2026-04-07
> Async mock methods MUST use `.ReturnsAsync()`, not `.Returns()`
> Reference: ERR-009, LESSON-151

**RULE: For methods returning `Task<T>`, use `.ReturnsAsync(value)` in Moq setup**

```csharp
// ❌ WRONG - causes InvalidCastException at runtime
Mock<IMachineMetricsCollector> mockCollector = new();
mockCollector.Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
    .Returns(new MachineMetrics { Hostname = "TEST", CpuPercent = 25.0 });

// ✅ CORRECT - use ReturnsAsync
mockCollector.Setup(x => x.CollectAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(new MachineMetrics { Hostname = "TEST", CpuPercent = 25.0 });

// ✅ CORRECT - void async methods use ReturnsAsync without value
Mock<ITelegramAlerter> mockTelegram = new();
mockTelegram.Setup(x => x.SendImmediateAsync(
        It.IsAny<TelegramAlert>(), 
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(true);  // Returns Task<bool>

// ✅ CORRECT - CancellationToken parameter matching
mockCollector.Setup(x => x.SomeAsync(It.IsAny<CancellationToken>()))
    .ReturnsAsync(result);

// ✅ CORRECT - verify async method was called
mockCollector.Verify(
    x => x.CollectAsync(It.IsAny<CancellationToken>()), 
    Times.Once);
```

**Worker Constructor Verification Pattern**:
```csharp
// ALWAYS verify constructor parameter order from implementation
// Example: HeartbeatWorker correct signature
HeartbeatWorker worker = new(
    NullLogger<HeartbeatWorker>.Instance,
    mockCollector.Object,         // ← collector BEFORE repo
    heartbeatRepo,                 // ← repo AFTER collector
    configuration);

// Example: OutboxSyncWorker correct signature
OutboxSyncWorker worker = new(
    NullLogger<OutboxSyncWorker>.Instance,
    outboxRepo,
    configuration,                 // ← config BEFORE httpFactory
    mockHttpFactory.Object);       // ← httpFactory LAST

// Example: TelegramWorker (no repository dependency)
TelegramWorker worker = new(
    NullLogger<TelegramWorker>.Instance,
    mockTelegram.Object,
    configuration);
```

---

## Nullable Assertions — xUnit Pattern

> ⚠️ AGGIORNATO da Legacy Tests Fix — 2026-04-07
> Nullable value types with precision require explicit null check
> Reference: ERR-010, LESSON-152

**RULE: For nullable value types, check null BEFORE accessing .Value**

```csharp
// ❌ WRONG - cannot use precision with nullable double?
IvtsSnapshot snapshot = await _repo.GetLatestSnapshotAsync("SPY", CancellationToken.None);
Assert.Equal(0.45, snapshot.IvrPercentile, precision: 2);  // CS1061 error

// ✅ CORRECT - explicit null check first
Assert.NotNull(snapshot.IvrPercentile);
Assert.Equal(0.45, snapshot.IvrPercentile.Value, precision: 2);

// ✅ CORRECT - alternative with null-coalescing
double ivrValue = snapshot.IvrPercentile ?? throw new AssertionException("IvrPercentile was null");
Assert.Equal(0.45, ivrValue, precision: 2);

// ✅ CORRECT - multiple nullable assertions
Assert.NotNull(snapshot.Iv30d);
Assert.NotNull(snapshot.Iv60d);
Assert.NotNull(snapshot.Iv90d);
Assert.InRange(snapshot.Iv30d.Value, 0.0, 1.0);
Assert.InRange(snapshot.Iv60d.Value, 0.0, 1.0);
Assert.InRange(snapshot.Iv90d.Value, 0.0, 1.0);
```

---

## Namespace Conflict Resolution

> ⚠️ AGGIORNATO da Legacy Tests Fix — 2026-04-07
> Handle namespace+class name collisions with type alias
> Reference: ERR-003, LESSON-153

**RULE: Use type alias when namespace and class share the same name**

```csharp
// Problem: OptionsExecutionService.Campaign.Campaign causes ambiguity
// CS0101: The namespace already contains a definition for 'Campaign'

// ✅ SOLUTION: Type alias at file top
using CampaignEntity = OptionsExecutionService.Campaign.Campaign;

namespace OptionsExecutionService.Tests.Repositories;

public sealed class CampaignRepositoryTests : IAsyncLifetime
{
    private ICampaignRepository _repo = default!;
    
    [Fact]
    public async Task SaveCampaignAsync_ThenGet_ReturnsEntity()
    {
        // Use aliased type throughout test file
        CampaignEntity campaign = new()
        {
            CampaignId = Guid.NewGuid().ToString(),
            Strategy = CreateTestStrategy("IronCondor"),
            State = CampaignState.Active,
            // ...
        };
        
        await _repo.SaveCampaignAsync(campaign, CancellationToken.None);
        CampaignEntity? retrieved = await _repo.GetCampaignAsync(campaign.CampaignId, CancellationToken.None);
        
        Assert.NotNull(retrieved);
        Assert.Equal(campaign.CampaignId, retrieved.CampaignId);
    }
}
```

**Prevention**: Never name a namespace the same as a class within it. Use plural form for namespaces if needed (e.g., `Campaigns` namespace, `Campaign` class).

---

## BackgroundService Testing — Configurable Timing

> ⚠️ AGGIUNTO da OutboxSyncWorker Test Fix — 2026-04-07
> BackgroundService startup delays MUST be configurable for tests
> Reference: ERR-012, LESSON-157, LL-038

**PROBLEM**: Workers with hardcoded startup delays prevent tests from executing within test timeouts.

**RULE: ALL timing values in BackgroundService workers (startup delay, interval) MUST be configurable via IConfiguration**

### Implementation Pattern

```csharp
// In BackgroundService worker (e.g., OutboxSyncWorker.cs)
public sealed class OutboxSyncWorker : BackgroundService
{
    private readonly int _intervalSeconds;
    private readonly int _startupDelaySeconds;  // ← Configurable, not hardcoded

    public OutboxSyncWorker(
        ILogger<OutboxSyncWorker> logger,
        IOutboxRepository outbox,
        IConfiguration config,
        IHttpClientFactory httpFactory)
    {
        // Load with production defaults
        _intervalSeconds = config.GetValue("OutboxSync:IntervalSeconds", 30);
        _startupDelaySeconds = config.GetValue("OutboxSync:StartupDelaySeconds", 5);  // ← Default 5s for production
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // CRITICAL: Use configurable delay, allow 0 for tests
        if (_startupDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(_startupDelaySeconds), stoppingToken)
                      .ConfigureAwait(false);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken)
                      .ConfigureAwait(false);
        }
    }
}
```

### Test Configuration

```csharp
// In test constructor (OutboxSyncWorkerTests.cs)
public OutboxSyncWorkerTests()
{
    _mockConfig = new Mock<IConfiguration>();
    
    // CRITICAL: Set startup delay to 0 for tests
    SetupConfigValue("OutboxSync:IntervalSeconds", "1");        // Fast interval
    SetupConfigValue("OutboxSync:StartupDelaySeconds", "0");    // ← No delay for tests
    SetupConfigValue("OutboxSync:BatchSize", "50");
    // ... other config values
}

private void SetupConfigValue(string key, string value)
{
    Mock<IConfigurationSection> section = new();
    section.Setup(s => s.Value).Returns(value);
    _mockConfig.Setup(c => c.GetSection(key)).Returns(section.Object);
    _mockConfig.Setup(c => c[key]).Returns(value);
}
```

### Test Execution Pattern

```csharp
[Fact]
public async Task RunCycle_NoPendingEvents_DoesNotSendRequests()
{
    // Arrange
    _mockOutbox.Setup(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new List<OutboxEntry>());

    OutboxSyncWorker worker = new(
        _mockLogger.Object,
        _mockOutbox.Object,
        _mockConfig.Object,
        _mockHttpFactory.Object);

    // Act
    CancellationTokenSource cts = new();
    Task workerTask = worker.StartAsync(cts.Token);
    
    // With StartupDelaySeconds=0, worker starts immediately
    await Task.Delay(100);  // Short wait for first cycle
    
    cts.Cancel();
    await workerTask;

    // Assert
    _mockOutbox.Verify(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), 
        Times.AtLeastOnce);  // ← Now passes!
}
```

### Production Configuration (appsettings.json)

```json
{
  "OutboxSync": {
    "IntervalSeconds": 30,
    "StartupDelaySeconds": 5,    // ← Production: wait for dependencies
    "BatchSize": 50
  }
}
```

### Benefits

- **Fast tests**: 0ms startup delay in tests vs 5000ms in production
- **Reliable tests**: Worker executes immediately, no race conditions
- **Flexible**: Can simulate different delays in integration tests
- **Production-safe**: Default value (5s) still applies when config is missing

### All Affected Workers

Apply this pattern to:
- `HeartbeatWorker` (startup delay for metrics initialization)
- `OutboxSyncWorker` (startup delay for database/HTTP readiness)
- `AlertDispatchWorker` (startup delay for Telegram API)
- `CampaignMonitorWorker` (startup delay for IBKR connection)
- `OrderExecutorWorker` (startup delay for IBKR readiness)
- Any future BackgroundService implementations

**WHY**: Production workers need startup delay to wait for dependencies (database migrations, IBKR connection, external APIs). Tests need immediate execution to verify behavior within test timeout limits.

---

## Culture-Invariant Test Data — CRITICAL

**Problem**: Tests fail on non-US locales when asserting on numeric strings. Italian Windows formats `0.85` as `"0,85"` (comma separator), breaking assertions and log parsing.

**Root Cause**: String interpolation uses `Thread.CurrentThread.CurrentCulture` for formatting.

### ❌ WRONG: Culture-dependent formatting

```csharp
// BUG: Formats as "0,85" on Italian systems, "0.85" on US systems
string message = $"Delta {delta:F2} threshold {threshold:F2}";

// Test FAILS on Italian Windows
Assert.Contains("0.85", message);  // Expected "0.85", actual "0,85"
```

### ✅ CORRECT: Culture-invariant formatting

```csharp
// Production code: ALWAYS use InvariantCulture
string message = string.Format(CultureInfo.InvariantCulture,
    "Delta {0:F2} threshold {1:F2}",
    delta, threshold);

// Test: Assert passes on ALL locales
Assert.Contains("0.85", message);  // ✅ Always "0.85"
```

### When to use InvariantCulture (ALWAYS in production)

**REQUIRED** (programmatic consumption):
- Alert messages (for grep/parsing)
- Log entries (for analysis tools)
- CSV/TSV exports
- JSON string construction (if manual, not via `JsonSerializer`)
- SQL query strings with embedded numbers
- File names with timestamps
- External API payloads
- Any string that might be parsed/searched

**Use CurrentCulture** (UI only):
- User-facing dashboard numbers
- Localized reports
- Interactive UI displays

### Testing Pattern

**Option 1: Test with multiple cultures**
```csharp
[Theory]
[InlineData("en-US")]
[InlineData("it-IT")]
[InlineData("de-DE")]
public void AlertMessage_WithDifferentCultures_UsesInvariantFormat(string cultureName)
{
    // Arrange
    CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
    Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
    
    try
    {
        // Act
        string message = CreateAlert(0.85);
        
        // Assert - MUST pass regardless of culture
        Assert.Contains("0.85", message);
        Assert.DoesNotContain("0,85", message);
    }
    finally
    {
        Thread.CurrentThread.CurrentCulture = originalCulture;
    }
}
```

**Option 2: CI check with non-US culture**
```bash
# In CI/CD pipeline or pre-commit hook
dotnet test -e DOTNET_CLI_UI_LANGUAGE=it-IT
```

### Detection

Find all risky patterns:
```bash
# Find string interpolation with numeric format specifiers
grep -rn '\$".*{.*:[FfGgDd][0-9]' src/
```

### Audit Checklist

- [ ] All alert message creation
- [ ] All log formatters
- [ ] CSV/JSON exporters
- [ ] SQL query builders with numbers
- [ ] File name generators with timestamps

**Reference**: ERR-015, LL-177

---

## Windows Antivirus Handling in Tests

**Problem**: Error 0x800711C7 "Application Control Policy blocked file" prevents test DLL execution on Windows with antivirus.

### Root Causes (multiple, different solutions)

**Windows Defender Real-Time Protection**:
```powershell
# Add folder exclusion (immediate effect)
Add-MpPreference -ExclusionPath "C:\path\to\trading-system"
```

**AVIRA Security** (or other third-party AV):
```powershell
# Exclusions ineffective - requires strong-name signing OR temporary disable
.\scripts\unlock-and-test-all.ps1  # All-in-one: disable, test, re-enable
```

**Enterprise WDAC** (Group Policy):
- Contact IT admin for policy exclusion
- OR use CI/CD on Linux (no WDAC)
- OR obtain code signing certificate

### Detection Script

```powershell
# Check which antivirus is active
Get-Process | Where-Object { 
    $_.ProcessName -like "*Avira*" -or 
    $_.ProcessName -like "*Defender*" -or
    $_.ProcessName -like "*Norton*" -or
    $_.ProcessName -like "*McAfee*"
}

# Check Windows Defender status
Get-MpPreference | Select-Object DisableRealtimeMonitoring

# Check Smart App Control (Windows 11)
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppHost" -Name "EnableWebContentEvaluation"
```

### Permanent Solution: Strong-Name Signing

```powershell
# One-time setup (requires sn.exe from Visual Studio or Windows SDK)
.\scripts\setup-strong-name-signing.ps1

# This creates:
# - TradingSystem.snk (private key - NEVER commit to git)
# - Directory.Build.props (signing configuration)
# - All future builds are signed automatically
```

**Benefits**:
- Antivirus recognizes signed DLLs as trusted
- No exclusions needed
- Works with AVIRA, Smart App Control, enterprise AV
- Professional for production deployment

**Security**:
- `TradingSystem.snk` is PRIVATE key (in .gitignore)
- Never commit .snk to version control
- Backup securely (password manager, encrypted storage)

### Test Environment Documentation

Add to `DEVELOPMENT_SETUP.md`:
```markdown
## Antivirus Configuration

**If you see error 0x800711C7**:
1. Run `.\scripts\unlock-and-test-all.ps1` (temporary fix)
2. OR run `.\scripts\setup-strong-name-signing.ps1` (permanent fix)
3. OR use GitHub Actions (Linux CI, no antivirus)
```

### CI/CD Alternatives

**GitHub Actions** (Linux, no antivirus):
```yaml
# .github/workflows/test-on-push.yml
on: [push, pull_request]
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - run: dotnet test  # ✅ No antivirus blocking
```

**WSL2** (Linux subsystem on Windows):
```bash
wsl --install  # If not already installed
wsl
cd /mnt/c/path/to/trading-system
dotnet test  # ✅ No Windows antivirus interference
```

**Docker** (isolated environment):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /app
COPY . .
RUN dotnet test  # ✅ No host antivirus
```

**Reference**: ERR-016, LL-178, WINDOWS_DEFENDER_UNLOCK.md, DEVELOPMENT_SETUP.md

---

## File-Based Testing: StreamReader Buffering Gotcha

**Problem**: Tests fail with empty results when reading small files with `StreamReader` + `FileStream.Position` tracking.

**Root Cause**: `StreamReader` buffers data in 1KB+ chunks. `FileStream.Position` jumps to buffer end (or EOF for small files), not to current line position.

### ❌ WRONG: Mixing StreamReader + FileStream.Position

```csharp
// BUG: fs.Position jumps ahead due to buffering
FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
fs.Seek(startPosition, SeekOrigin.Begin);
StreamReader reader = new(fs);

// Loop NEVER executes for files < 1KB
while (!reader.EndOfStream && fs.Position < endPosition)
{
    string line = await reader.ReadLineAsync();  // NEVER REACHED
}
```

**Why it fails**:
1. Test creates 67-byte file
2. `StreamReader` constructor reads 1KB buffer
3. `fs.Position` jumps to 67 (EOF)
4. Loop check: `67 < 67` → false
5. Zero lines processed

### ✅ CORRECT: Use StreamReader abstractions only

```csharp
// CORRECT: No fs.Position tracking during read
FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
fs.Seek(startPosition, SeekOrigin.Begin);
StreamReader reader = new(fs);

// Read all available lines from startPosition
while (!reader.EndOfStream)
{
    string line = await reader.ReadLineAsync();
    if (line == null) break;
    
    ProcessLine(line);
}

// Track position using file size (external observable), not fs.Position
long currentFileSize = new FileInfo(path).Length;
await SaveState(path, currentFileSize, currentFileSize);
```

### Alternative: Precise Position Tracking

If you MUST track exact byte position, use `FileStream.Read` directly (no `StreamReader`):

```csharp
byte[] buffer = new byte[1024];
long position = startPosition;

while (position < endPosition)
{
    fs.Seek(position, SeekOrigin.Begin);
    int bytesRead = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, endPosition - position));
    if (bytesRead == 0) break;
    
    // Manual line parsing from buffer
    string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
    // ... split by \n, process lines ...
    
    position += bytesRead;  // Accurate position tracking
}
```

### Testing Pattern

**ALWAYS test with small files** (< 1KB) to expose buffering bugs:

```csharp
[Fact]
public async Task LogReader_WithSmallFile_ProcessesAllLines()
{
    // Arrange
    string logPath = Path.Combine(_tempDir, "small.log");
    
    // Write 67-byte file (well below 1KB buffer size)
    await File.WriteAllTextAsync(logPath,
        "[2026-04-07 10:30:15 ERR] Test error\n");
    
    // Act
    await _worker.RunCycleAsync();
    
    // Assert
    var alerts = await _alertRepo.GetAllAsync();
    Assert.NotEmpty(alerts);  // Must process despite file < 1KB
}
```

### Detection

```bash
# Find all patterns mixing StreamReader + FileStream.Position
grep -rn "StreamReader" src/ | xargs grep -l "\.Position"
```

### Rules

1. **NEVER mix `StreamReader` + `FileStream.Position`** - buffering makes `.Position` unreliable
2. **Use `StreamReader` OR `FileStream`**, not both for position tracking
3. **`StreamReader.EndOfStream` is authoritative** for buffered reading
4. **Track logical position separately** from physical `FileStream.Position`
5. **Test with < 100 byte files** to catch buffering bugs early

### Affected Code Patterns

- Log file tailing (`tail -f` style workers)
- Incremental CSV/TSV processing
- Large file line-by-line parsing
- Any "resume from position" file reading

**Reference**: ERR-017, LL-179

---

*Skill version: 3.2 — Ultima modifica: Test Coverage Sprint (Culture, Antivirus, StreamReader) — Data: 2026-04-07*
