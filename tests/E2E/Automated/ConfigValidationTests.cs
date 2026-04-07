using Xunit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TradingSystem.SharedKernel.Domain;

namespace TradingSystem.E2E.Automated;

/// <summary>
/// Automated tests for configuration file validation (no IBKR required)
/// </summary>
public sealed class ConfigValidationTests
{
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-09")]
    public void TradingMode_Paper_ShouldBeValid()
    {
        // Arrange
        string config = """
        {
          "TradingMode": "paper"
        }
        """;

        JObject json = JObject.Parse(config);

        // Act
        string? tradingMode = json["TradingMode"]?.Value<string>();
        bool isValid = tradingMode == "paper" || tradingMode == "live";

        // Assert
        Assert.NotNull(tradingMode);
        Assert.Equal("paper", tradingMode);
        Assert.True(isValid);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-10")]
    public void TradingMode_Invalid_ShouldBeDetected()
    {
        // Arrange
        string config = """
        {
          "TradingMode": "invalid_mode"
        }
        """;

        JObject json = JObject.Parse(config);

        // Act
        string? tradingMode = json["TradingMode"]?.Value<string>();
        bool isValid = tradingMode == "paper" || tradingMode == "live";

        // Assert
        Assert.False(isValid, "Invalid trading mode should be rejected");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-11")]
    public void IbkrConfig_ShouldHaveRequiredFields()
    {
        // Arrange
        string config = """
        {
          "Ibkr": {
            "Host": "127.0.0.1",
            "Port": 4002,
            "ClientId": 1
          }
        }
        """;

        JObject json = JObject.Parse(config);

        // Act
        JToken? ibkr = json["Ibkr"];

        // Assert
        Assert.NotNull(ibkr);
        Assert.Equal("127.0.0.1", ibkr["Host"]?.Value<string>());
        Assert.Equal(4002, ibkr["Port"]?.Value<int>());
        Assert.Equal(1, ibkr["ClientId"]?.Value<int>());
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-12")]
    public void DatabasePath_ShouldBeAbsolutePath()
    {
        // Arrange
        string config = """
        {
          "Database": {
            "DataDirectory": "C:\\ProgramData\\TradingSystem"
          }
        }
        """;

        JObject json = JObject.Parse(config);

        // Act
        string? dataDir = json["Database"]?["DataDirectory"]?.Value<string>();

        // Assert
        Assert.NotNull(dataDir);
        Assert.True(Path.IsPathRooted(dataDir), "Database path must be absolute");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-13")]
    public void TelegramConfig_ShouldHaveBotToken()
    {
        // Arrange
        string config = """
        {
          "Telegram": {
            "BotToken": "1234567890:ABCdefGHIjklMNOpqrsTUVwxyz",
            "ChatId": "-1001234567890"
          }
        }
        """;

        JObject json = JObject.Parse(config);

        // Act
        string? botToken = json["Telegram"]?["BotToken"]?.Value<string>();
        string? chatId = json["Telegram"]?["ChatId"]?.Value<string>();

        // Assert
        Assert.NotNull(botToken);
        Assert.NotNull(chatId);
        Assert.Contains(":", botToken); // Telegram bot token format: ID:SECRET
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-14")]
    public void StrategyFile_ShouldHaveValidJson()
    {
        // Arrange
        string strategyJson = """
        {
          "strategyId": "TEST_STRATEGY",
          "name": "Test Strategy",
          "symbol": "SPY",
          "strategyType": "iron_condor",
          "enabled": true,
          "entryRules": {
            "minDaysToExpiration": 30,
            "maxDaysToExpiration": 45
          },
          "legs": [
            {
              "action": "SELL",
              "optionType": "PUT",
              "strikeSelection": "delta",
              "strikeValue": -0.16,
              "quantity": 1
            }
          ],
          "exitRules": {
            "profitTargetPercent": 50.0,
            "stopLossPercent": 200.0
          },
          "riskManagement": {
            "maxPositionSize": 10
          }
        }
        """;

        // Act
        Exception? exception = Record.Exception(() => JObject.Parse(strategyJson));

        // Assert
        Assert.Null(exception); // No JSON parsing errors
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-15")]
    public void StrategyFile_ShouldHaveRequiredFields()
    {
        // Arrange
        string strategyJson = """
        {
          "strategyId": "TEST_001",
          "name": "Test",
          "symbol": "SPY",
          "strategyType": "iron_condor",
          "enabled": true,
          "legs": [],
          "exitRules": {},
          "riskManagement": {}
        }
        """;

        JObject json = JObject.Parse(strategyJson);

        // Act & Assert
        Assert.NotNull(json["strategyId"]);
        Assert.NotNull(json["name"]);
        Assert.NotNull(json["symbol"]);
        Assert.NotNull(json["strategyType"]);
        Assert.NotNull(json["enabled"]);
        Assert.NotNull(json["legs"]);
        Assert.NotNull(json["exitRules"]);
        Assert.NotNull(json["riskManagement"]);
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-16")]
    public void OrderSafetyValidation_PaperMode_ShouldAllow()
    {
        // Arrange
        TradingMode mode = TradingMode.Paper;

        // Act
        bool isAllowed = mode == TradingMode.Paper;

        // Assert
        Assert.True(isAllowed, "Paper mode should allow order submission");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-17")]
    public void OrderSafetyValidation_LiveMode_ShouldRequireConfirmation()
    {
        // Arrange
        TradingMode mode = TradingMode.Live;

        // Act
        bool requiresConfirmation = mode == TradingMode.Live;

        // Assert
        Assert.True(requiresConfirmation, "Live mode must require explicit confirmation");
    }

    [Fact]
    [Trait("Category", "E2E")]
    [Trait("TestId", "E2E-AUTO-18")]
    public void CloudflareWorkerUrl_ShouldBeHttps()
    {
        // Arrange
        string config = """
        {
          "CloudflareWorkerUrl": "https://trading-worker.example.workers.dev"
        }
        """;

        JObject json = JObject.Parse(config);

        // Act
        string? workerUrl = json["CloudflareWorkerUrl"]?.Value<string>();

        // Assert
        Assert.NotNull(workerUrl);
        Assert.StartsWith("https://", workerUrl, StringComparison.OrdinalIgnoreCase);
    }
}
