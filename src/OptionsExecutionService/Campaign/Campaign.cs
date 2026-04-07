namespace OptionsExecutionService.Campaign;

using SharedKernel.Domain;

/// <summary>
/// Domain entity representing a trading campaign.
/// A campaign is a single instance of a strategy execution with full lifecycle tracking.
/// Immutable record with value semantics.
/// </summary>
public sealed record Campaign
{
    /// <summary>
    /// Unique campaign identifier (GUID).
    /// </summary>
    public required string CampaignId { get; init; }

    /// <summary>
    /// Strategy definition used for this campaign.
    /// </summary>
    public required StrategyDefinition Strategy { get; init; }

    /// <summary>
    /// Current lifecycle state: Open, Active, Closed.
    /// </summary>
    public required CampaignState State { get; init; }

    /// <summary>
    /// Timestamp when campaign was created (UTC).
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when positions were opened and campaign became Active (UTC).
    /// Null if still in Open state.
    /// </summary>
    public DateTime? ActivatedAt { get; init; }

    /// <summary>
    /// Timestamp when campaign was closed (UTC).
    /// Null if not yet closed.
    /// </summary>
    public DateTime? ClosedAt { get; init; }

    /// <summary>
    /// Reason for campaign closure: profit_target, stop_loss, max_days, time_exit, manual, error.
    /// Null if not yet closed.
    /// </summary>
    public string? CloseReason { get; init; }

    /// <summary>
    /// Final realized P&amp;L in USD. Only set when State=Closed.
    /// </summary>
    public decimal? RealizedPnL { get; init; }

    /// <summary>
    /// Strategy-specific state as JSON. Persisted to strategy_state table.
    /// Can store arbitrary data needed by the strategy.
    /// </summary>
    public string? StateJson { get; init; }

    /// <summary>
    /// Creates a new campaign in Open state.
    /// </summary>
    public static Campaign Create(StrategyDefinition strategy)
    {
        return new Campaign
        {
            CampaignId = Guid.NewGuid().ToString(),
            Strategy = strategy,
            State = CampaignState.Open,
            CreatedAt = DateTime.UtcNow,
            ActivatedAt = null,
            ClosedAt = null,
            CloseReason = null,
            RealizedPnL = null,
            StateJson = null
        };
    }

    /// <summary>
    /// Transitions campaign to Active state.
    /// </summary>
    public Campaign Activate()
    {
        if (State != CampaignState.Open)
        {
            throw new InvalidOperationException($"Cannot activate campaign in state {State}. Must be Open.");
        }

        return this with
        {
            State = CampaignState.Active,
            ActivatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Transitions campaign to Closed state with final P&amp;L and reason.
    /// </summary>
    public Campaign Close(string reason, decimal realizedPnL)
    {
        if (State == CampaignState.Closed)
        {
            throw new InvalidOperationException("Campaign is already closed.");
        }

        return this with
        {
            State = CampaignState.Closed,
            ClosedAt = DateTime.UtcNow,
            CloseReason = reason,
            RealizedPnL = realizedPnL
        };
    }

    /// <summary>
    /// Updates campaign state JSON (strategy-specific data).
    /// </summary>
    public Campaign UpdateStateJson(string stateJson)
    {
        return this with { StateJson = stateJson };
    }
}
