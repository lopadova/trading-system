using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Data;

/// <summary>
/// Runs database migrations in a transactional, idempotent manner.
/// Tracks applied migrations in schema_migrations table.
/// Each migration runs in its own transaction (rollback on failure).
/// </summary>
public sealed class MigrationRunner
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(IDbConnectionFactory db, ILogger<MigrationRunner> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Applies all pending migrations from the provided list.
    /// Migrations already applied (tracked in schema_migrations) are skipped.
    /// Each migration runs in its own transaction for atomicity.
    /// </summary>
    /// <param name="migrations">List of migrations to apply (will be sorted by Version)</param>
    /// <param name="ct">Cancellation token</param>
    public async Task RunAsync(IReadOnlyList<IMigration> migrations, CancellationToken ct)
    {
        // Always open connection and ensure table exists, even with empty migration list
        await using SqliteConnection conn = await _db.OpenAsync(ct);

        // Ensure schema_migrations table exists (idempotent)
        await conn.ExecuteAsync("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version    INTEGER PRIMARY KEY,
                name       TEXT NOT NULL,
                applied_at TEXT NOT NULL DEFAULT (datetime('now'))
            )
            """);

        // Validate input (negative-first) - after table creation
        if (migrations == null || migrations.Count == 0)
        {
            _logger.LogInformation("No migrations to run");
            return;
        }

        // Get set of already-applied migration versions
        IEnumerable<int> appliedList = await conn.QueryAsync<int>(
            "SELECT version FROM schema_migrations");
        HashSet<int> applied = [.. appliedList];

        _logger.LogInformation("Found {AppliedCount} applied migrations, {TotalCount} total migrations",
            applied.Count, migrations.Count);

        // Apply migrations in version order
        foreach (IMigration m in migrations.OrderBy(x => x.Version))
        {
            // Skip if already applied (idempotent)
            if (applied.Contains(m.Version))
            {
                _logger.LogDebug("Migration {Version} '{Name}' already applied, skipping",
                    m.Version, m.Name);
                continue;
            }

            // Apply migration in a transaction
            await using SqliteTransaction tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);
            try
            {
                _logger.LogInformation("Applying migration {Version} '{Name}'...",
                    m.Version, m.Name);

                // Execute the migration SQL
                await conn.ExecuteAsync(m.UpSql, transaction: tx);

                // Record migration as applied
                await conn.ExecuteAsync(
                    "INSERT INTO schema_migrations (version, name) VALUES (@V, @N)",
                    new { V = m.Version, N = m.Name },
                    transaction: tx);

                // Commit the transaction
                await tx.CommitAsync(ct);

                _logger.LogInformation("Migration {Version} '{Name}' applied successfully",
                    m.Version, m.Name);
            }
            catch (Exception ex)
            {
                // Rollback on any error
                await tx.RollbackAsync(ct);

                _logger.LogError(ex, "Migration {Version} '{Name}' failed — rolled back",
                    m.Version, m.Name);

                // Rethrow to stop migration process
                // Caller (Program.cs) should log and exit
                throw;
            }
        }

        _logger.LogInformation("All migrations completed successfully");
    }
}
