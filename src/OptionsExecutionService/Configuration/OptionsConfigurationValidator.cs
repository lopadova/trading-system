using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel.Configuration;

namespace OptionsExecutionService.Configuration;

/// <summary>
/// Validates OptionsExecutionService configuration at startup.
/// Fail-fast on critical errors, log warnings for non-critical issues.
/// </summary>
public sealed class OptionsConfigurationValidator : IConfigurationValidator
{
    private readonly IConfiguration _config;
    private readonly ILogger<OptionsConfigurationValidator> _logger;

    public OptionsConfigurationValidator(
        IConfiguration config,
        ILogger<OptionsConfigurationValidator> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Validates all configuration sections.
    /// Returns validation result with critical errors and warnings.
    /// </summary>
    public ValidationResult Validate()
    {
        List<string> criticalErrors = new();
        List<string> warnings = new();

        // Validate TradingMode (critical)
        ValidateTradingMode(criticalErrors);

        // Validate SQLite configuration (critical)
        ValidateSqlite(criticalErrors, warnings);

        // Validate IBKR configuration (critical)
        ValidateIbkr(criticalErrors, warnings);

        // Validate Safety configuration (critical)
        ValidateSafety(criticalErrors, warnings);

        // Validate Strategy configuration (critical)
        ValidateStrategy(criticalErrors, warnings);

        // Validate Execution configuration (warning)
        ValidateExecution(warnings);

        // Validate Campaign configuration (warning)
        ValidateCampaign(warnings);

        // Return result
        if (criticalErrors.Count > 0)
        {
            return ValidationResult.Failure(criticalErrors, warnings);
        }

        if (warnings.Count > 0)
        {
            return ValidationResult.SuccessWithWarnings(warnings.ToArray());
        }

        return ValidationResult.Success();
    }

    private void ValidateTradingMode(List<string> criticalErrors)
    {
        string? tradingMode = _config["TradingMode"];

        if (string.IsNullOrWhiteSpace(tradingMode))
        {
            criticalErrors.Add("TradingMode is required but not configured");
            return;
        }

        // Normalize and validate
        string normalizedMode = tradingMode.Trim().ToLowerInvariant();

        if (normalizedMode != "paper" && normalizedMode != "live")
        {
            criticalErrors.Add($"TradingMode must be 'paper' or 'live', got '{tradingMode}'");
            return;
        }

        // SAFETY: Fail if live mode is configured (should never be used without explicit authorization)
        if (normalizedMode == "live")
        {
            criticalErrors.Add(
                "TradingMode is set to 'live'. Live trading is NOT ALLOWED without explicit authorization. " +
                "This is a CRITICAL safety violation. Change to 'paper' mode immediately.");
        }
    }

    private void ValidateSqlite(List<string> criticalErrors, List<string> warnings)
    {
        string? dbPath = _config["Sqlite:OptionsDbPath"];

        if (string.IsNullOrWhiteSpace(dbPath))
        {
            criticalErrors.Add("Sqlite:OptionsDbPath is required but not configured");
            return;
        }

        // Validate path format (basic check - actual directory creation happens at runtime)
        if (dbPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            criticalErrors.Add($"Sqlite:OptionsDbPath contains invalid characters: '{dbPath}'");
        }

        // Warning if using relative path in current directory (should use data/ subdirectory)
        if (!dbPath.Contains(Path.DirectorySeparatorChar) && !dbPath.Contains(Path.AltDirectorySeparatorChar))
        {
            warnings.Add($"Sqlite:OptionsDbPath '{dbPath}' is in current directory. " +
                "Consider using 'data/' subdirectory for better organization.");
        }
    }

    private void ValidateIbkr(List<string> criticalErrors, List<string> warnings)
    {
        string? host = _config["IBKR:Host"];
        int paperPort = _config.GetValue<int>("IBKR:PaperPort", 0);
        int livePort = _config.GetValue<int>("IBKR:LivePort", 0);
        int clientId = _config.GetValue<int>("IBKR:ClientId", -1);
        int reconnectInitialDelay = _config.GetValue<int>("IBKR:ReconnectInitialDelaySeconds", 0);
        int reconnectMaxDelay = _config.GetValue<int>("IBKR:ReconnectMaxDelaySeconds", 0);
        int maxReconnectAttempts = _config.GetValue<int>("IBKR:MaxReconnectAttempts", -1);
        int connectionTimeout = _config.GetValue<int>("IBKR:ConnectionTimeoutSeconds", 0);

        // Validate host
        if (string.IsNullOrWhiteSpace(host))
        {
            criticalErrors.Add("IBKR:Host is required but not configured");
        }

        // Validate paper port
        if (paperPort <= 0 || paperPort > 65535)
        {
            criticalErrors.Add($"IBKR:PaperPort must be between 1 and 65535, got {paperPort}");
        }

        // Validate live port
        if (livePort <= 0 || livePort > 65535)
        {
            criticalErrors.Add($"IBKR:LivePort must be between 1 and 65535, got {livePort}");
        }

        // SAFETY: Ensure paper port is not a known live port
        if (paperPort == 4001 || paperPort == 7496)
        {
            criticalErrors.Add(
                $"IBKR:PaperPort is set to {paperPort}, which is a LIVE trading port. " +
                "This is a CRITICAL safety violation. Use 4002 (IB Gateway) or 7497 (TWS) for paper trading.");
        }

        // SAFETY: Ensure live port is a known live port (4001 for IB Gateway, 7496 for TWS)
        if (livePort != 4001 && livePort != 7496)
        {
            warnings.Add($"IBKR:LivePort is {livePort}. Standard live ports are 4001 (IB Gateway) or 7496 (TWS).");
        }

        // SAFETY: Ensure paper port is a known paper port (4002 for IB Gateway, 7497 for TWS)
        if (paperPort != 4002 && paperPort != 7497)
        {
            warnings.Add($"IBKR:PaperPort is {paperPort}. Standard paper ports are 4002 (IB Gateway) or 7497 (TWS).");
        }

        // Validate client ID
        if (clientId < 0)
        {
            criticalErrors.Add($"IBKR:ClientId must be non-negative, got {clientId}");
        }

        // Warning if client ID is 0 (valid but unusual)
        if (clientId == 0)
        {
            warnings.Add("IBKR:ClientId is 0. This is valid but unusual. Most connections use ID 1 or higher.");
        }

        // Validate reconnect delays
        if (reconnectInitialDelay <= 0)
        {
            warnings.Add($"IBKR:ReconnectInitialDelaySeconds should be positive, got {reconnectInitialDelay}.");
        }

        if (reconnectMaxDelay < reconnectInitialDelay)
        {
            warnings.Add(
                $"IBKR:ReconnectMaxDelaySeconds ({reconnectMaxDelay}) should be >= " +
                $"ReconnectInitialDelaySeconds ({reconnectInitialDelay}).");
        }

        if (maxReconnectAttempts < 0)
        {
            warnings.Add($"IBKR:MaxReconnectAttempts should be non-negative, got {maxReconnectAttempts}. 0 = infinite retries.");
        }

        if (connectionTimeout <= 0)
        {
            warnings.Add($"IBKR:ConnectionTimeoutSeconds should be positive, got {connectionTimeout}.");
        }
    }

    private void ValidateSafety(List<string> criticalErrors, List<string> warnings)
    {
        int maxPositionSize = _config.GetValue<int>("Safety:MaxPositionSize", 0);
        decimal maxPositionValueUsd = _config.GetValue<decimal>("Safety:MaxPositionValueUsd", 0);
        decimal minAccountBalanceUsd = _config.GetValue<decimal>("Safety:MinAccountBalanceUsd", 0);
        decimal maxPositionPct = _config.GetValue<decimal>("Safety:MaxPositionPctOfAccount", 0);
        int circuitBreakerThreshold = _config.GetValue<int>("Safety:CircuitBreakerFailureThreshold", 0);
        int circuitBreakerWindow = _config.GetValue<int>("Safety:CircuitBreakerWindowMinutes", 0);
        int circuitBreakerCooldown = _config.GetValue<int>("Safety:CircuitBreakerCooldownMinutes", 0);

        // Validate position limits (critical)
        if (maxPositionSize <= 0)
        {
            criticalErrors.Add($"Safety:MaxPositionSize must be positive, got {maxPositionSize}");
        }

        if (maxPositionValueUsd <= 0)
        {
            criticalErrors.Add($"Safety:MaxPositionValueUsd must be positive, got {maxPositionValueUsd}");
        }

        if (minAccountBalanceUsd < 0)
        {
            criticalErrors.Add($"Safety:MinAccountBalanceUsd must be non-negative, got {minAccountBalanceUsd}");
        }

        // Validate risk percentage (critical)
        // Stored as fraction (0-1), where 0.05 = 5%
        if (maxPositionPct <= 0 || maxPositionPct > 1)
        {
            criticalErrors.Add($"Safety:MaxPositionPctOfAccount must be between 0 and 1 (fraction), got {maxPositionPct}. Use 0.05 for 5%.");
        }

        // Warning if risk is very high (>10% per trade = 0.10 fraction)
        if (maxPositionPct > 0.10m)
        {
            warnings.Add(
                $"Safety:MaxPositionPctOfAccount is {maxPositionPct:P0} ({maxPositionPct * 100:F1}%), which is very high. " +
                "Consider lowering to 0.05 (5%) or less for better risk management.");
        }

        // Validate circuit breaker settings (critical)
        if (circuitBreakerThreshold <= 0)
        {
            criticalErrors.Add($"Safety:CircuitBreakerFailureThreshold must be positive, got {circuitBreakerThreshold}");
        }

        if (circuitBreakerWindow <= 0)
        {
            criticalErrors.Add($"Safety:CircuitBreakerWindowMinutes must be positive, got {circuitBreakerWindow}");
        }

        if (circuitBreakerCooldown <= 0)
        {
            criticalErrors.Add($"Safety:CircuitBreakerCooldownMinutes must be positive, got {circuitBreakerCooldown}");
        }

        // Cross-field validation: cooldown should be >= window for safety
        if (circuitBreakerCooldown < circuitBreakerWindow)
        {
            warnings.Add(
                $"Safety:CircuitBreakerCooldownMinutes ({circuitBreakerCooldown}) is less than " +
                $"CircuitBreakerWindowMinutes ({circuitBreakerWindow}). Consider making cooldown >= window.");
        }
    }

    private void ValidateStrategy(List<string> criticalErrors, List<string> warnings)
    {
        string? filePath = _config["Strategy:FilePath"];
        int reloadInterval = _config.GetValue<int>("Strategy:ReloadIntervalSeconds", 0);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            criticalErrors.Add("Strategy:FilePath is required but not configured");
            return;
        }

        // Validate path format
        if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            criticalErrors.Add($"Strategy:FilePath contains invalid characters: '{filePath}'");
        }

