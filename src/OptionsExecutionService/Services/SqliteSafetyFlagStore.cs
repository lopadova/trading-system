using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SharedKernel.Data;
using SharedKernel.Safety;

namespace OptionsExecutionService.Services;

/// <summary>
/// SQLite-backed implementation of <see cref="ISafetyFlagStore"/>. Flags are
/// persisted to <c>safety_flags</c> (migration 004) so a trading halt survives
/// a service restart — if <c>DailyPnLWatcher</c> pauses trading, the operator
/// must unpause explicitly; a redeploy is NOT a reset mechanism.
/// <para>
/// Error semantics mirror the interface contract:
/// </para>
/// <list type="bullet">
///   <item><description><see cref="GetAsync"/> / <see cref="IsSetAsync"/> swallow IO errors → return null/false (never throws).</description></item>
///   <item><description><see cref="SetAsync"/> re-throws on failure (a pause that didn't stick is worse than a crash).</description></item>
/// </list>
/// </summary>
public sealed class SqliteSafetyFlagStore : ISafetyFlagStore
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<SqliteSafetyFlagStore> _logger;

    public SqliteSafetyFlagStore(
        IDbConnectionFactory db,
        ILogger<SqliteSafetyFlagStore> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("key cannot be null or empty", nameof(key));
        }

        const string sql = "SELECT value FROM safety_flags WHERE key = @Key LIMIT 1";

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct).ConfigureAwait(false);
            CommandDefinition cmd = new(sql, new { Key = key }, cancellationToken: ct);
            string? value = await conn.QuerySingleOrDefaultAsync<string?>(cmd).ConfigureAwait(false);
            return value;
        }
        catch (Exception ex)
        {
            // Contract: never throw from Get. Log + return null so the order-path
            // treats this as "flag not set" — erring toward letting trading flow
            // (the PnL watcher will re-raise the flag on its next tick anyway).
            _logger.LogError(ex, "SqliteSafetyFlagStore.GetAsync failed for key={Key} — returning null", key);
            return null;
        }
    }

    public async Task SetAsync(string key, string value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("key cannot be null or empty", nameof(key));
        }
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        // UPSERT via SQLite's ON CONFLICT syntax (requires 3.24+; bundled Microsoft.Data.Sqlite is newer).
        const string sql = """
            INSERT INTO safety_flags (key, value, updated_at)
            VALUES (@Key, @Value, @UpdatedAt)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_at = excluded.updated_at;
            """;

        try
        {
            await using SqliteConnection conn = await _db.OpenAsync(ct).ConfigureAwait(false);
            CommandDefinition cmd = new(
                sql,
                new { Key = key, Value = value, UpdatedAt = DateTimeOffset.UtcNow.ToString("O") },
                cancellationToken: ct);
            await conn.ExecuteAsync(cmd).ConfigureAwait(false);
            _logger.LogInformation("SafetyFlag set: {Key} = {Value}", key, value);
        }
        catch (Exception ex)
        {
            // Contract: DO throw from Set — an unreliable pause is worse than a crash.
            _logger.LogError(ex, "SqliteSafetyFlagStore.SetAsync failed for key={Key}", key);
            throw;
        }
    }

    public async Task<bool> IsSetAsync(string key, CancellationToken ct)
    {
        string? value = await GetAsync(key, ct).ConfigureAwait(false);
        // Deliberately strict: only the literal "1" counts. Prevents typo-truthy
        // values (e.g. "true", "yes") from silently enabling a halt the operator
        // didn't intend. Keep the check consistent with ISafetyFlagStore contract.
        return value == "1";
    }
}
