namespace SharedKernel.Tests.Strategy;

using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Domain;
using SharedKernel.Strategy;
using Xunit;

/// <summary>
/// Tests for StrategyLoader.
/// Covers file loading, JSON parsing, directory scanning, and validation integration.
/// </summary>
public sealed class StrategyLoaderTests
{
    private readonly string _testDataPath;
    private readonly StrategyValidator _validator;
    private readonly StrategyLoader _loader;

    public StrategyLoaderTests()
    {
        // Get path to test data directory
        string currentDir = Directory.GetCurrentDirectory();
        _testDataPath = Path.Combine(currentDir, "TestData");

        _validator = new StrategyValidator(NullLogger<StrategyValidator>.Instance);
        _loader = new StrategyLoader(_validator, NullLogger<StrategyLoader>.Instance, strategiesBasePath: null);
    }

    [Fact]
    [Trait("TestId", "TEST-04-15")]
    public async Task LoadStrategyAsync_ValidFile_ReturnsStrategy()
    {
        // Arrange
        string filePath = Path.Combine(_testDataPath, "valid-strategy.json");

        // Act
        StrategyDefinition strategy = await _loader.LoadStrategyAsync(filePath);

        // Assert
        Assert.NotNull(strategy);
        Assert.Equal("Test Bull Put Spread", strategy.StrategyName);
        Assert.Equal("Valid test strategy for unit tests", strategy.Description);
        Assert.Equal(TradingMode.Paper, strategy.TradingMode);
        Assert.Equal("SPY", strategy.Underlying.Symbol);
        Assert.Equal("SMART", strategy.Underlying.Exchange);
        Assert.Equal(filePath, strategy.SourceFilePath);
    }

    [Fact]
    [Trait("TestId", "TEST-04-16")]
    public async Task LoadStrategyAsync_ValidFile_ParsesEntryRulesCorrectly()
    {
        // Arrange
        string filePath = Path.Combine(_testDataPath, "valid-strategy.json");

        // Act
        StrategyDefinition strategy = await _loader.LoadStrategyAsync(filePath);

        // Assert
        Assert.NotNull(strategy.EntryRules);
        Assert.Equal(30, strategy.EntryRules.MarketConditions.MinDaysToExpiration);
        Assert.Equal(45, strategy.EntryRules.MarketConditions.MaxDaysToExpiration);
        Assert.Equal(25, strategy.EntryRules.MarketConditions.IvRankMin);
        Assert.Equal(75, strategy.EntryRules.MarketConditions.IvRankMax);
        Assert.Equal(new TimeOnly(9, 35, 0), strategy.EntryRules.Timing.EntryTimeStart);
        Assert.Equal(new TimeOnly(15, 30, 0), strategy.EntryRules.Timing.EntryTimeEnd);
        Assert.Equal(5, strategy.EntryRules.Timing.DaysOfWeek.Length);
        Assert.Contains(DayOfWeek.Monday, strategy.EntryRules.Timing.DaysOfWeek);
    }

    [Fact]
    [Trait("TestId", "TEST-04-17")]
    public async Task LoadStrategyAsync_ValidFile_ParsesPositionConfigCorrectly()
    {
        // Arrange
        string filePath = Path.Combine(_testDataPath, "valid-strategy.json");

        // Act
        StrategyDefinition strategy = await _loader.LoadStrategyAsync(filePath);

        // Assert
        Assert.NotNull(strategy.Position);
        Assert.Equal("BullPutSpread", strategy.Position.Type);
        Assert.Equal(2, strategy.Position.Legs.Length);
        Assert.Equal(3, strategy.Position.MaxPositions);
        Assert.Equal(1000, strategy.Position.CapitalPerPosition);

        // Validate first leg (SELL PUT)
        OptionLeg leg1 = strategy.Position.Legs[0];
        Assert.Equal("SELL", leg1.Action);
        Assert.Equal("PUT", leg1.Right);
        Assert.Equal("DELTA", leg1.StrikeSelectionMethod);
        Assert.Equal(-0.30m, leg1.StrikeValue);
        Assert.Equal(1, leg1.Quantity);

        // Validate second leg (BUY PUT)
        OptionLeg leg2 = strategy.Position.Legs[1];
        Assert.Equal("BUY", leg2.Action);
        Assert.Equal("PUT", leg2.Right);
        Assert.Equal("OFFSET", leg2.StrikeSelectionMethod);
        Assert.Equal(-5, leg2.StrikeOffset);
        Assert.Equal(1, leg2.Quantity);
    }

    [Fact]
    [Trait("TestId", "TEST-04-18")]
    public async Task LoadStrategyAsync_ValidFile_ParsesExitRulesCorrectly()
    {
        // Arrange
        string filePath = Path.Combine(_testDataPath, "valid-strategy.json");

        // Act
        StrategyDefinition strategy = await _loader.LoadStrategyAsync(filePath);

        // Assert
        Assert.NotNull(strategy.ExitRules);
        Assert.Equal(0.50m, strategy.ExitRules.ProfitTarget);
        Assert.Equal(2.00m, strategy.ExitRules.StopLoss);
        Assert.Equal(21, strategy.ExitRules.MaxDaysInTrade);
        Assert.Equal(new TimeOnly(15, 45, 0), strategy.ExitRules.ExitTimeOfDay);
    }

