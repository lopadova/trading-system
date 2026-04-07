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
