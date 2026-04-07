using Microsoft.Data.Sqlite;
using SharedKernel.Data;

namespace SharedKernel.Tests.Helpers;

/// <summary>
/// In-memory SQLite connection factory for testing.
/// Returns the same already-open connection for all calls.
/// Useful for testing database operations without file I/O.
/// </summary>
public sealed class InMemoryConnectionFactory : IDbConnectionFactory
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Creates a factory that returns the given in-memory SQLite connection.
    /// </summary>
    /// <param name="connection">Already-open SQLite connection (must use "DataSource=:memory:")</param>
    public InMemoryConnectionFactory(SqliteConnection connection)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
    }

    /// <summary>
    /// Returns the in-memory connection.
    /// Note: Connection is NOT disposed by this factory.
    /// </summary>
    public Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        // Return same connection (already open, in-memory)
        // Do NOT dispose - caller is responsible for lifetime
        return Task.FromResult(_connection);
    }
}
