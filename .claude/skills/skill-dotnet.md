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
*Skill version: 1.0 — auto-aggiornabile dagli agenti (vedi skill-self-improvement.md)*
