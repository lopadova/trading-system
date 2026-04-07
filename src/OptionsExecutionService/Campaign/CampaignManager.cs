namespace OptionsExecutionService.Campaign;

using Microsoft.Extensions.Logging;
using OptionsExecutionService.Orders;
using SharedKernel.Domain;
using SharedKernel.Strategy;

/// <summary>
/// Campaign lifecycle manager.
/// Orchestrates: Load strategy → Check entry conditions → Place orders →
/// Monitor position → Check exit conditions → Close position → Record results.
/// </summary>
public sealed class CampaignManager : ICampaignManager
{
    private readonly IStrategyLoader _strategyLoader;
    private readonly ICampaignRepository _repository;
    private readonly IOrderPlacer _orderPlacer;
    private readonly ILogger<CampaignManager> _logger;

    public CampaignManager(
        IStrategyLoader strategyLoader,
        ICampaignRepository repository,
        IOrderPlacer orderPlacer,
        ILogger<CampaignManager> logger)
    {
        _strategyLoader = strategyLoader;
        _repository = repository;
        _orderPlacer = orderPlacer;
        _logger = logger;
    }

    public async Task<string> CreateCampaignAsync(string strategyFilePath, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(strategyFilePath))
        {
            throw new ArgumentException("Strategy file path cannot be null or empty", nameof(strategyFilePath));
        }

        // Load and validate strategy from JSON file
        _logger.LogInformation("Loading strategy from {FilePath}", strategyFilePath);
        StrategyDefinition strategy = await _strategyLoader.LoadStrategyAsync(strategyFilePath, ct);

        // Create campaign in Open state
        Campaign campaign = Campaign.Create(strategy);

        // Persist campaign
        await _repository.SaveCampaignAsync(campaign, ct);

        _logger.LogInformation(
            "Created campaign {CampaignId} for strategy {StrategyName} in state {State}",
            campaign.CampaignId, strategy.StrategyName, campaign.State);

