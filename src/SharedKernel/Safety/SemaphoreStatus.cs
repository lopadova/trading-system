namespace SharedKernel.Safety;

/// <summary>
/// Composite operating-risk indicator mirrored from the Cloudflare Worker's
/// <c>GET /api/risk/semaphore</c> endpoint. The semantics drive the order-gating
/// logic in <c>OptionsExecutionService.Services.SemaphoreGate</c>:
/// <list type="bullet">
///   <item><description><see cref="Green"/> — all sub-indicators green; new orders allowed.</description></item>
///   <item><description><see cref="Orange"/> — caution; hold existing, no new entries (and the default fail-cautious value on transient network errors).</description></item>
///   <item><description><see cref="Red"/> — any sub-indicator red; block new orders outright (and the default fail-closed value when the Worker is unreachable within the IsRed timeout, so a hung Worker can never accidentally approve a trade).</description></item>
/// </list>
/// </summary>
public enum SemaphoreStatus
{
    /// <summary>
    /// Safe to enter new positions. All composite sub-indicators (SPX regime,
    /// VIX level, rolling yield, IVTS) are green.
    /// </summary>
    Green = 0,

    /// <summary>
    /// Caution regime — hold existing positions but do NOT open new ones. Also
    /// the fallback value when the Worker is reachable but returns an
    /// unexpected payload, so we fail cautious rather than closed.
    /// </summary>
    Orange = 1,

    /// <summary>
    /// Stop operativity. At least one sub-indicator is red; new orders MUST be
    /// blocked. Also the fail-closed value when the Worker is unreachable or
    /// returns an auth failure — a broken security posture is not a safe state
    /// to allow trades from.
    /// </summary>
    Red = 2,
}
