using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OptionsExecutionService.Campaign;
using SharedKernel.Domain;

namespace OptionsExecutionService.Workers;

/// <summary>
/// Background worker that monitors active campaigns and executes entry/exit logic.
/// Checks Open campaigns for entry conditions and Active campaigns for exit conditions.
/// Runs on configurable interval (default: 60 seconds).
/// </summary>
public sealed class CampaignMonitorWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CampaignMonitorWorker> _logger;
    private readonly int _intervalSeconds;

    public CampaignMonitorWorker(
        IServiceProvider serviceProvider,
        ILogger<CampaignMonitorWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _intervalSeconds = configuration.GetValue<int>("Campaign:MonitorIntervalSeconds", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Worker} started. Interval={Interval}s",
            nameof(CampaignMonitorWorker), _intervalSeconds);

        // Wait 30 seconds before first run to allow IBKR connection to establish
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunMonitorCycleAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken)
                      .ConfigureAwait(false);
        }

        _logger.LogInformation("{Worker} shutting down", nameof(CampaignMonitorWorker));
    }

    private async Task RunMonitorCycleAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Running campaign monitor cycle");

            // Create a scope for scoped services (ICampaignManager, repositories)
            using IServiceScope scope = _serviceProvider.CreateScope();
            ICampaignManager campaignManager = scope.ServiceProvider.GetRequiredService<ICampaignManager>();

            // STEP 1: Check Open campaigns for entry conditions
            IReadOnlyList<Campaign.Campaign> openCampaigns =
                await campaignManager.GetCampaignsByStateAsync(CampaignState.Open, ct);

            _logger.LogDebug("Found {Count} Open campaigns to check for entry", openCampaigns.Count);

            foreach (Campaign.Campaign campaign in openCampaigns)
            {
                try
                {
                    bool entered = await campaignManager.CheckAndExecuteEntryAsync(campaign.CampaignId, ct);
                    if (entered)
                    {
                        _logger.LogInformation(
                            "Campaign {CampaignId} entered successfully. Now in Active state.",
                            campaign.CampaignId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to check entry for campaign {CampaignId}", campaign.CampaignId);
                    // Continue with other campaigns - do not let one failure block others
                }
            }

            // STEP 2: Check Active campaigns for exit conditions
            IReadOnlyList<Campaign.Campaign> activeCampaigns =
                await campaignManager.GetCampaignsByStateAsync(CampaignState.Active, ct);

            _logger.LogDebug("Found {Count} Active campaigns to check for exit", activeCampaigns.Count);

            foreach (Campaign.Campaign campaign in activeCampaigns)
            {
                try
                {
                    bool exited = await campaignManager.CheckAndExecuteExitAsync(campaign.CampaignId, ct);
                    if (exited)
                    {
                        _logger.LogInformation(
                            "Campaign {CampaignId} exited successfully. Now in Closed state.",
                            campaign.CampaignId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to check exit for campaign {CampaignId}", campaign.CampaignId);
                    // Continue with other campaigns
                }
            }

            _logger.LogDebug("Campaign monitor cycle completed. Open={Open} Active={Active}",
                openCampaigns.Count, activeCampaigns.Count);
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress - do not log
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} cycle failed. Will retry in {Interval}s",
                nameof(CampaignMonitorWorker), _intervalSeconds);
            // Do NOT rethrow - worker must survive errors and retry on next cycle
        }
    }
}
