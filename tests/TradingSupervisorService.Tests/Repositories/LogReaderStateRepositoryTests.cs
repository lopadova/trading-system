using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Data;
using SharedKernel.Tests.Data;
using TradingSupervisorService.Repositories;
using Xunit;

namespace tests.TradingSupervisorService.Tests.Repositories;

/// <summary>
/// Tests for LogReaderStateRepository.
/// Verifies state tracking for log file reading positions.
/// </summary>
public sealed class LogReaderStateRepositoryTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _dbFactory;
    private readonly LogReaderStateRepository _repository;

    public LogReaderStateRepositoryTests()
    {
        _dbFactory = new InMemoryConnectionFactory();
        _repository = new LogReaderStateRepository(_dbFactory, NullLogger<LogReaderStateRepository>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbFactory.DisposeAsync();
    }

    [Fact]
    public async Task GetStateAsync_WhenFileNotTracked_ReturnsNull()
    {
        // Arrange
        await CreateSchemaAsync();
        string filePath = "logs/test.log";

        // Act
        LogReaderStateRecord? result = await _repository.GetStateAsync(filePath, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpsertStateAsync_WhenNewFile_InsertsRecord()
    {
        // Arrange
        await CreateSchemaAsync();
        LogReaderStateRecord state = new()
        {
            FilePath = "logs/test.log",
            LastPosition = 1024,
            LastSize = 2048,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };

        // Act
        await _repository.UpsertStateAsync(state, CancellationToken.None);

        // Assert
        LogReaderStateRecord? result = await _repository.GetStateAsync(state.FilePath, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(state.FilePath, result.FilePath);
        Assert.Equal(state.LastPosition, result.LastPosition);
        Assert.Equal(state.LastSize, result.LastSize);
    }

    [Fact]
    public async Task UpsertStateAsync_WhenExistingFile_UpdatesRecord()
    {
        // Arrange
        await CreateSchemaAsync();
        string filePath = "logs/test.log";

        // Insert initial state
        LogReaderStateRecord initial = new()
        {
            FilePath = filePath,
            LastPosition = 1024,
            LastSize = 2048,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };
        await _repository.UpsertStateAsync(initial, CancellationToken.None);

        // Act - update with new position
        LogReaderStateRecord updated = new()
        {
            FilePath = filePath,
            LastPosition = 3072,
            LastSize = 4096,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };
        await _repository.UpsertStateAsync(updated, CancellationToken.None);

        // Assert
        LogReaderStateRecord? result = await _repository.GetStateAsync(filePath, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(updated.LastPosition, result.LastPosition);
        Assert.Equal(updated.LastSize, result.LastSize);
    }

    [Fact]
    public async Task UpsertStateAsync_WithNegativePosition_ThrowsArgumentException()
    {
        // Arrange
        await CreateSchemaAsync();
        LogReaderStateRecord state = new()
        {
            FilePath = "logs/test.log",
            LastPosition = -1,  // Invalid
            LastSize = 100,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repository.UpsertStateAsync(state, CancellationToken.None));
    }

    [Fact]
    public async Task UpsertStateAsync_WithEmptyFilePath_ThrowsArgumentException()
    {
        // Arrange
        await CreateSchemaAsync();
        LogReaderStateRecord state = new()
        {
            FilePath = "",  // Invalid
            LastPosition = 100,
            LastSize = 100,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _repository.UpsertStateAsync(state, CancellationToken.None));
    }

    [Fact]
    public async Task GetStateAsync_WithMultipleFiles_ReturnCorrectState()
    {
        // Arrange
        await CreateSchemaAsync();

        LogReaderStateRecord state1 = new()
        {
            FilePath = "logs/file1.log",
            LastPosition = 100,
            LastSize = 200,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };
        LogReaderStateRecord state2 = new()
        {
            FilePath = "logs/file2.log",
            LastPosition = 500,
            LastSize = 1000,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        };

        await _repository.UpsertStateAsync(state1, CancellationToken.None);
        await _repository.UpsertStateAsync(state2, CancellationToken.None);

        // Act
        LogReaderStateRecord? result1 = await _repository.GetStateAsync(state1.FilePath, CancellationToken.None);
        LogReaderStateRecord? result2 = await _repository.GetStateAsync(state2.FilePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(state1.LastPosition, result1.LastPosition);
        Assert.Equal(state2.LastPosition, result2.LastPosition);
    }

    /// <summary>
    /// Creates the log_reader_state table schema for testing.
    /// Mimics the production migration.
    /// </summary>
    private async Task CreateSchemaAsync()
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS log_reader_state (
                file_path      TEXT PRIMARY KEY NOT NULL,
                last_position  INTEGER NOT NULL,
                last_size      INTEGER NOT NULL,
                updated_at     TEXT NOT NULL
            );
            """;

        await using var conn = await _dbFactory.OpenAsync(CancellationToken.None);
        await conn.ExecuteAsync(createTableSql);
    }
}
