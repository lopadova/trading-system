using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Domain;
using System.Globalization;
using System.Text.Json;
using TradingSupervisorService.Repositories;

namespace TradingSupervisorService.Workers;

/// <summary>
/// Background service that monitors position Greeks in real-time.
/// Reads active positions with Greeks data from options.db and creates alerts
/// when risk thresholds are breached (high delta, gamma, theta, vega).
/// Runs on a configurable interval (default: 60 seconds).
/// </summary>
public sealed class GreeksMonitorWorker : BackgroundService
{
    private readonly ILogger<GreeksMonitorWorker> _logger;
    private readonly IPositionsRepository _positionsRepo;
    private readonly IAlertRepository _alertRepo;
    private readonly int _intervalSeconds;
    private readonly bool _enabled;

    // Risk thresholds (configurable)
    private readonly double _deltaThreshold;
    private readonly double _gammaThreshold;
    private readonly double _thetaThreshold;  // absolute value (theta is negative)
    private readonly double _vegaThreshold;

    public GreeksMonitorWorker(
        ILogger<GreeksMonitorWorker> logger,
        IPositionsRepository positionsRepo,
        IAlertRepository alertRepo,
        IConfiguration config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _positionsRepo = positionsRepo ?? throw new ArgumentNullException(nameof(positionsRepo));
        _alertRepo = alertRepo ?? throw new ArgumentNullException(nameof(alertRepo));

        // Read configuration with safe defaults
        _enabled = config.GetValue<bool>("GreeksMonitor:Enabled", true);
        _intervalSeconds = config.GetValue<int>("GreeksMonitor:IntervalSeconds", 60);
        _deltaThreshold = config.GetValue<double>("GreeksMonitor:DeltaThreshold", 0.70);  // 70 delta
        _gammaThreshold = config.GetValue<double>("GreeksMonitor:GammaThreshold", 0.05);  // 0.05 gamma
        _thetaThreshold = config.GetValue<double>("GreeksMonitor:ThetaThreshold", 50.0);  // $50/day decay
        _vegaThreshold = config.GetValue<double>("GreeksMonitor:VegaThreshold", 100.0);   // $100 per 1% IV change

        // Validate configuration (negative-first)
        if (_intervalSeconds <= 0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:IntervalSeconds={_intervalSeconds}. Must be > 0.");
        }

        if (_deltaThreshold < 0.0 || _deltaThreshold > 1.0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:DeltaThreshold={_deltaThreshold}. Must be 0.0-1.0.");
        }

        if (_gammaThreshold <= 0.0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:GammaThreshold={_gammaThreshold}. Must be > 0.");
        }

        if (_thetaThreshold <= 0.0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:ThetaThreshold={_thetaThreshold}. Must be > 0.");
        }

        if (_vegaThreshold <= 0.0)
        {
            throw new ArgumentException($"Invalid GreeksMonitor:VegaThreshold={_vegaThreshold}. Must be > 0.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Early return: if Greeks monitoring is disabled, exit immediately
        if (!_enabled)
        {
            _logger.LogInformation("{Worker} is disabled in configuration. Not starting.", nameof(GreeksMonitorWorker));
            return;
        }

        _logger.LogInformation(
            "{Worker} started. Interval={Interval}s, DeltaThreshold={Delta:F2}, GammaThreshold={Gamma:F3}, ThetaThreshold=${Theta:F0}, VegaThreshold=${Vega:F0}",
            nameof(GreeksMonitorWorker), _intervalSeconds, _deltaThreshold, _gammaThreshold, _thetaThreshold, _vegaThreshold);

        // Main loop: monitor Greeks on interval
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleAsync(stoppingToken);

            // Wait for next cycle (with cancellation support)
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown - exit loop
                break;
            }
        }

        _logger.LogInformation("{Worker} stopped", nameof(GreeksMonitorWorker));
    }

    /// <summary>
    /// Executes one monitoring cycle.
    /// Fetches all active positions with Greeks, checks thresholds, creates alerts if needed.
    /// Does NOT throw - any errors are logged and the worker continues to next cycle.
    /// </summary>
    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            // Fetch all active positions with Greeks data
            IReadOnlyList<ActivePositionRecord> positions = await _positionsRepo.GetActivePositionsWithGreeksAsync(ct);

