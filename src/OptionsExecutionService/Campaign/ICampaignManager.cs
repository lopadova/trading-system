namespace OptionsExecutionService.Campaign;

using SharedKernel.Domain;

/// <summary>
/// High-level service for managing campaign lifecycle.
/// Orchestrates strategy loading, order placement, monitoring, and exit.
/// </summary>
public interface ICampaignManager
{
    /// <summary>
    /// Creates a new campaign from a strategy file and starts monitoring for entry conditions.
    /// Campaign starts in Open state.
    /// </summary>
    /// <param name="strategyFilePath">Path to strategy JSON file</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Campaign ID</returns>
    Task<string> CreateCampaignAsync(string strategyFilePath, CancellationToken ct = default);

    /// <summary>
    /// Checks entry conditions for an Open campaign and places orders if conditions are met.
    /// Transitions campaign to Active state if orders are placed successfully.
    /// Returns true if campaign was activated, false if entry conditions not met.
    /// </summary>
    /// <param name="campaignId">Campaign identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task<bool> CheckAndExecuteEntryAsync(string campaignId, CancellationToken ct = default);

    /// <summary>
    /// Monitors an Active campaign and checks exit conditions.
    /// Closes positions and transitions to Closed state if any exit condition is met.
    /// Returns true if campaign was closed, false if still active.
    /// </summary>
    /// <param name="campaignId">Campaign identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task<bool> CheckAndExecuteExitAsync(string campaignId, CancellationToken ct = default);

    /// <summary>
    /// Manually closes a campaign (emergency stop).
    /// Closes all positions immediately regardless of exit conditions.
    /// </summary>
    /// <param name="campaignId">Campaign identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task CloseCampaignAsync(string campaignId, CancellationToken ct = default);

    /// <summary>
    /// Gets current campaign status.
    /// </summary>
    /// <param name="campaignId">Campaign identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task<Campaign?> GetCampaignAsync(string campaignId, CancellationToken ct = default);

    /// <summary>
    /// Gets all campaigns in a specific state.
    /// </summary>
    /// <param name="state">Campaign state filter</param>
    /// <param name="ct">Cancellation token</param>
    Task<IReadOnlyList<Campaign>> GetCampaignsByStateAsync(CampaignState state, CancellationToken ct = default);
}
