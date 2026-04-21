using Dapper;
using Microsoft.Data.Sqlite;

namespace SharedKernel.Data;

/// <summary>
/// Production SQLite connection factory.
/// Applies all required PRAGMA settings on every connection:
/// - WAL mode for concurrent reads/writes
/// - NORMAL synchronous for performance
/// - 5 second busy timeout for write contention
/// - Foreign keys enabled for referential integrity
/// - 32MB cache size for query performance
/// </summary>
public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>
    /// Creates a new factory for the specified database file path.
    /// Database file will be created if it does not exist (Mode=ReadWriteCreate).
    /// Directory will be created automatically if missing.
    /// </summary>
    /// <param name="dbPath">Absolute or relative path to the .db file</param>
    public SqliteConnectionFactory(string dbPath)
    {
        // Validate input (negative-first)
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            throw new ArgumentException("Database path cannot be null or empty", nameof(dbPath));
        }

        // Ensure the directory exists (SQLite creates the file, not directories)
        // Extract directory path from dbPath
        string? directoryPath = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        // Connection string with required settings
        // Mode=ReadWriteCreate: create DB if missing
        // Cache=Shared: allow multiple connections to share cache (WAL mode)
        _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared";
    }

    /// <summary>
    /// Opens a new connection and applies all required PRAGMA settings.
    /// This method is called frequently (per-request in repositories).
    /// </summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        SqliteConnection conn = new(_connectionString);

        try
        {
            // Open the connection first
            await conn.OpenAsync(ct);

            // Apply PRAGMA settings (required on EVERY connection)
            // WAL mode persists in DB file, but other PRAGMA settings are per-connection
            await conn.ExecuteAsync("PRAGMA journal_mode=WAL;");
            await conn.ExecuteAsync("PRAGMA synchronous=NORMAL;");
            await conn.ExecuteAsync("PRAGMA busy_timeout=5000;");
            await conn.ExecuteAsync("PRAGMA foreign_keys=ON;");
            await conn.ExecuteAsync("PRAGMA cache_size=-32000;");  // -32000 = 32MB

            return conn;
        }
        catch
        {
            // If any PRAGMA fails, dispose the connection and rethrow
            // This prevents connection leaks
            await conn.DisposeAsync();
            throw;
        }
    }
}
