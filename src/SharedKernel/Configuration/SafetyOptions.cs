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
    /// Absolute percentage drawdown (today vs yesterday close) that triggers
    /// an automatic trading-paused flag. Compared against |pnlPct|, so a +2.1%
    /// intraday rally won't trigger it (only losses actually halt trading
    /// in the <c>DailyPnLWatcher</c>, but we store the magnitude here so the
    /// watcher can apply its own sign convention). Default: 2.0 (2%).
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
