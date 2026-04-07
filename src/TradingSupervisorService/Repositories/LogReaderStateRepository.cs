using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;

namespace TradingSupervisorService.Repositories;

/// <summary>
/// Data model for log_reader_state table records.
/// Tracks file read position for log tailing (tail -f style).
/// </summary>
public sealed record LogReaderStateRecord
{
    public string FilePath { get; init; } = string.Empty;
    public long LastPosition { get; init; }
    public long LastSize { get; init; }
    public string UpdatedAt { get; init; } = string.Empty;  // ISO8601
}

/// <summary>
/// Repository interface for log_reader_state table.
/// Manages file read positions for log file monitoring.
/// </summary>
public interface ILogReaderStateRepository
{
    /// <summary>
    /// Gets the last read position for a specific log file.
    /// Returns null if this file has never been read before.
    /// </summary>
    Task<LogReaderStateRecord?> GetStateAsync(string filePath, CancellationToken ct);

    /// <summary>
    /// Upserts (INSERT OR REPLACE) the read position for a log file.
    /// Creates new record if first time, updates existing record otherwise.
    /// </summary>
    Task UpsertStateAsync(LogReaderStateRecord state, CancellationToken ct);
}

/// <summary>
/// SQLite implementation of ILogReaderStateRepository using Dapper.
/// All queries use explicit SQL (no ORM).
/// All IO operations have try/catch with logging.
/// </summary>
public sealed class LogReaderStateRepository : ILogReaderStateRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<LogReaderStateRepository> _logger;

    public LogReaderStateRepository(IDbConnectionFactory db, ILogger<LogReaderStateRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets the read state for a specific log file path.
    /// Returns null if this file hasn't been tracked yet (first read).
    /// </summary>
    public async Task<LogReaderStateRecord?> GetStateAsync(string filePath, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("FilePath cannot be null or empty", nameof(filePath));
        }

        const string sql = """
            SELECT
                file_path AS FilePath,
                last_position AS LastPosition,
                last_size AS LastSize,
                updated_at AS UpdatedAt
            FROM log_reader_state
            WHERE file_path = @FilePath
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new { FilePath = filePath },
                cancellationToken: ct);
            LogReaderStateRecord? result = await conn.QuerySingleOrDefaultAsync<LogReaderStateRecord>(cmd);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get log reader state for {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Upserts the read state for a log file.
    /// Uses INSERT OR REPLACE to handle both new and existing records.
    /// This is idempotent and thread-safe (last write wins).
    /// </summary>
    public async Task UpsertStateAsync(LogReaderStateRecord state, CancellationToken ct)
    {
        // Validate input (negative-first)
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }
        if (string.IsNullOrWhiteSpace(state.FilePath))
        {
            throw new ArgumentException("FilePath cannot be null or empty", nameof(state));
        }
        if (state.LastPosition < 0)
        {
            throw new ArgumentException("LastPosition cannot be negative", nameof(state));
        }
        if (state.LastSize < 0)
        {
            throw new ArgumentException("LastSize cannot be negative", nameof(state));
        }

        const string sql = """
            INSERT OR REPLACE INTO log_reader_state
                (file_path, last_position, last_size, updated_at)
            VALUES
                (@FilePath, @LastPosition, @LastSize, @UpdatedAt)
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct);

            CommandDefinition cmd = new(sql, new
            {
                state.FilePath,
                state.LastPosition,
                state.LastSize,
                state.UpdatedAt
            }, cancellationToken: ct);

            await conn.ExecuteAsync(cmd);

            _logger.LogDebug("Upserted log reader state for {FilePath} position={Position} size={Size}",
                state.FilePath, state.LastPosition, state.LastSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert log reader state for {FilePath}", state.FilePath);
            throw;
        }
    }
}