    [Fact]
    [Trait("TestId", "TEST-04-19")]
    public async Task LoadStrategyAsync_ValidFile_ParsesRiskManagementCorrectly()
    {
        // Arrange
        string filePath = Path.Combine(_testDataPath, "valid-strategy.json");

        // Act
        StrategyDefinition strategy = await _loader.LoadStrategyAsync(filePath);

        // Assert
        Assert.NotNull(strategy.RiskManagement);
        Assert.Equal(5000, strategy.RiskManagement.MaxTotalCapitalAtRisk);
        Assert.Equal(10.0m, strategy.RiskManagement.MaxDrawdownPercent);
        Assert.Equal(500, strategy.RiskManagement.MaxDailyLoss);
    }

    [Fact]
    [Trait("TestId", "TEST-04-20")]
    public async Task LoadStrategyAsync_FileNotFound_ThrowsFileNotFoundException()
    {
        // Arrange
        string filePath = Path.Combine(_testDataPath, "nonexistent.json");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _loader.LoadStrategyAsync(filePath));
    }

    [Fact]
    [Trait("TestId", "TEST-04-21")]
    public async Task LoadStrategyAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        string filePath = Path.Combine(_testDataPath, "invalid-bad-json.json");

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadStrategyAsync(filePath));
        Assert.Contains("parse", ex.Message.ToLowerInvariant());
    }

    [Fact]
    [Trait("TestId", "TEST-04-22")]
    public async Task LoadStrategyAsync_MissingRequiredFields_ThrowsInvalidOperationException()
    {
        // Arrange
        string filePath = Path.Combine(_testDataPath, "invalid-missing-fields.json");

        // Act & Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _loader.LoadStrategyAsync(filePath));
        Assert.Contains("deserialize", ex.Message.ToLowerInvariant());
    }

    [Fact]
    [Trait("TestId", "TEST-04-23")]
    public async Task LoadStrategyAsync_NullFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _loader.LoadStrategyAsync(null!));
    }

    [Fact]
    [Trait("TestId", "TEST-04-24")]
    public async Task LoadStrategyAsync_EmptyFilePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _loader.LoadStrategyAsync(""));
    }

    [Fact]
    [Trait("TestId", "TEST-04-25")]
    public async Task LoadAllStrategiesAsync_ValidDirectory_ReturnsAllValidStrategies()
    {
        // Arrange - use test data directory that has mix of valid and invalid files
        string directoryPath = _testDataPath;

        // Act
        IReadOnlyList<StrategyDefinition> strategies = await _loader.LoadAllStrategiesAsync(directoryPath);

        // Assert
        Assert.NotEmpty(strategies);
        // Should load only valid-strategy.json, skip invalid ones
        Assert.Single(strategies);
        Assert.Equal("Test Bull Put Spread", strategies[0].StrategyName);
    }

    [Fact]
    [Trait("TestId", "TEST-04-26")]
    public async Task LoadAllStrategiesAsync_DirectoryNotFound_ReturnsEmptyList()
    {
        // Arrange
        string directoryPath = Path.Combine(_testDataPath, "nonexistent-dir");

        // Act
        IReadOnlyList<StrategyDefinition> strategies = await _loader.LoadAllStrategiesAsync(directoryPath);

        // Assert
        Assert.Empty(strategies);
    }

    [Fact]
    [Trait("TestId", "TEST-04-27")]
    public async Task LoadAllStrategiesAsync_EmptyDirectory_ReturnsEmptyList()
    {
        // Arrange - create temporary empty directory
        string emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        try
        {
            // Act
            IReadOnlyList<StrategyDefinition> strategies = await _loader.LoadAllStrategiesAsync(emptyDir);

            // Assert
            Assert.Empty(strategies);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(emptyDir))
            {
                Directory.Delete(emptyDir, recursive: true);
            }
        }
    }

    [Fact]
    [Trait("TestId", "TEST-04-28")]
    public async Task LoadExampleStrategiesAsync_LoadsFromExamplesFolder()
    {
        // Arrange - create loader with strategies base path pointing to actual strategies folder
        string strategiesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "strategies"));
        StrategyLoader loader = new(_validator, NullLogger<StrategyLoader>.Instance, strategiesPath);

        // Act
        IReadOnlyList<StrategyDefinition> strategies = await loader.LoadExampleStrategiesAsync();

        // Assert
        // Should load example-put-spread.json from strategies/examples
        Assert.NotEmpty(strategies);
        Assert.Contains(strategies, s => s.StrategyName.Contains("Example"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-29")]
    public async Task LoadPrivateStrategiesAsync_LoadsFromPrivateFolder()
    {
        // Arrange - create loader with strategies base path
        string strategiesPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "..", "strategies"));
        StrategyLoader loader = new(_validator, NullLogger<StrategyLoader>.Instance, strategiesPath);

        // Act
        IReadOnlyList<StrategyDefinition> strategies = await loader.LoadPrivateStrategiesAsync();

        // Assert
        // Private folder exists but only has .gitkeep, so should be empty
        Assert.Empty(strategies);
    }
}
