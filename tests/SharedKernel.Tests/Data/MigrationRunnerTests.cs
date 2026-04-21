using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using Xunit;
using Moq;
using Dapper;

namespace SharedKernel.Tests.Data;

/// <summary>
/// Unit tests for MigrationRunner.
/// Tests migration application, idempotency, rollback on failure, and ordering.
/// </summary>
public sealed class MigrationRunnerTests
{
    /// <summary>
    /// Simple test migration for testing.
    /// </summary>
    private sealed class TestMigration001 : IMigration
    {
        public int Version => 1;
        public string Name => "TestMigration001";
        public string UpSql => "CREATE TABLE test_table_1 (id INTEGER PRIMARY KEY);";
    }

    private sealed class TestMigration002 : IMigration
    {
        public int Version => 2;
        public string Name => "TestMigration002";
        public string UpSql => "CREATE TABLE test_table_2 (id INTEGER PRIMARY KEY);";
    }

    private sealed class FailingMigration : IMigration
    {
        public int Version => 999;
        public string Name => "FailingMigration";
        public string UpSql => "INVALID SQL THAT WILL FAIL;";
    }

    /// <summary>
    /// TEST-01-01: MigrationRunner creates schema_migrations table on first run
    /// </summary>
    [Fact]
    [Trait("TaskId", "T-01")]
    [Trait("TestId", "TEST-01-01")]
    public async Task RunAsync_CreatesSchemaMigrationsTable()
    {
        // Arrange
        await using InMemoryConnectionFactory factory = new();
        Mock<ILogger<MigrationRunner>> loggerMock = new();
        MigrationRunner runner = new(factory, loggerMock.Object);

        // Act
        await runner.RunAsync(Array.Empty<IMigration>(), CancellationToken.None);

        // Assert: schema_migrations table should exist
        await using SqliteConnection conn = await factory.OpenAsync();
        string sql = "SELECT name FROM sqlite_master WHERE type='table' AND name='schema_migrations'";
        string? tableName = await conn.QuerySingleOrDefaultAsync<string>(sql);

        Assert.NotNull(tableName);
        Assert.Equal("schema_migrations", tableName);
    }

    /// <summary>
    /// TEST-01-02: MigrationRunner applies migrations in version order
    /// </summary>
    [Fact]
    [Trait("TaskId", "T-01")]
    [Trait("TestId", "TEST-01-02")]
    public async Task RunAsync_AppliesMigrationsInOrder()
    {
        // Arrange
        await using InMemoryConnectionFactory factory = new();
        Mock<ILogger<MigrationRunner>> loggerMock = new();
        MigrationRunner runner = new(factory, loggerMock.Object);

        // Migrations in reverse order (should be sorted by Version)
        IMigration[] migrations = new IMigration[]
        {
            new TestMigration002(),
            new TestMigration001()
        };

        // Act
        await runner.RunAsync(migrations, CancellationToken.None);

        // Assert: Both tables should exist
        await using SqliteConnection conn = await factory.OpenAsync();

        string? table1 = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='test_table_1'");
        string? table2 = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name='test_table_2'");

        Assert.NotNull(table1);
        Assert.NotNull(table2);

        // Assert: Migrations recorded in correct order
        int[] versions = (await conn.QueryAsync<int>(
            "SELECT version FROM schema_migrations ORDER BY version")).ToArray();

        Assert.Equal(2, versions.Length);
        Assert.Equal(1, versions[0]);
        Assert.Equal(2, versions[1]);
    }