            // Early return: no positions to monitor
            if (positions.Count == 0)
            {
                _logger.LogDebug("{Worker} cycle: no active positions with Greeks data", nameof(GreeksMonitorWorker));
                return;
            }

            _logger.LogDebug("{Worker} cycle: monitoring {Count} positions", nameof(GreeksMonitorWorker), positions.Count);

            // Check each position for risk threshold breaches
            int alertsCreated = 0;

            foreach (ActivePositionRecord position in positions)
            {
                // Check Delta threshold (directional risk)
                if (position.Delta.HasValue && Math.Abs(position.Delta.Value) > _deltaThreshold)
                {
                    await CreateDeltaAlertAsync(position, ct);
                    alertsCreated++;
                }

                // Check Gamma threshold (convexity risk)
                if (position.Gamma.HasValue && position.Gamma.Value > _gammaThreshold)
                {
                    await CreateGammaAlertAsync(position, ct);
                    alertsCreated++;
                }

                // Check Theta threshold (time decay risk)
                // Theta is usually negative, so compare absolute value
                if (position.Theta.HasValue && Math.Abs(position.Theta.Value) > _thetaThreshold)
                {
                    await CreateThetaAlertAsync(position, ct);
                    alertsCreated++;
                }

                // Check Vega threshold (volatility risk)
                if (position.Vega.HasValue && position.Vega.Value > _vegaThreshold)
                {
                    await CreateVegaAlertAsync(position, ct);
                    alertsCreated++;
                }
            }

