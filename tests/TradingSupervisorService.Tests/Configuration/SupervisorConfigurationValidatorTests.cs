using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Configuration;
using TradingSupervisorService.Configuration;
using Xunit;

namespace TradingSupervisorService.Tests.Configuration;

/// <summary>
/// Unit tests for SupervisorConfigurationValidator.
/// Tests all validation rules for configuration sections.
/// </summary>
public sealed class SupervisorConfigurationValidatorTests
{
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static SupervisorConfigurationValidator CreateValidator(Dictionary<string, string?> values)
    {
        IConfiguration config = CreateConfiguration(values);
        return new SupervisorConfigurationValidator(config, NullLogger<SupervisorConfigurationValidator>.Instance);
    }

    [Fact]
    public void Validate_WithValidPaperConfiguration_ReturnsSuccess()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1",
            ["Monitoring:IntervalSeconds"] = "60",
            ["Monitoring:CpuThresholdPercent"] = "80",
            ["Monitoring:RamThresholdPercent"] = "85",
            ["Monitoring:DiskThresholdGb"] = "5",
            ["OutboxSync:IntervalSeconds"] = "30",
            ["OutboxSync:BatchSize"] = "50",
            ["OutboxSync:MaxRetries"] = "10",
            ["Cloudflare:WorkerUrl"] = "https://trading-alerts.workers.dev",
            ["Cloudflare:ApiKey"] = "test-key",
            ["Telegram:Enabled"] = "false",
            ["LogReader:OptionsServiceLogPath"] = "logs/options.log",
            ["LogReader:IntervalSeconds"] = "30",
            ["IvtsMonitor:Enabled"] = "false"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.CriticalErrors);
    }

    [Fact]
    public void Validate_WithMissingTradingMode_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("TradingMode is required"));
    }

    [Fact]
    public void Validate_WithLiveTradingMode_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "live",  // CRITICAL SAFETY VIOLATION
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("live") && e.Contains("not allowed"));
    }

    [Fact]
    public void Validate_WithInvalidTradingMode_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "demo",  // Invalid value
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("must be 'paper' or 'live'"));
    }

    [Fact]
    public void Validate_WithMissingDatabasePath_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("SupervisorDbPath is required"));
    }

    [Fact]
    public void Validate_WithInvalidPortNumbers_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "99999",  // Invalid port
            ["IBKR:LivePort"] = "-1",      // Invalid port
            ["IBKR:ClientId"] = "1"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("PaperPort"));
        Assert.Contains(result.CriticalErrors, e => e.Contains("LivePort"));
    }

    [Fact]
    public void Validate_WithNegativeClientId_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "-5"  // Invalid client ID
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("ClientId must be non-negative"));
    }

    [Fact]
    public void Validate_WithMissingIbkrHost_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("IBKR:Host is required"));
    }

    [Fact]
    public void Validate_WithTelegramEnabledButMissingConfig_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1",
            ["Telegram:Enabled"] = "true",
            ["Telegram:ChatId"] = "0"  // Invalid chat ID
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);  // Telegram is non-critical
        Assert.Contains(result.Warnings, w => w.Contains("BotToken"));
        Assert.Contains(result.Warnings, w => w.Contains("ChatId"));
    }

    [Fact]
    public void Validate_WithInvalidCloudflareUrl_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1",
            ["Cloudflare:WorkerUrl"] = "not-a-valid-url",
            ["Cloudflare:ApiKey"] = "test-key"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);  // Cloudflare is non-critical
        Assert.Contains(result.Warnings, w => w.Contains("not a valid HTTP/HTTPS URL"));
    }

    [Fact]
    public void Validate_WithIvtsMonitorEnabledButMissingSymbol_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1",
            ["IvtsMonitor:Enabled"] = "true",
            ["IvtsMonitor:IntervalSeconds"] = "900"
            // Missing Symbol
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);  // IVTS monitor is non-critical
        Assert.Contains(result.Warnings, w => w.Contains("Symbol is not configured"));
    }

    [Fact]
    public void Validate_WithNonStandardPaperPort_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:SupervisorDbPath"] = "data/supervisor.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "5000",  // Non-standard but valid
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "1"
        };

        SupervisorConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("PaperPort") && w.Contains("Standard paper ports"));
    }
}
