# Skill: SQLite + Dapper — Pattern e Best Practice
> Aggiornabile dagli agenti. Controlla knowledge/errors-registry.md per fix noti.

---

## PRAGMA Setup Obbligatorio (su ogni connessione)

```csharp
await conn.ExecuteAsync("PRAGMA journal_mode=WAL;");
await conn.ExecuteAsync("PRAGMA synchronous=NORMAL;");
await conn.ExecuteAsync("PRAGMA busy_timeout=5000;");
await conn.ExecuteAsync("PRAGMA foreign_keys=ON;");
await conn.ExecuteAsync("PRAGMA cache_size=-32000;");
```

## Migration System

```csharp
public sealed class MigrationRunner
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(IDbConnectionFactory db, ILogger<MigrationRunner> logger)
        => (_db, _logger) = (db, logger);

    public async Task RunAsync(IReadOnlyList<IMigration> migrations, CancellationToken ct)
    {
        await using SqliteConnection conn = await _db.OpenAsync(ct);

        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version    INTEGER PRIMARY KEY,
                name       TEXT NOT NULL,
                applied_at TEXT NOT NULL DEFAULT (datetime('now'))
            )
            """);

        HashSet<int> applied = [.. await conn.QueryAsync<int>(
            "SELECT version FROM schema_migrations")];

        foreach (IMigration m in migrations.OrderBy(x => x.Version))
        {
            if (applied.Contains(m.Version))
            {
                _logger.LogDebug("Migration {Version} already applied, skipping", m.Version);
                continue;
            }

            await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
            try
            {
                await conn.ExecuteAsync(m.UpSql, transaction: tx);
                await conn.ExecuteAsync(
                    "INSERT INTO schema_migrations (version, name) VALUES (@V, @N)",
                    new { V = m.Version, N = m.Name }, transaction: tx);
                await tx.CommitAsync(ct);
                _logger.LogInformation("Migration {Version} '{Name}' applied", m.Version, m.Name);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex, "Migration {Version} failed — rolled back", m.Version);
                throw;
            }
        }
    }
}
```

## Pattern INSERT OR IGNORE (idempotenza)

```csharp
// Per dedupe_key o idempotency_key — secondo insert ignorato silenziosamente
const string sql = """
    INSERT OR IGNORE INTO sync_outbox
        (event_id, event_type, payload_json, dedupe_key, status, created_at)
    VALUES
        (@EventId, @EventType, @PayloadJson, @DedupeKey, 'pending', datetime('now'))
    """;
// result.meta.changes == 0 → era un duplicato (normale, non un errore)
```

## Pattern UPSERT (INSERT OR REPLACE)

```csharp
// Per aggiornare un record esistente o inserirne uno nuovo
const string sql = """
    INSERT INTO log_reader_state (file_path, last_position, last_size, updated_at)
    VALUES (@FilePath, @LastPosition, @LastSize, datetime('now'))
    ON CONFLICT(file_path) DO UPDATE SET
        last_position = excluded.last_position,
        last_size     = excluded.last_size,
        updated_at    = excluded.updated_at
    """;
```

## Pattern Query Sicura con Limite

```csharp
// SEMPRE con LIMIT su query che possono restituire molte righe
const string sql = """
    SELECT * FROM sync_outbox
    WHERE status IN ('pending', 'failed')
      AND (next_retry_at IS NULL OR next_retry_at <= datetime('now'))
    ORDER BY created_at ASC
    LIMIT @BatchSize
    """;
IEnumerable<OutboxEntry> rows = await conn.QueryAsync<OutboxEntry>(sql, new { BatchSize = 50 });
```

## Pattern Transazione Esplicita

```csharp
await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
try
{
    foreach (OutboxEntry entry in entries)
    {
        await conn.ExecuteAsync(
            "UPDATE sync_outbox SET status='sent', sent_at=datetime('now') WHERE event_id=@Id",
            new { Id = entry.EventId }, transaction: tx);
    }
    await tx.CommitAsync(ct);
}
catch
{
    await tx.RollbackAsync(ct);
    throw;
}
```

## Anti-pattern Noti

```csharp
// ❌ CancellationToken diretto in Dapper (non supportato in tutte le versioni)
await conn.ExecuteAsync(sql, params, cancellationToken: ct);  // → potrebbe non compilare

// ✅ CORRETTO — CommandDefinition con CancellationToken
CommandDefinition cmd = new(sql, parameters, cancellationToken: ct);
await conn.ExecuteAsync(cmd);

// ❌ Query senza LIMIT su tabelle grandi
await conn.QueryAsync<Event>("SELECT * FROM platform_events");

// ✅ CORRETTO — sempre LIMIT
await conn.QueryAsync<Event>("SELECT * FROM platform_events ORDER BY id DESC LIMIT 100");

// ❌ Aprire connessione senza await using
var conn = await _db.OpenAsync(ct);   // → memory leak se eccezione

// ✅ CORRETTO
await using SqliteConnection conn = await _db.OpenAsync(ct);
```

## Verifica WAL mode

```bash
# Verifica post-creazione DB
sqlite3 data/supervisor.db "PRAGMA journal_mode;"
# Output atteso: wal

sqlite3 data/supervisor.db "PRAGMA busy_timeout;"
# Output atteso: 5000
```
