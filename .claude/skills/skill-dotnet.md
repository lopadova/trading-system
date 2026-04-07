# Skill: .NET 10 / C# 13 — Convenzioni e Pattern Enterprise
> Leggi SEMPRE questo file prima di scrivere codice C#.
> AGGIORNALO quando scopri errori o pattern migliori (vedi skill-self-improvement.md).

---

## Pattern Base — BackgroundService

```csharp
public sealed class ExampleWorker : BackgroundService
{
    private readonly ILogger<ExampleWorker> _logger;
    private readonly IExampleRepository _repo;
    private readonly int _intervalSeconds;

    public ExampleWorker(ILogger<ExampleWorker> logger, IExampleRepository repo, IConfiguration cfg)
    {
        _logger = logger;
        _repo   = repo;
        _intervalSeconds = cfg.GetValue<int>("Monitoring:IntervalSeconds", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Worker} started. Interval={Interval}s",
            nameof(ExampleWorker), _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken)
                      .ConfigureAwait(false);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            // logica qui
        }
        catch (OperationCanceledException) { /* shutdown graceful — non loggare */ }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} cycle failed. Retry in {Interval}s",
                nameof(ExampleWorker), _intervalSeconds);
            // NON rilanciare: il worker deve sopravvivere agli errori di ciclo
        }
    }
}
```

## Pattern Repository con Dapper

```csharp
public sealed class ExampleRepository : IExampleRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<ExampleRepository> _logger;

    public ExampleRepository(IDbConnectionFactory db, ILogger<ExampleRepository> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task InsertAsync(ExampleRecord record, CancellationToken ct)
    {
        const string sql = """
            INSERT INTO examples (col1, col2, created_at)
            VALUES (@Col1, @Col2, @CreatedAt)
            """;
        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);
            await conn.ExecuteAsync(sql, new
            {
                record.Col1,
                record.Col2,
                CreatedAt = DateTime.UtcNow.ToString("O")
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InsertAsync failed for {Record}", record);
            throw;
        }
    }
}
```

## Pattern DbConnectionFactory

```csharp
public interface IDbConnectionFactory
{
    Task<SqliteConnection> OpenAsync(CancellationToken ct = default);
}

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string dbPath)
    {
        _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        SqliteConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);
        // PRAGMA obbligatori su ogni connessione
        await conn.ExecuteAsync("PRAGMA journal_mode=WAL;");
        await conn.ExecuteAsync("PRAGMA synchronous=NORMAL;");
        await conn.ExecuteAsync("PRAGMA busy_timeout=5000;");
        await conn.ExecuteAsync("PRAGMA foreign_keys=ON;");
        return conn;
    }
}
```

## Record Immutabili (DTO/Domain)

```csharp
public sealed record MachineMetrics
{
    public string   Hostname      { get; init; } = string.Empty;
    public DateTime TimestampUtc  { get; init; } = DateTime.UtcNow;
    public long     UptimeSeconds { get; init; }
    public double   CpuPercent    { get; init; }
    public double   RamPercent    { get; init; }
    public double   DiskFreeGb    { get; init; }
    public string   TradingMode   { get; init; } = "paper";
}
```

## Result Type (no eccezioni per flow control)

```csharp
public sealed record Result<T>
{
    public bool    Success { get; init; }
    public T?      Value   { get; init; }
    public string? Error   { get; init; }

    public static Result<T> Ok(T value)    => new() { Success = true,  Value = value };
    public static Result<T> Fail(string e) => new() { Success = false, Error = e };
}
```

## Anti-pattern Vietati

```csharp
// ❌ else dopo return
if (x) return;
else Do();   // → rimuovi else

// ❌ var per tipi non ovvi
var x = GetSomething();   // → SqliteConnection conn = ...

// ❌ IO senza try/catch
await File.WriteAllTextAsync(path, data);   // → wrappa in try/catch con log

// ❌ eccezione per flow control
try { var u = GetUser(id); } catch { return null; }   // → usa nullable return

// ❌ Thread.Sleep in async context
Thread.Sleep(1000);   // → await Task.Delay(1000, ct)
```

