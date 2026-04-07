namespace OptionsExecutionService.Campaign;

/// <summary>
/// Repository for persisting and retrieving campaign data.
/// Campaigns are stored in strategy_state table (campaign metadata)
/// and active_positions table (positions belonging to campaign).
/// </summary>
public interface ICampaignRepository
{
    /// <summary>
    /// Saves a campaign (insert or update).
    /// Persists to strategy_state table using campaign_id as key.
    /// </summary>
    Task SaveCampaignAsync(Campaign campaign, CancellationToken ct = default);

    /// <summary>
    /// Loads a campaign by ID.
    /// Returns null if not found.
    /// </summary>
    Task<Campaign?> GetCampaignAsync(string campaignId, CancellationToken ct = default);

    /// <summary>
    /// Loads all campaigns in a specific state.
    /// </summary>
    Task<IReadOnlyList<Campaign>> GetCampaignsByStateAsync(CampaignState state, CancellationToken ct = default);

    /// <summary>
    /// Loads all campaigns for a specific strategy name.
    /// </summary>
    Task<IReadOnlyList<Campaign>> GetCampaignsByStrategyAsync(string strategyName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a campaign (for testing/cleanup). NOT used in production flow.
    /// </summary>
    Task DeleteCampaignAsync(string campaignId, CancellationToken ct = default);
}
