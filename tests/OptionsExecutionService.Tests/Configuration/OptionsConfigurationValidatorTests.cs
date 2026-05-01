using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OptionsExecutionService.Configuration;
using SharedKernel.Configuration;
using Xunit;

namespace OptionsExecutionService.Tests.Configuration;

/// <summary>
/// Unit tests for OptionsConfigurationValidator.
/// Tests all validation rules for configuration sections.
/// </summary>
public sealed class OptionsConfigurationValidatorTests
{
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static OptionsConfigurationValidator CreateValidator(Dictionary<string, string?> values)
    {
        IConfiguration config = CreateConfiguration(values);
        return new OptionsConfigurationValidator(config, NullLogger<OptionsConfigurationValidator>.Instance);
    }

    [Fact]
    public void Validate_WithValidPaperConfiguration_ReturnsSuccess()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["IBKR:ReconnectInitialDelaySeconds"] = "5",
            ["IBKR:ReconnectMaxDelaySeconds"] = "300",
            ["IBKR:MaxReconnectAttempts"] = "0",
            ["IBKR:ConnectionTimeoutSeconds"] = "10",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",  // 5% as fraction
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120",
            ["Strategy:FilePath"] = "strategies/private/current.json",
            ["Strategy:ReloadIntervalSeconds"] = "300",
            ["Execution:MaxConcurrentOrders"] = "5",
            ["Execution:OrderTimeoutSeconds"] = "30",
            ["Campaign:MonitorIntervalSeconds"] = "60"
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.CriticalErrors);
    }

    [Fact]
    public void Validate_WithLiveTradingMode_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "live",  // CRITICAL SAFETY VIOLATION
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",  // 5% as fraction
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120",
            ["Strategy:FilePath"] = "strategies/current.json"
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("CRITICAL safety violation"));
    }

    [Fact]
    public void Validate_WithLivePortAsPaperPort_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4001",  // LIVE PORT - CRITICAL SAFETY VIOLATION
            ["IBKR:LivePort"] = "4002",
            ["IBKR:ClientId"] = "2",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",  // 5% as fraction
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120",
            ["Strategy:FilePath"] = "strategies/current.json"
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e =>
            e.Contains("PaperPort") && e.Contains("LIVE trading port") && e.Contains("CRITICAL"));
    }

    [Fact]
    public void Validate_WithInvalidSafetyLimits_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["Safety:MaxPositionSize"] = "-10",  // Invalid
            ["Safety:MaxPositionValueUsd"] = "0",  // Invalid
            ["Safety:MinAccountBalanceUsd"] = "-5000",  // Invalid
            ["Safety:MaxPositionPctOfAccount"] = "1.5",  // Invalid (> 1.0)
            ["Safety:CircuitBreakerFailureThreshold"] = "0",  // Invalid
            ["Safety:CircuitBreakerWindowMinutes"] = "-60",  // Invalid
            ["Safety:CircuitBreakerCooldownMinutes"] = "0",  // Invalid
            ["Strategy:FilePath"] = "strategies/current.json"
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("MaxPositionSize must be positive"));
        Assert.Contains(result.CriticalErrors, e => e.Contains("MaxPositionValueUsd must be positive"));
        Assert.Contains(result.CriticalErrors, e => e.Contains("MinAccountBalanceUsd must be non-negative"));
        Assert.Contains(result.CriticalErrors, e => e.Contains("MaxPositionPctOfAccount must be between 0 and 1"));
        Assert.Contains(result.CriticalErrors, e => e.Contains("CircuitBreakerFailureThreshold must be positive"));
        Assert.Contains(result.CriticalErrors, e => e.Contains("CircuitBreakerWindowMinutes must be positive"));
        Assert.Contains(result.CriticalErrors, e => e.Contains("CircuitBreakerCooldownMinutes must be positive"));
    }

    [Fact]
    public void Validate_WithMissingStrategyFilePath_ReturnsError()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",  // 5% as fraction
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120"
            // Missing Strategy:FilePath
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.CriticalErrors, e => e.Contains("Strategy:FilePath is required"));
    }

    [Fact]
    public void Validate_WithHighRiskPercent_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.15",  // 15% - High risk warning
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120",
            ["Strategy:FilePath"] = "strategies/current.json"
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("MaxPositionPctOfAccount") && w.Contains("very high"));
    }

    [Fact]
    public void Validate_WithReconnectMaxDelayLessThanInitialDelay_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["IBKR:ReconnectInitialDelaySeconds"] = "100",
            ["IBKR:ReconnectMaxDelaySeconds"] = "50",  // Less than initial - warning
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",  // 5% as fraction
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120",
            ["Strategy:FilePath"] = "strategies/current.json"
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("ReconnectMaxDelaySeconds"));
    }

    [Fact]
    public void Validate_WithCircuitBreakerResetLessThanWindow_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",  // 5% as fraction
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "120",
            ["Safety:CircuitBreakerCooldownMinutes"] = "60",  // Less than window - warning
            ["Strategy:FilePath"] = "strategies/current.json"
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w =>
            w.Contains("CircuitBreakerCooldownMinutes") && w.Contains("CircuitBreakerWindowMinutes"));
    }

    [Fact]
    public void Validate_WithLowStrategyReloadInterval_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",  // 5% as fraction
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120",
            ["Strategy:FilePath"] = "strategies/current.json",
            ["Strategy:ReloadIntervalSeconds"] = "10"  // Very low - warning
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("ReloadIntervalSeconds") && w.Contains("very low"));
    }

    [Fact]
    public void Validate_WithHighConcurrentOrders_ReturnsWarning()
    {
        // Arrange
        Dictionary<string, string?> config = new()
        {
            ["TradingMode"] = "paper",
            ["Sqlite:OptionsDbPath"] = "data/options-execution.db",
            ["IBKR:Host"] = "127.0.0.1",
            ["IBKR:PaperPort"] = "4002",
            ["IBKR:LivePort"] = "4001",
            ["IBKR:ClientId"] = "2",
            ["Safety:MaxPositionSize"] = "10",
            ["Safety:MaxPositionValueUsd"] = "50000",
            ["Safety:MinAccountBalanceUsd"] = "10000",
            ["Safety:MaxPositionPctOfAccount"] = "0.05",  // 5% as fraction
            ["Safety:CircuitBreakerFailureThreshold"] = "3",
            ["Safety:CircuitBreakerWindowMinutes"] = "60",
            ["Safety:CircuitBreakerCooldownMinutes"] = "120",
            ["Strategy:FilePath"] = "strategies/current.json",
            ["Execution:MaxConcurrentOrders"] = "50",  // Very high - warning
            ["Execution:OrderTimeoutSeconds"] = "30"
        };

        OptionsConfigurationValidator validator = CreateValidator(config);

        // Act
        ValidationResult result = validator.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("MaxConcurrentOrders") && w.Contains("very high"));
    }
}