## DI Registration

```csharp
builder.Services.AddSingleton<IDbConnectionFactory>(
    _ => new SqliteConnectionFactory(config["Sqlite:SupervisorDbPath"]!));
builder.Services.AddScoped<IExampleRepository, ExampleRepository>();
builder.Services.AddHostedService<ExampleWorker>();
```

---

## Repository API Evolution — Domain-Driven Pattern

> ⚠️ AGGIORNATO da Legacy Tests Fix — 2026-04-07
> Repository pattern evolved from generic CRUD to domain-driven design
> Reference: ERR-004, LESSON-146

**RULE: Repository methods should be entity-specific, not generic**

```csharp
// ❌ OLD PATTERN: Generic CRUD (anti-pattern)
public interface IRepository<T>
{
    Task InsertAsync(T entity, CancellationToken ct);
    Task<T?> GetByIdAsync(string id, CancellationToken ct);
    Task UpdateStatusAsync(string id, string status, CancellationToken ct);
    Task<List<T>> ListActiveAsync(CancellationToken ct);
}

// ✅ NEW PATTERN: Domain-Driven (current standard)
public interface ICampaignRepository
{
    // Entity-specific method names
    Task SaveCampaignAsync(Campaign campaign, CancellationToken ct);  // Upsert pattern
    Task<Campaign?> GetCampaignAsync(string campaignId, CancellationToken ct);
    
    // State-based queries (not generic "ListActive")
    Task<IReadOnlyList<Campaign>> GetCampaignsByStateAsync(
        CampaignState state, 
        CancellationToken ct);
    
    // NO UpdateStatusAsync - use domain model methods instead
    // campaign = campaign.Activate();  // Domain method
    // await SaveCampaignAsync(campaign, ct);
}
```

**Migration Pattern**: Old → New
```csharp
// OLD: Generic insert
await repo.InsertAsync(campaign, ct);

// NEW: Domain-specific upsert
await repo.SaveCampaignAsync(campaign, ct);

// OLD: Status update via repository
await repo.UpdateStatusAsync(campaignId, "active", ct);

// NEW: Status update via domain model
Campaign campaign = await repo.GetCampaignAsync(campaignId, ct);
Campaign activated = campaign.Activate();  // Domain logic
await repo.SaveCampaignAsync(activated, ct);

// OLD: Generic list query
List<Campaign> active = await repo.ListActiveAsync(ct);

// NEW: State-based query
IReadOnlyList<Campaign> active = await repo.GetCampaignsByStateAsync(
    CampaignState.Active, ct);
```

**Benefits of Domain-Driven Repositories**:
1. **Intent clarity**: `SaveCampaignAsync` vs generic `InsertAsync`
2. **Type safety**: `GetCampaignsByStateAsync(CampaignState.Active)` vs string "active"
3. **Domain logic encapsulation**: Status changes via domain methods, not repository
4. **Read-only collections**: `IReadOnlyList<T>` prevents accidental mutations

---

## DTO Naming Convention — Record Suffix

> ⚠️ AGGIORNATO da Legacy Tests Fix — 2026-04-07
> Explicit "Record" or "Entry" suffix distinguishes DTOs from domain entities
> Reference: ERR-006, LESSON-147

**RULE: DTOs from repositories use explicit Record/Entry suffix**

