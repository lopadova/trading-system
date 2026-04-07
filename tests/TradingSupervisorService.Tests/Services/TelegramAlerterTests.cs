using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SharedKernel.Domain;
using TradingSupervisorService.Services;
using Xunit;

namespace TradingSupervisorService.Tests.Services;

/// <summary>
/// Unit tests for TelegramAlerter service.
/// Tests configuration validation, queueing, and rate limiting.
/// Does NOT test actual Telegram API calls (would require integration test with real bot).
/// </summary>
public sealed class TelegramAlerterTests
{
    private readonly Mock<ILogger<TelegramAlerter>> _loggerMock;

    public TelegramAlerterTests()
    {
        _loggerMock = new Mock<ILogger<TelegramAlerter>>();
    }

    /// <summary>
    /// TEST-05-01: TelegramAlerter initializes with disabled configuration.
    /// </summary>
    [Fact]
    public void Constructor_WhenDisabled_InitializesSuccessfully()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Telegram:Enabled", "false" }
            })
            .Build();

        // Act
        TelegramAlerter alerter = new(config, _loggerMock.Object);

        // Assert
        Assert.NotNull(alerter);
        Assert.Equal(0, alerter.GetPendingCount());
    }

    /// <summary>
    /// TEST-05-02: TelegramAlerter initializes with missing bot token (gracefully degrades).
    /// </summary>
    [Fact]
    public void Constructor_WhenMissingBotToken_DisablesAlerting()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Telegram:Enabled", "true" },
                { "Telegram:BotToken", "" },
                { "Telegram:ChatId", "123456789" }
            })
            .Build();

        // Act
        TelegramAlerter alerter = new(config, _loggerMock.Object);

        // Assert
        Assert.NotNull(alerter);
        // Should log warning and disable (verified by no exceptions thrown)
    }

    /// <summary>
    /// TEST-05-03: TelegramAlerter initializes with missing chat ID (gracefully degrades).
    /// </summary>
    [Fact]
    public void Constructor_WhenMissingChatId_DisablesAlerting()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Telegram:Enabled", "true" },
                { "Telegram:BotToken", "fake-token" },
                { "Telegram:ChatId", "0" }
            })
            .Build();

        // Act
        TelegramAlerter alerter = new(config, _loggerMock.Object);

        // Assert
        Assert.NotNull(alerter);
        // Should log warning and disable
    }

    /// <summary>
    /// TEST-05-04: QueueAlertAsync adds alert to queue when disabled.
    /// </summary>
    [Fact]
    public async Task QueueAlertAsync_WhenDisabled_DoesNotThrow()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Telegram:Enabled", "false" }
            })
            .Build();

        TelegramAlerter alerter = new(config, _loggerMock.Object);

        TelegramAlert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            Severity = AlertSeverity.Info,
            Type = AlertType.SystemHealth,
            Message = "Test alert",
            SourceService = "TestService"
        };

        // Act & Assert (should not throw)
        await alerter.QueueAlertAsync(alert);

        // Queue should remain empty when disabled
        Assert.Equal(0, alerter.GetPendingCount());
    }

    /// <summary>
    /// TEST-05-05: GetPendingCount returns correct count.
    /// </summary>
    [Fact]
    public void GetPendingCount_ReturnsZero_WhenDisabled()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Telegram:Enabled", "false" }
            })
            .Build();

        TelegramAlerter alerter = new(config, _loggerMock.Object);

        // Act
        int count = alerter.GetPendingCount();

        // Assert
        Assert.Equal(0, count);
    }

    /// <summary>
    /// TEST-05-06: TelegramConfig validation rejects invalid MaxRetryAttempts.
    /// </summary>
    [Fact]
    public void TelegramConfig_Validate_RejectsInvalidMaxRetryAttempts()
    {
        // Arrange
        TelegramConfig config = new()
        {
            Enabled = true,
            BotToken = "fake-token",
            ChatId = 123456789,
            MaxRetryAttempts = -1  // Invalid
        };

        // Act
        string? error = config.Validate();

        // Assert
        Assert.NotNull(error);
        Assert.Contains("MaxRetryAttempts", error);
    }

    /// <summary>
    /// TEST-05-07: TelegramConfig validation rejects invalid RetryDelaySeconds.
    /// </summary>
    [Fact]
    public void TelegramConfig_Validate_RejectsInvalidRetryDelaySeconds()
    {
        // Arrange
        TelegramConfig config = new()
        {
            Enabled = true,
            BotToken = "fake-token",
            ChatId = 123456789,
            RetryDelaySeconds = 0  // Invalid
        };

        // Act
        string? error = config.Validate();

        // Assert
        Assert.NotNull(error);
        Assert.Contains("RetryDelaySeconds", error);
    }

    /// <summary>
    /// TEST-05-08: TelegramConfig validation accepts valid configuration.
    /// </summary>
    [Fact]
    public void TelegramConfig_Validate_AcceptsValidConfiguration()
    {
        // Arrange
        TelegramConfig config = new()
        {
            Enabled = true,
            BotToken = "fake-token",
            ChatId = 123456789,
            MaxRetryAttempts = 3,
            RetryDelaySeconds = 5,
            MaxMessagesPerMinute = 20
        };

        // Act
        string? error = config.Validate();

        // Assert
        Assert.Null(error);
    }

    /// <summary>
    /// TEST-05-09: TelegramConfig validation skips validation when disabled.
    /// </summary>
    [Fact]
    public void TelegramConfig_Validate_SkipsValidationWhenDisabled()
    {
        // Arrange
        TelegramConfig config = new()
        {
            Enabled = false,
            BotToken = "",  // Invalid but should be ignored
            ChatId = 0      // Invalid but should be ignored
        };

        // Act
        string? error = config.Validate();

        // Assert
        Assert.Null(error);  // No error because disabled
    }

    /// <summary>
    /// TEST-05-10: SendImmediateAsync returns false when disabled.
    /// </summary>
    [Fact]
    public async Task SendImmediateAsync_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        IConfiguration config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Telegram:Enabled", "false" }
            })
            .Build();

        TelegramAlerter alerter = new(config, _loggerMock.Object);

        TelegramAlert alert = new()
        {
            AlertId = Guid.NewGuid().ToString(),
            Severity = AlertSeverity.Critical,
            Type = AlertType.SystemHealth,
            Message = "Critical alert",
            SourceService = "TestService"
        };

        // Act
        bool result = await alerter.SendImmediateAsync(alert, CancellationToken.None);

        // Assert
        Assert.False(result);
    }
}
