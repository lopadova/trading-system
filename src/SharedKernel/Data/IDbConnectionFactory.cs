using Microsoft.Data.Sqlite;

namespace SharedKernel.Data;

/// <summary>
/// Factory interface for creating and configuring SQLite database connections.
/// Implementations MUST apply PRAGMA settings (WAL mode, foreign keys, etc.).
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Opens a new SQLite connection with all required PRAGMA settings applied.
    /// Connection is opened and ready for use.
    /// Caller MUST dispose the connection (use 'await using' pattern).
    /// </summary>
    /// <param name="ct">Cancellation token for async operation</param>
    /// <returns>An open SqliteConnection with PRAGMA settings configured</returns>
    Task<SqliteConnection> OpenAsync(CancellationToken ct = default);
}