```csharp
// Convention:
// - Domain entities: No suffix (Campaign, Order, Strategy)
// - Repository DTOs: Record/Entry suffix (AlertRecord, OutboxEntry, ServiceHeartbeat)

// ✅ CORRECT: Domain entity (business logic, mutable state)
public sealed class Campaign
{
    public string CampaignId { get; private set; }
    public CampaignState State { get; private set; }
    
    // Domain methods
    public Campaign Activate() { ... }
    public Campaign Close() { ... }
}

// ✅ CORRECT: Repository DTO (data transfer, immutable)
public sealed record AlertRecord
{
    public string AlertId { get; init; } = string.Empty;
    public string AlertType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? DetailsJson { get; init; }
    public string SourceService { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string? ResolvedAt { get; init; }
    public string? ResolvedBy { get; init; }
}

// ✅ CORRECT: Outbox DTO with "Entry" suffix (semantic naming)
public sealed record OutboxEntry
{
    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string PayloadJson { get; init; } = string.Empty;
    public string? DedupeKey { get; init; }
    public string Status { get; init; } = "pending";  // pending|sent|failed
    public int RetryCount { get; init; }
    public string? LastError { get; init; }
    public string? NextRetryAt { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string? SentAt { get; init; }
}

// ✅ CORRECT: Service heartbeat (semantic naming, not "MachineMetricsRecord")
public sealed record ServiceHeartbeat
{
    public string ServiceName { get; init; } = string.Empty;
    public string Hostname { get; init; } = string.Empty;
    public string LastSeenAt { get; init; } = string.Empty;
    public long UptimeSeconds { get; init; }
    public double CpuPercent { get; init; }
    public double RamPercent { get; init; }
    public double DiskFreeGb { get; init; }
    public string TradingMode { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}
```

**Type Evolution Examples**:
```csharp
// OLD → NEW renames
OutboxEvent      → OutboxEntry         (semantics: entry not event)
Alert            → AlertRecord         (explicit DTO suffix)
LogReaderState   → LogReaderStateRecord  (explicit DTO suffix)
MachineMetrics   → ServiceHeartbeat    (semantic: service-specific)
```

**Benefits**:
1. **Clarity**: Immediately distinguish domain vs data transfer
2. **Immutability**: Record suffix signals read-only DTO
3. **Consistency**: All repository DTOs follow same convention
4. **Refactoring**: Easy to identify DTOs when restructuring

---

## Worker Constructor Patterns

> ⚠️ AGGIORNATO da Legacy Tests Fix — 2026-04-07
> Worker constructor parameter order is part of the contract
> Reference: ERR-005, LESSON-148

**RULE: Document and verify constructor parameter order for BackgroundService workers**

```csharp
// Pattern: ILogger → Dependencies → IConfiguration
// Order rationale: Framework (logger) → Domain (repos, collectors) → Config

// ✅ HeartbeatWorker: collector BEFORE repo
public sealed class HeartbeatWorker : BackgroundService
{
    public HeartbeatWorker(
        ILogger<HeartbeatWorker> logger,
        IMachineMetricsCollector collector,  // ← collector first (data source)
        IHeartbeatRepository repository,     // ← repo second (data sink)
        IConfiguration configuration)
    {
        // ...
    }
}

// ✅ OutboxSyncWorker: config BEFORE httpFactory
public sealed class OutboxSyncWorker : BackgroundService
{
    public OutboxSyncWorker(
        ILogger<OutboxSyncWorker> logger,
        IOutboxRepository repository,
        IConfiguration configuration,        // ← config before external services
        IHttpClientFactory httpClientFactory)
    {
        // ...
    }
}

// ✅ TelegramWorker: minimal dependencies (no repository)
public sealed class TelegramWorker : BackgroundService
{
    public TelegramWorker(
        ILogger<TelegramWorker> logger,
        ITelegramAlerter telegramAlerter,
        IConfiguration configuration)
    {
        // ...
    }
}
```

**Prevention**:
1. Document constructor parameter order rationale in XML comments
2. Use constructor versioning for breaking changes (new constructor, deprecate old)
3. Consider builder pattern for workers with 4+ dependencies

---

*Skill version: 2.0 — Ultima modifica: Legacy Tests Fix — Data: 2026-04-07*
