using Dapper;
using Microsoft.Data.Sqlite;
using SharedKernel.Data;

namespace SharedKernel.Tests.Data;

/// <summary>
/// In-memory SQLite connection factory for unit tests.
/// Each instance creates an isolated in-memory database.
/// Database is destroyed when all connections are closed.
/// </summary>
/// <remarks>
/// IMPORTANT: Keep at least one connection open for the lifetime of the test,
/// otherwise the in-memory database will be destroyed.
/// Use the pattern: await using InMemoryConnectionFactory factory = new();
/// </remarks>
public sealed class InMemoryConnectionFactory : IDbConnectionFactory, IAsyncDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;

    private static bool _typeHandlersRegistered = false;
    private static readonly object _lock = new();

    /// <summary>
    /// Creates a new in-memory database with a unique name.
    /// Keeps one connection open to prevent database destruction.
    /// </summary>
    public InMemoryConnectionFactory()
    {
        // Register Dapper TypeHandlers once (thread-safe)
        if (!_typeHandlersRegistered)
        {
            lock (_lock)
            {
                if (!_typeHandlersRegistered)
                {
                    SqlMapper.AddTypeHandler(new DateTimeHandler());
                    SqlMapper.AddTypeHandler(new NullableDateTimeHandler());
                    _typeHandlersRegistered = true;
                }
            }
        }

        // Use a unique database name for isolation between test instances
        string dbName = $"TestDb_{Guid.NewGuid():N}";
        _connectionString = $"Data Source={dbName};Mode=Memory;Cache=Shared";

        // Open a keep-alive connection to prevent database destruction
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();

        // Apply PRAGMA settings to the keep-alive connection
        ApplyPragmaSettings(_keepAliveConnection);
    }

    /// <summary>
    /// Opens a new connection to the in-memory database.
    /// Connection has all PRAGMA settings applied (same as production).
    /// </summary>
    public async Task<SqliteConnection> OpenAsync(CancellationToken ct = default)
    {
        SqliteConnection conn = new(_connectionString);
        await conn.OpenAsync(ct);

        // Apply PRAGMA settings (same as production)
        ApplyPragmaSettings(conn);

        return conn;
    }

    /// <summary>
    /// Applies production PRAGMA settings to a connection.
    /// Note: WAL mode is not supported in-memory, so we use DELETE mode.
    /// </summary>
    private static void ApplyPragmaSettings(SqliteConnection conn)
    {
        // In-memory databases do not support WAL mode
        // Use DELETE mode instead (default for in-memory)
        conn.Execute("PRAGMA journal_mode=DELETE;");
        conn.Execute("PRAGMA synchronous=NORMAL;");
        conn.Execute("PRAGMA foreign_keys=ON;");
        conn.Execute("PRAGMA cache_size=-32000;");
    }

    /// <summary>
    /// Closes the keep-alive connection, destroying the in-memory database.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _keepAliveConnection.DisposeAsync();
    }
}
