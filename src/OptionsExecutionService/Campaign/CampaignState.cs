namespace OptionsExecutionService.Campaign;

/// <summary>
/// Lifecycle states for a trading campaign.
/// State transitions: Open → Active → PendingExit → Closed
/// </summary>
public enum CampaignState
{
    /// <summary>
    /// Campaign is created and configured but no positions opened yet.
    /// Waiting for entry conditions to be met.
    /// </summary>
    Open = 0,

    /// <summary>
    /// Campaign has active positions. Monitoring for exit conditions.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Campaign is closed. All positions exited. Final P&amp;L recorded.
    /// Terminal state - no further transitions allowed.
    /// </summary>
    Closed = 2,

    /// <summary>
    /// Campaign exit orders have been placed but not yet fully filled.
    /// Waiting for broker to confirm all exit executions.
    /// Intermediate state between Active and Closed.
    /// Phase 1: State Persistence & Idempotency
    /// </summary>
    PendingExit = 3
}
