using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace OptionsExecutionService.Services;

/// <summary>
/// Thread-safe singleton provider for account equity with staleness detection.
/// Phase 2: Shared safety state P1 - Task RM-06
/// </summary>
public sealed class AccountEquityProvider : IAccountEquityProvider
{
    private readonly ILogger<AccountEquityProvider> _logger;
    private readonly int _maxAgeSeconds;

    // Equity cache - lock-protected for thread safety
    private readonly Lock _lock = new();
    private decimal _netLiquidation = 0m;
    private DateTime? _asOfUtc = null;

    public AccountEquityProvider(
        IConfiguration configuration,
        ILogger<AccountEquityProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Read freshness threshold from config (default: 300 seconds = 5 minutes)
        _maxAgeSeconds = configuration?.GetValue<int>("Safety:AccountBalanceMaxAgeSeconds", 300) ?? 300;
    }

    public AccountEquitySnapshot? GetEquity()
    {
        lock (_lock)
        {
            if (_asOfUtc is null)
            {
                // No equity data available
                return null;
            }

            TimeSpan age = DateTime.UtcNow - _asOfUtc.Value;
            bool isStale = age.TotalSeconds > _maxAgeSeconds;

            return new AccountEquitySnapshot
            {
                NetLiquidation = _netLiquidation,
                AsOfUtc = _asOfUtc.Value,
                Age = age,
                IsStale = isStale
            };
        }
    }

    public void UpdateEquity(decimal netLiquidation, DateTime asOfUtc)
    {
        lock (_lock)
        {
            _netLiquidation = netLiquidation;
            _asOfUtc = asOfUtc;

            _logger.LogDebug(
                "Account equity updated: NetLiquidation={NetLiquidation:C}, AsOf={AsOfUtc:O}",
                netLiquidation, asOfUtc);
        }
    }
}
