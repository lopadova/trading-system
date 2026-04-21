namespace SharedKernel.Safety;

/// <summary>
/// Key-value store for cross-cutting safety flags (e.g. <c>trading_paused</c>,
/// <c>semaphore_override_active</c>). Backed by SQLite so flags survive a
/// service restart — a trading halt set by <c>DailyPnLWatcher</c> must NOT be
/// reset by the operator re-deploying the service.
/// <para>
/// Flag semantics (values are free-form strings; common conventions):
/// </para>
/// <list type="bullet">
///   <item><description><c>trading_paused</c>: "1" (halted) / "0" or missing (active).</description></item>
///   <item><description><c>semaphore_override_active</c>: "1" if operator has deliberately bypassed the semaphore gate.</description></item>
/// </list>
/// </summary>
public interface ISafetyFlagStore
{
    /// <summary>
    /// Returns the current value of a flag, or <c>null</c> if not set.
    /// Never throws — returns null on IO failure and logs the error, because
    /// a flag-lookup failure must not take down the caller (often the
    /// order-placement path).
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken ct);

    /// <summary>
    /// Upserts a flag value. Throws on IO failure — unlike
    /// <see cref="GetAsync"/>, a failed write is a real problem that the
    /// caller needs to know about (otherwise a trading-pause might silently
    /// not take effect).
    /// </summary>
    Task SetAsync(string key, string value, CancellationToken ct);

    /// <summary>
    /// Convenience wrapper for boolean flags. Returns true only when the flag
    /// value is exactly "1" (anything else, including missing, is false). This
    /// deliberately strict parse prevents accidental truthy-ness from typos.
    /// </summary>
    Task<bool> IsSetAsync(string key, CancellationToken ct);
}