        return campaign.CampaignId;
    }

    public async Task<bool> CheckAndExecuteEntryAsync(string campaignId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("Campaign ID cannot be null or empty", nameof(campaignId));
        }

        // Load campaign
        Campaign? campaign = await _repository.GetCampaignAsync(campaignId, ct);

        if (campaign == null)
        {
            throw new InvalidOperationException($"Campaign {campaignId} not found");
        }

        // Campaign must be in Open state to enter
        if (campaign.State != CampaignState.Open)
        {
            _logger.LogWarning(
                "Cannot execute entry for campaign {CampaignId} in state {State}. Must be Open.",
                campaignId, campaign.State);
            return false;
        }

        // Check entry conditions
        if (!CheckEntryConditions(campaign))
        {
            _logger.LogDebug(
                "Entry conditions not met for campaign {CampaignId}",
                campaignId);
            return false;
        }

        try
        {
            // Place entry orders
            _logger.LogInformation(
                "Entry conditions met. Placing orders for campaign {CampaignId}",
                campaignId);

            IReadOnlyList<string> positionIds = await _orderPlacer.PlaceEntryOrdersAsync(
                campaignId,
                campaign.Strategy,
                ct);

            _logger.LogInformation(
                "Entry orders placed. Campaign {CampaignId} opened {PositionCount} positions: {PositionIds}",
                campaignId, positionIds.Count, string.Join(", ", positionIds));

            // Transition campaign to Active state
            Campaign activeCampaign = campaign.Activate();
            await _repository.SaveCampaignAsync(activeCampaign, ct);

            _logger.LogInformation(
                "Campaign {CampaignId} transitioned to Active state",
                campaignId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to execute entry for campaign {CampaignId}. Campaign remains in Open state.",
                campaignId);
            throw;
        }
    }

    public async Task<bool> CheckAndExecuteExitAsync(string campaignId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("Campaign ID cannot be null or empty", nameof(campaignId));
        }

        // Load campaign
        Campaign? campaign = await _repository.GetCampaignAsync(campaignId, ct);

        if (campaign == null)
        {
            throw new InvalidOperationException($"Campaign {campaignId} not found");
        }

        // Campaign must be Active to check exit
        if (campaign.State != CampaignState.Active)
        {
            _logger.LogWarning(
                "Cannot check exit for campaign {CampaignId} in state {State}. Must be Active.",
                campaignId, campaign.State);
            return false;
        }

        // Get current unrealized P&L
        decimal unrealizedPnL = await _orderPlacer.GetUnrealizedPnLAsync(campaignId, ct);

        // Check exit conditions
        (bool shouldExit, string? exitReason) = await CheckExitConditionsAsync(campaign, unrealizedPnL, ct);

        if (!shouldExit)
        {
            _logger.LogDebug(
                "Exit conditions not met for campaign {CampaignId}. UnrealizedPnL={UnrealizedPnL}",
                campaignId, unrealizedPnL);
            return false;
        }

        try
        {
            // Close positions
            _logger.LogInformation(
                "Exit condition met: {ExitReason}. Closing positions for campaign {CampaignId}",
                exitReason, campaignId);

            decimal realizedPnL = await _orderPlacer.ClosePositionsAsync(campaignId, ct);

            _logger.LogInformation(
                "Positions closed for campaign {CampaignId}. RealizedPnL={RealizedPnL}",
                campaignId, realizedPnL);

            // Transition campaign to Closed state
            Campaign closedCampaign = campaign.Close(exitReason!, realizedPnL);
            await _repository.SaveCampaignAsync(closedCampaign, ct);

            _logger.LogInformation(
                "Campaign {CampaignId} closed. Reason={Reason}, PnL={PnL}",
                campaignId, exitReason, realizedPnL);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to execute exit for campaign {CampaignId}. Campaign remains in Active state.",
                campaignId);
            throw;
        }
    }

    public async Task CloseCampaignAsync(string campaignId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("Campaign ID cannot be null or empty", nameof(campaignId));
        }

        Campaign? campaign = await _repository.GetCampaignAsync(campaignId, ct);

        if (campaign == null)
        {
            throw new InvalidOperationException($"Campaign {campaignId} not found");
        }

        if (campaign.State == CampaignState.Closed)
        {
            _logger.LogInformation("Campaign {CampaignId} is already closed", campaignId);
            return;
        }

        // Close positions if campaign is Active
        decimal realizedPnL = 0m;

        if (campaign.State == CampaignState.Active)
        {
            _logger.LogInformation("Manually closing positions for campaign {CampaignId}", campaignId);
            realizedPnL = await _orderPlacer.ClosePositionsAsync(campaignId, ct);
        }

        // Close campaign
        Campaign closedCampaign = campaign.Close("manual", realizedPnL);
        await _repository.SaveCampaignAsync(closedCampaign, ct);

        _logger.LogInformation(
            "Campaign {CampaignId} manually closed. RealizedPnL={RealizedPnL}",
            campaignId, realizedPnL);
    }

    public async Task<Campaign?> GetCampaignAsync(string campaignId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(campaignId))
        {
            throw new ArgumentException("Campaign ID cannot be null or empty", nameof(campaignId));
        }

        return await _repository.GetCampaignAsync(campaignId, ct);
    }

    public async Task<IReadOnlyList<Campaign>> GetCampaignsByStateAsync(
        CampaignState state,
        CancellationToken ct = default)
    {
        return await _repository.GetCampaignsByStateAsync(state, ct);
    }

    /// <summary>
    /// Checks if entry conditions are met for a campaign.
    /// Evaluates: time of day, day of week, market conditions (IVRank, DTE).
    /// </summary>
    private bool CheckEntryConditions(Campaign campaign)
    {
        DateTime now = DateTime.UtcNow;
        TimeOnly nowTime = TimeOnly.FromDateTime(now);
        DayOfWeek nowDayOfWeek = now.DayOfWeek;

        EntryRules entry = campaign.Strategy.EntryRules;

        // Check day of week
        if (!entry.Timing.DaysOfWeek.Contains(nowDayOfWeek))
        {
            _logger.LogDebug(
                "Entry condition failed: wrong day of week. Current={Current}, Allowed={Allowed}",
                nowDayOfWeek, string.Join(",", entry.Timing.DaysOfWeek));
            return false;
        }

        // Check time of day window
        if (nowTime < entry.Timing.EntryTimeStart || nowTime > entry.Timing.EntryTimeEnd)
        {
            _logger.LogDebug(
                "Entry condition failed: outside time window. Current={Current}, Window={Start}-{End}",
                nowTime, entry.Timing.EntryTimeStart, entry.Timing.EntryTimeEnd);
            return false;
        }

        // Market conditions (IVRank, DTE) require real-time market data
        // For now, return true (entry allowed). T-18 (Market Data) will implement real checks.
        _logger.LogDebug(
            "Entry conditions met for campaign {CampaignId}. Time and day checks passed.",
            campaign.CampaignId);

        return true;
    }

    /// <summary>
    /// Checks if exit conditions are met for an active campaign.
    /// Evaluates: profit target, stop loss, max days in trade, time-based exit.
    /// Returns (shouldExit, reason).
    /// </summary>
    private async Task<(bool ShouldExit, string? Reason)> CheckExitConditionsAsync(
        Campaign campaign,
        decimal unrealizedPnL,
        CancellationToken ct)
    {
        ExitRules exit = campaign.Strategy.ExitRules;

        // Check profit target (as percentage of entry credit)
        // For simplicity, assume initial credit = CapitalPerPosition (will be refined in T-16)
        decimal initialCredit = campaign.Strategy.Position.CapitalPerPosition;
        decimal profitTargetAmount = initialCredit * exit.ProfitTarget;

        if (unrealizedPnL >= profitTargetAmount)
        {
            return (true, "profit_target");
        }

        // Check stop loss (as negative percentage of entry credit)
        decimal stopLossAmount = -(initialCredit * exit.StopLoss);

        if (unrealizedPnL <= stopLossAmount)
        {
            return (true, "stop_loss");
        }

        // Check max days in trade
        if (campaign.ActivatedAt.HasValue)
        {
            TimeSpan timeInTrade = DateTime.UtcNow - campaign.ActivatedAt.Value;

            if (timeInTrade.TotalDays >= exit.MaxDaysInTrade)
            {
                return (true, "max_days");
            }
        }

        // Check time-based exit (force exit at specific time of day)
        TimeOnly nowTime = TimeOnly.FromDateTime(DateTime.UtcNow);

        if (nowTime >= exit.ExitTimeOfDay)
        {
            return (true, "time_exit");
        }

        // No exit conditions met
        return (false, null);
    }
}
