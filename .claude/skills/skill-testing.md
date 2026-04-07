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

*Skill version: 3.0 — Ultima modifica: Legacy Tests Fix — Data: 2026-04-07*
