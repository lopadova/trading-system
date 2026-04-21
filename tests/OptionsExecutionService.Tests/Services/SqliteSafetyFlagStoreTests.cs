using Microsoft.Extensions.Logging.Abstractions;
using OptionsExecutionService.Migrations;
using OptionsExecutionService.Services;
using SharedKernel.Data;
using SharedKernel.Safety;
using SharedKernel.Tests.Data;
using Xunit;

namespace OptionsExecutionService.Tests.Services;

/// <summary>
/// Integration tests for <see cref="SqliteSafetyFlagStore"/>. Uses the in-memory
/// SQLite factory with the real migrations applied, so we validate the end-to-end
/// UPSERT path and the semantic of <see cref="ISafetyFlagStore.IsSetAsync"/>.
/// </summary>
public sealed class SqliteSafetyFlagStoreTests : IAsyncDisposable
{
    private readonly InMemoryConnectionFactory _db;
    private readonly SqliteSafetyFlagStore _store;

    public SqliteSafetyFlagStoreTests()
    {
        _db = new InMemoryConnectionFactory();
        MigrationRunner migrationRunner = new(_db, NullLogger<MigrationRunner>.Instance);
        migrationRunner.RunAsync(OptionsMigrations.All, CancellationToken.None).GetAwaiter().GetResult();
        _store = new SqliteSafetyFlagStore(_db, NullLogger<SqliteSafetyFlagStore>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task GetAsync_UnknownKey_ReturnsNull()
    {
        string? v = await _store.GetAsync("not_set", CancellationToken.None);
        Assert.Null(v);
    }

    [Fact]
    public async Task SetAsync_ThenGet_RoundTrip()
    {
        await _store.SetAsync("trading_paused", "1", CancellationToken.None);
        string? v = await _store.GetAsync("trading_paused", CancellationToken.None);
        Assert.Equal("1", v);
    }

    [Fact]
    public async Task SetAsync_Upsert_OverwritesExisting()
    {
        await _store.SetAsync("trading_paused", "1", CancellationToken.None);
        await _store.SetAsync("trading_paused", "0", CancellationToken.None);
        string? v = await _store.GetAsync("trading_paused", CancellationToken.None);
        Assert.Equal("0", v);
    }

    [Fact]
    public async Task IsSetAsync_OnlyTrueForLiteralOne()
    {
        await _store.SetAsync("k1", "1", CancellationToken.None);
        await _store.SetAsync("k2", "0", CancellationToken.None);
        await _store.SetAsync("k3", "yes", CancellationToken.None);
        await _store.SetAsync("k4", "true", CancellationToken.None);

        Assert.True(await _store.IsSetAsync("k1", CancellationToken.None));
        Assert.False(await _store.IsSetAsync("k2", CancellationToken.None));
        Assert.False(await _store.IsSetAsync("k3", CancellationToken.None));
        Assert.False(await _store.IsSetAsync("k4", CancellationToken.None));
        Assert.False(await _store.IsSetAsync("not_set", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_EmptyKey_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _store.GetAsync("", CancellationToken.None));
    }

    [Fact]
    public async Task SetAsync_NullValue_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _store.SetAsync("k", null!, CancellationToken.None));
    }
}