    /// <summary>
    /// TEST-01-03: MigrationRunner is idempotent (skips already-applied migrations)
    /// </summary>
    [Fact]
    [Trait("TaskId", "T-01")]
    [Trait("TestId", "TEST-01-03")]
    public async Task RunAsync_SkipsAlreadyAppliedMigrations()
    {
        // Arrange
        await using InMemoryConnectionFactory factory = new();
        Mock<ILogger<MigrationRunner>> loggerMock = new();
        MigrationRunner runner = new(factory, loggerMock.Object);

        IMigration[] migrations = new IMigration[]
        {
            new TestMigration001(),
            new TestMigration002()
        };

        // Act: Run migrations twice
        await runner.RunAsync(migrations, CancellationToken.None);
        await runner.RunAsync(migrations, CancellationToken.None);  // Should skip both

        // Assert: Only 2 migrations recorded (not 4)
        await using SqliteConnection conn = await factory.OpenAsync();
        int count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM schema_migrations");

        Assert.Equal(2, count);
    }

    /// <summary>
    /// TEST-01-04: MigrationRunner rolls back failed migrations
    /// </summary>
    [Fact]
    [Trait("TaskId", "T-01")]
    [Trait("TestId", "TEST-01-04")]
    public async Task RunAsync_RollsBackFailedMigration()
    {
        // Arrange
        await using InMemoryConnectionFactory factory = new();
        Mock<ILogger<MigrationRunner>> loggerMock = new();
        MigrationRunner runner = new(factory, loggerMock.Object);

        IMigration[] migrations = new IMigration[]
        {
            new TestMigration001(),
            new FailingMigration()  // This will fail
        };

        // Act & Assert: Should throw exception
        await Assert.ThrowsAsync<SqliteException>(async () =>
            await runner.RunAsync(migrations, CancellationToken.None));

        // Assert: First migration should have succeeded
        await using SqliteConnection conn = await factory.OpenAsync();
        int count = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM schema_migrations WHERE version = 1");
        Assert.Equal(1, count);

        // Assert: Failed migration should NOT be recorded
        int failedCount = await conn.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM schema_migrations WHERE version = 999");
        Assert.Equal(0, failedCount);
    }

    /// <summary>
    /// TEST-01-05: MigrationRunner handles empty migration list gracefully
    /// </summary>
    [Fact]
    [Trait("TaskId", "T-01")]
    [Trait("TestId", "TEST-01-05")]
    public async Task RunAsync_HandlesEmptyMigrationList()
    {
        // Arrange
        await using InMemoryConnectionFactory factory = new();
        Mock<ILogger<MigrationRunner>> loggerMock = new();
        MigrationRunner runner = new(factory, loggerMock.Object);

        // Act: Should not throw
        await runner.RunAsync(Array.Empty<IMigration>(), CancellationToken.None);

        // Assert: schema_migrations table should still be created
        await using SqliteConnection conn = await factory.OpenAsync();
        string sql = "SELECT name FROM sqlite_master WHERE type='table' AND name='schema_migrations'";
        string? tableName = await conn.QuerySingleOrDefaultAsync<string>(sql);

        Assert.NotNull(tableName);
    }

    /// <summary>
    /// TEST-01-06: MigrationRunner records migration metadata correctly
    /// </summary>
    [Fact]
    [Trait("TaskId", "T-01")]
    [Trait("TestId", "TEST-01-06")]
    public async Task RunAsync_RecordsMigrationMetadata()
    {
        // Arrange
        await using InMemoryConnectionFactory factory = new();
        Mock<ILogger<MigrationRunner>> loggerMock = new();
        MigrationRunner runner = new(factory, loggerMock.Object);

        IMigration[] migrations = new IMigration[] { new TestMigration001() };

        // Act
        await runner.RunAsync(migrations, CancellationToken.None);

        // Assert: Check migration metadata
        await using SqliteConnection conn = await factory.OpenAsync();
        dynamic? record = await conn.QuerySingleOrDefaultAsync(
            "SELECT version, name, applied_at FROM schema_migrations WHERE version = 1");

        Assert.NotNull(record);
        Assert.Equal(1, record!.version);
        Assert.Equal("TestMigration001", record.name);
        Assert.NotNull(record.applied_at);  // Should have timestamp
    }
}
