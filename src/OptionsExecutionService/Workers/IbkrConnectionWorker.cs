using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SharedKernel.Ibkr;

namespace OptionsExecutionService.Workers;

/// <summary>
/// Background worker that maintains IBKR connection and handles reconnection.
/// Ensures IBKR client stays connected throughout service lifetime.
/// </summary>
public sealed class IbkrConnectionWorker : BackgroundService
{
    private readonly IIbkrClient _ibkrClient;
    private readonly ILogger<IbkrConnectionWorker> _logger;
    private readonly int _keepaliveIntervalSeconds;

    public IbkrConnectionWorker(
        IIbkrClient ibkrClient,
        ILogger<IbkrConnectionWorker> logger,
        IConfiguration configuration)
    {
        _ibkrClient = ibkrClient ?? throw new ArgumentNullException(nameof(ibkrClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _keepaliveIntervalSeconds = configuration.GetValue<int>("IBKR:KeepaliveIntervalSeconds", 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Worker} started. Keepalive interval={Interval}s",
            nameof(IbkrConnectionWorker), _keepaliveIntervalSeconds);

        try
        {
            // Initial connection attempt
            _logger.LogInformation("Connecting to IBKR...");
            await _ibkrClient.ConnectAsync(stoppingToken);
            _logger.LogInformation("IBKR connection established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish initial IBKR connection. Will retry in background.");
            // Do not throw - let the keepalive loop attempt reconnection
        }

        // Keepalive loop
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunKeepaliveAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(_keepaliveIntervalSeconds), stoppingToken)
                      .ConfigureAwait(false);
        }

        // Graceful shutdown
        _logger.LogInformation("{Worker} shutting down. Disconnecting from IBKR...", nameof(IbkrConnectionWorker));
        try
        {
            await _ibkrClient.DisconnectAsync();
            _logger.LogInformation("IBKR disconnected cleanly");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during IBKR disconnect");
        }
    }

    private async Task RunKeepaliveAsync(CancellationToken ct)
    {
        try
        {
            // Check connection state
            if (!_ibkrClient.IsConnected)
            {
                _logger.LogWarning("IBKR connection lost. Attempting reconnection...");
                await _ibkrClient.ConnectAsync(ct);
                _logger.LogInformation("IBKR reconnected successfully");
                return;
            }

            // Send keepalive request
            _logger.LogDebug("Sending IBKR keepalive request");
            _ibkrClient.RequestCurrentTime();
        }
        catch (OperationCanceledException)
        {
            // Shutdown in progress - do not log
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Worker} keepalive failed. Will retry in {Interval}s",
                nameof(IbkrConnectionWorker), _keepaliveIntervalSeconds);
            // Do NOT rethrow - worker must survive errors and retry on next cycle
        }
    }
}
