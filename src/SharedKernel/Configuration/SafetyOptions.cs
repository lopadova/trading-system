namespace SharedKernel.Configuration;

/// <summary>
/// Runtime safety knobs bound from the <c>Safety:*</c> configuration section.
/// Kept separate from <see cref="SharedKernel.Domain.OrderSafetyConfig"/> —
/// that record encodes the STATIC per-order validator limits (max size, max
/// value, etc.), whereas this one carries the DYNAMIC system-wide toggles
/// (override flags, circuit-breaker tuning for the daily-PnL watcher).
/// Separation avoids conflating "per-order validator inputs" with "global
/// system posture" and keeps each record focused on one concern.
/// </summary>
public sealed record SafetyOptions
{
    /// <summary>
    /// Convention name for binding from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
    /// </summary>
    public const string SectionName = "Safety";

    /// <summary>
    /// When true, the SemaphoreGate check is bypassed entirely — orders will
    /// be placed even when the composite indicator is RED. This is an
    /// operator-managed escape hatch for scenarios where the semaphore logic
    /// is known-wrong (stale data, mis-tuned thresholds). Default: false.
    /// <para>
    /// WARNING: enabling this in production triggers a CRITICAL-severity
    /// startup banner AND an alert — visibility is deliberately loud so a
    /// forgotten override can't become silent prod state.
    /// </para>
    /// </summary>
    public bool OverrideSemaphore { get; init; } = false;

    /// <summary>
    /// Daily drawdown threshold (today vs yesterday close, LOSS side only)
    /// that triggers an automatic <c>trading_paused</c> flag.
    /// <para>
    /// Stored as a positive magnitude; <c>DailyPnLWatcher</c> applies the
    /// sign convention: only <c>pnlPct &lt;= -MaxDailyDrawdownPct</c>
    /// triggers the pause. A +2.1% intraday rally will NOT trigger it —
    /// "drawdown" in this config is by definition a loss event.
    /// </para>
    /// <para>
    /// Default: 2.0 (meaning "halt if today's loss vs yesterday ≥ 2%").
    /// </para>
    /// </summary>
    public decimal MaxDailyDrawdownPct { get; init; } = 2.0m;

    /// <summary>
    /// How often the <c>DailyPnLWatcher</c> polls the Worker's
    /// <c>/api/performance/today</c> endpoint. Default: 5 minutes. A lower
    /// value catches a rapid drawdown faster but increases Worker load;
    /// 5 minutes is the sweet spot for a daily-granularity metric.
    /// </summary>
    public int PollIntervalSeconds { get; init; } = 300;
}
