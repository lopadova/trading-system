using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SharedKernel.Configuration;

namespace TradingSupervisorService.Configuration;

/// <summary>
/// Validates TradingSupervisorService configuration at startup.
/// Fail-fast on critical errors, log warnings for non-critical issues.
/// </summary>
public sealed class SupervisorConfigurationValidator : IConfigurationValidator
{
    private readonly IConfiguration _config;
    private readonly ILogger<SupervisorConfigurationValidator> _logger;

    public SupervisorConfigurationValidator(
        IConfiguration config,
        ILogger<SupervisorConfigurationValidator> logger)
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

        // Validate Monitoring configuration (warning)
        ValidateMonitoring(warnings);

        // Validate OutboxSync configuration (warning)
        ValidateOutboxSync(warnings);

        // Validate Cloudflare configuration (warning)
        ValidateCloudflare(warnings);

        // Validate Telegram configuration (warning only if enabled)
        ValidateTelegram(warnings);

        // Validate LogReader configuration (warning)
        ValidateLogReader(warnings);

        // Validate IvtsMonitor configuration (warning only if enabled)
        ValidateIvtsMonitor(warnings);

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

        // SAFETY: Warn if live mode is configured (should never be used without explicit authorization)
        if (normalizedMode == "live")
        {
            criticalErrors.Add(
                "TradingMode is set to 'live'. Live trading is not allowed without explicit authorization. " +
                "Change to 'paper' mode.");
        }
    }

    private void ValidateSqlite(List<string> criticalErrors, List<string> warnings)
    {
        string? dbPath = _config["Sqlite:SupervisorDbPath"];

        if (string.IsNullOrWhiteSpace(dbPath))
        {
            criticalErrors.Add("Sqlite:SupervisorDbPath is required but not configured");
            return;
        }

        // Validate path format (basic check - actual directory creation happens at runtime)
        if (dbPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            criticalErrors.Add($"Sqlite:SupervisorDbPath contains invalid characters: '{dbPath}'");
        }

        // Warning if using relative path in current directory (should use data/ subdirectory)
        if (!dbPath.Contains(Path.DirectorySeparatorChar) && !dbPath.Contains(Path.AltDirectorySeparatorChar))
        {
            warnings.Add($"Sqlite:SupervisorDbPath '{dbPath}' is in current directory. " +
                "Consider using 'data/' subdirectory for better organization.");
        }
    }

    private void ValidateIbkr(List<string> criticalErrors, List<string> warnings)
    {
        string? host = _config["IBKR:Host"];
        int paperPort = _config.GetValue<int>("IBKR:PaperPort", 0);
        int livePort = _config.GetValue<int>("IBKR:LivePort", 0);
        int clientId = _config.GetValue<int>("IBKR:ClientId", -1);

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
    }

    private void ValidateMonitoring(List<string> warnings)
    {
        int intervalSeconds = _config.GetValue<int>("Monitoring:IntervalSeconds", 0);
        double cpuThreshold = _config.GetValue<double>("Monitoring:CpuThresholdPercent", 0);
        double ramThreshold = _config.GetValue<double>("Monitoring:RamThresholdPercent", 0);
        double diskThreshold = _config.GetValue<double>("Monitoring:DiskThresholdGb", 0);

        if (intervalSeconds <= 0)
        {
            warnings.Add($"Monitoring:IntervalSeconds should be positive, got {intervalSeconds}. Using default 60.");
        }

        if (cpuThreshold <= 0 || cpuThreshold > 100)
        {
            warnings.Add($"Monitoring:CpuThresholdPercent should be between 0 and 100, got {cpuThreshold}.");
        }

        if (ramThreshold <= 0 || ramThreshold > 100)
        {
            warnings.Add($"Monitoring:RamThresholdPercent should be between 0 and 100, got {ramThreshold}.");
        }

        if (diskThreshold <= 0)
        {
            warnings.Add($"Monitoring:DiskThresholdGb should be positive, got {diskThreshold}.");
        }
    }

    private void ValidateOutboxSync(List<string> warnings)
    {
        int intervalSeconds = _config.GetValue<int>("OutboxSync:IntervalSeconds", 0);
        int batchSize = _config.GetValue<int>("OutboxSync:BatchSize", 0);
        int maxRetries = _config.GetValue<int>("OutboxSync:MaxRetries", 0);

        if (intervalSeconds <= 0)
        {
            warnings.Add($"OutboxSync:IntervalSeconds should be positive, got {intervalSeconds}.");
        }

        if (batchSize <= 0)
        {
            warnings.Add($"OutboxSync:BatchSize should be positive, got {batchSize}.");
        }

        if (maxRetries < 0)
        {
            warnings.Add($"OutboxSync:MaxRetries should be non-negative, got {maxRetries}.");
        }
    }

    private void ValidateCloudflare(List<string> warnings)
    {
        string? workerUrl = _config["Cloudflare:WorkerUrl"];
        string? apiKey = _config["Cloudflare:ApiKey"];

        if (string.IsNullOrWhiteSpace(workerUrl))
        {
            warnings.Add("Cloudflare:WorkerUrl is not configured. Outbox sync will fail.");
        }
        else if (!Uri.TryCreate(workerUrl, UriKind.Absolute, out Uri? uri) ||
                 (uri.Scheme != "https" && uri.Scheme != "http"))
        {
            warnings.Add($"Cloudflare:WorkerUrl '{workerUrl}' is not a valid HTTP/HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            warnings.Add("Cloudflare:ApiKey is not configured. Outbox sync will fail authentication.");
        }
    }

    private void ValidateTelegram(List<string> warnings)
    {
        bool enabled = _config.GetValue<bool>("Telegram:Enabled", false);

        // Only validate if Telegram is enabled
        if (!enabled)
        {
            return;
        }

        string? botToken = _config["Telegram:BotToken"];
        long chatId = _config.GetValue<long>("Telegram:ChatId", 0);
        int maxRetries = _config.GetValue<int>("Telegram:MaxRetryAttempts", 0);
        int retryDelay = _config.GetValue<int>("Telegram:RetryDelaySeconds", 0);
        int maxMessagesPerMinute = _config.GetValue<int>("Telegram:MaxMessagesPerMinute", 0);

        if (string.IsNullOrWhiteSpace(botToken))
        {
            warnings.Add("Telegram:Enabled is true but Telegram:BotToken is not configured. Alerts will fail.");
        }

        if (chatId == 0)
        {
            warnings.Add("Telegram:Enabled is true but Telegram:ChatId is 0. This is likely invalid. Alerts will fail.");
        }

        if (maxRetries < 0)
        {
            warnings.Add($"Telegram:MaxRetryAttempts should be non-negative, got {maxRetries}.");
        }

        if (retryDelay <= 0)
        {
            warnings.Add($"Telegram:RetryDelaySeconds should be positive, got {retryDelay}.");
        }

        if (maxMessagesPerMinute <= 0)
        {
            warnings.Add($"Telegram:MaxMessagesPerMinute should be positive, got {maxMessagesPerMinute}.");
        }
    }

    private void ValidateLogReader(List<string> warnings)
    {
        string? logPath = _config["LogReader:OptionsServiceLogPath"];
        int intervalSeconds = _config.GetValue<int>("LogReader:IntervalSeconds", 0);

        if (string.IsNullOrWhiteSpace(logPath))
        {
            warnings.Add("LogReader:OptionsServiceLogPath is not configured. Log reading will fail.");
        }

        if (intervalSeconds <= 0)
        {
            warnings.Add($"LogReader:IntervalSeconds should be positive, got {intervalSeconds}.");
        }
    }

    private void ValidateIvtsMonitor(List<string> warnings)
    {
        bool enabled = _config.GetValue<bool>("IvtsMonitor:Enabled", false);

        // Only validate if IVTS monitoring is enabled
        if (!enabled)
        {
            return;
        }

        int intervalSeconds = _config.GetValue<int>("IvtsMonitor:IntervalSeconds", 0);
        string? symbol = _config["IvtsMonitor:Symbol"];
        double ivrThreshold = _config.GetValue<double>("IvtsMonitor:IvrThresholdPercent", 0);

        if (intervalSeconds <= 0)
        {
            warnings.Add($"IvtsMonitor:IntervalSeconds should be positive, got {intervalSeconds}.");
        }

        if (string.IsNullOrWhiteSpace(symbol))
        {
            warnings.Add("IvtsMonitor:Enabled is true but IvtsMonitor:Symbol is not configured.");
        }

        if (ivrThreshold <= 0 || ivrThreshold > 100)
        {
            warnings.Add($"IvtsMonitor:IvrThresholdPercent should be between 0 and 100, got {ivrThreshold}.");
        }
    }
}