        // Warning if reload interval is too low (< 60 seconds = excessive disk I/O)
        if (reloadInterval > 0 && reloadInterval < 60)
        {
            warnings.Add(
                $"Strategy:ReloadIntervalSeconds is {reloadInterval}, which is very low. " +
                "Consider using 300+ seconds to avoid excessive disk I/O.");
        }

        // Warning if reload interval is 0 (disabled)
        if (reloadInterval == 0)
        {
            warnings.Add("Strategy:ReloadIntervalSeconds is 0. Strategy auto-reload is disabled.");
        }
    }

    private void ValidateExecution(List<string> warnings)
    {
        int maxConcurrentOrders = _config.GetValue<int>("Execution:MaxConcurrentOrders", 0);
        int orderTimeout = _config.GetValue<int>("Execution:OrderTimeoutSeconds", 0);

        if (maxConcurrentOrders <= 0)
        {
            warnings.Add($"Execution:MaxConcurrentOrders should be positive, got {maxConcurrentOrders}.");
        }

        if (orderTimeout <= 0)
        {
            warnings.Add($"Execution:OrderTimeoutSeconds should be positive, got {orderTimeout}.");
        }

        // Warning if max concurrent orders is very high (> 20)
        if (maxConcurrentOrders > 20)
        {
            warnings.Add(
                $"Execution:MaxConcurrentOrders is {maxConcurrentOrders}, which is very high. " +
                "Consider lowering to reduce complexity and risk.");
        }
    }

    private void ValidateCampaign(List<string> warnings)
    {
        int monitorInterval = _config.GetValue<int>("Campaign:MonitorIntervalSeconds", 0);

        if (monitorInterval <= 0)
        {
            warnings.Add($"Campaign:MonitorIntervalSeconds should be positive, got {monitorInterval}.");
        }

        // Warning if monitor interval is too low (< 10 seconds = excessive overhead)
        if (monitorInterval > 0 && monitorInterval < 10)
        {
            warnings.Add(
                $"Campaign:MonitorIntervalSeconds is {monitorInterval}, which is very low. " +
                "Consider using 30+ seconds to avoid excessive overhead.");
        }
    }
}