            if (alertsCreated > 0)
            {
                _logger.LogInformation("{Worker} cycle completed: {AlertCount} alerts created from {PositionCount} positions",
                    nameof(GreeksMonitorWorker), alertsCreated, positions.Count);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown - do not log as error
            _logger.LogDebug("{Worker} cycle cancelled (shutdown requested)", nameof(GreeksMonitorWorker));
        }
        catch (Exception ex)
        {
            // Log error but DO NOT rethrow - worker must survive cycle failures
            _logger.LogError(ex, "{Worker} cycle failed. Will retry in {Interval}s",
                nameof(GreeksMonitorWorker), _intervalSeconds);
        }
    }

    /// <summary>
    /// Creates an alert for high delta (directional risk).
    /// Delta > threshold indicates high directional exposure.
    /// </summary>
    private async Task CreateDeltaAlertAsync(ActivePositionRecord position, CancellationToken ct)
    {
        // Build alert details JSON
        var details = new
        {
            position_id = position.PositionId,
            campaign_id = position.CampaignId,
            symbol = position.Symbol,
            contract_symbol = position.ContractSymbol,
            quantity = position.Quantity,
            delta = position.Delta,
            threshold = _deltaThreshold,
            underlying_price = position.UnderlyingPrice,
            greeks_updated_at = position.GreeksUpdatedAt
        };

        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "GreeksDelta",
            Severity = AlertSeverity.Warning.ToString().ToLowerInvariant(),
            Message = string.Format(CultureInfo.InvariantCulture,
                "High delta risk: position {0} has delta {1:F2} (threshold {2:F2})",
                position.ContractSymbol, position.Delta, _deltaThreshold),
            DetailsJson = JsonSerializer.Serialize(details),
            SourceService = "TradingSupervisorService",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        await _alertRepo.InsertAsync(alert, ct);

        _logger.LogWarning(
            "Delta threshold breach: position {PositionId} ({Symbol}) delta={Delta:F2} > threshold={Threshold:F2}",
            position.PositionId, position.Symbol, position.Delta, _deltaThreshold);
    }

    /// <summary>
    /// Creates an alert for high gamma (convexity risk).
    /// High gamma means delta changes rapidly with underlying price movement.
    /// </summary>
    private async Task CreateGammaAlertAsync(ActivePositionRecord position, CancellationToken ct)
    {
        var details = new
        {
            position_id = position.PositionId,
            campaign_id = position.CampaignId,
            symbol = position.Symbol,
            contract_symbol = position.ContractSymbol,
            quantity = position.Quantity,
            gamma = position.Gamma,
            threshold = _gammaThreshold,
            underlying_price = position.UnderlyingPrice,
            greeks_updated_at = position.GreeksUpdatedAt
        };

        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "GreeksGamma",
            Severity = AlertSeverity.Warning.ToString().ToLowerInvariant(),
            Message = string.Format(CultureInfo.InvariantCulture,
                "High gamma risk: position {0} has gamma {1:F3} (threshold {2:F3})",
                position.ContractSymbol, position.Gamma, _gammaThreshold),
            DetailsJson = JsonSerializer.Serialize(details),
            SourceService = "TradingSupervisorService",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        await _alertRepo.InsertAsync(alert, ct);

        _logger.LogWarning(
            "Gamma threshold breach: position {PositionId} ({Symbol}) gamma={Gamma:F3} > threshold={Threshold:F3}",
            position.PositionId, position.Symbol, position.Gamma, _gammaThreshold);
    }

    /// <summary>
    /// Creates an alert for high theta (time decay risk).
    /// High absolute theta means position loses significant value each day.
    /// </summary>
    private async Task CreateThetaAlertAsync(ActivePositionRecord position, CancellationToken ct)
    {
        var details = new
        {
            position_id = position.PositionId,
            campaign_id = position.CampaignId,
            symbol = position.Symbol,
            contract_symbol = position.ContractSymbol,
            quantity = position.Quantity,
            theta = position.Theta,
            threshold = _thetaThreshold,
            underlying_price = position.UnderlyingPrice,
            greeks_updated_at = position.GreeksUpdatedAt
        };

        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "GreeksTheta",
            Severity = AlertSeverity.Warning.ToString().ToLowerInvariant(),
            Message = string.Format(CultureInfo.InvariantCulture,
                "High theta decay: position {0} has theta ${1:F0}/day (threshold ${2:F0})",
                position.ContractSymbol, position.Theta, _thetaThreshold),
            DetailsJson = JsonSerializer.Serialize(details),
            SourceService = "TradingSupervisorService",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        await _alertRepo.InsertAsync(alert, ct);

        _logger.LogWarning(
            "Theta threshold breach: position {PositionId} ({Symbol}) theta=${Theta:F0}/day > threshold=${Threshold:F0}",
            position.PositionId, position.Symbol, Math.Abs(position.Theta!.Value), _thetaThreshold);
    }

    /// <summary>
    /// Creates an alert for high vega (volatility risk).
    /// High vega means position value is highly sensitive to IV changes.
    /// </summary>
    private async Task CreateVegaAlertAsync(ActivePositionRecord position, CancellationToken ct)
    {
        var details = new
        {
            position_id = position.PositionId,
            campaign_id = position.CampaignId,
            symbol = position.Symbol,
            contract_symbol = position.ContractSymbol,
            quantity = position.Quantity,
            vega = position.Vega,
            threshold = _vegaThreshold,
            implied_volatility = position.ImpliedVolatility,
            underlying_price = position.UnderlyingPrice,
            greeks_updated_at = position.GreeksUpdatedAt
        };

        AlertRecord alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            AlertType = "GreeksVega",
            Severity = AlertSeverity.Warning.ToString().ToLowerInvariant(),
            Message = string.Format(CultureInfo.InvariantCulture,
                "High vega risk: position {0} has vega ${1:F0} (threshold ${2:F0})",
                position.ContractSymbol, position.Vega, _vegaThreshold),
            DetailsJson = JsonSerializer.Serialize(details),
            SourceService = "TradingSupervisorService",
            CreatedAt = DateTime.UtcNow.ToString("O"),
            ResolvedAt = null,
            ResolvedBy = null
        };

        await _alertRepo.InsertAsync(alert, ct);

        _logger.LogWarning(
            "Vega threshold breach: position {PositionId} ({Symbol}) vega=${Vega:F0} > threshold=${Threshold:F0}",
            position.PositionId, position.Symbol, position.Vega, _vegaThreshold);
    }
}
