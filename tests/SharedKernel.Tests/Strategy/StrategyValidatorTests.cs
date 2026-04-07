namespace SharedKernel.Tests.Strategy;

using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Domain;
using SharedKernel.Strategy;
using Xunit;

/// <summary>
/// Tests for StrategyValidator.
/// Covers validation of all required fields, numeric ranges, and business rules.
/// </summary>
public sealed class StrategyValidatorTests
{
    private readonly StrategyValidator _validator;

    public StrategyValidatorTests()
    {
        _validator = new StrategyValidator(NullLogger<StrategyValidator>.Instance);
    }

    [Fact]
    [Trait("TestId", "TEST-04-01")]
    public void Validate_ValidStrategy_ReturnsSuccess()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy();

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    [Trait("TestId", "TEST-04-02")]
    public void Validate_NullStrategy_ReturnsFailure()
    {
        // Act
        ValidationResult result = _validator.Validate(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("cannot be null", result.Errors[0]);
    }

    [Fact]
    [Trait("TestId", "TEST-04-03")]
    public void Validate_EmptyStrategyName_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with { StrategyName = "" };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("StrategyName"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-04")]
    public void Validate_InvalidDaysToExpiration_ReturnsFailure()
    {
        // Arrange - MaxDaysToExpiration < MinDaysToExpiration
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            EntryRules = new EntryRules
            {
                MarketConditions = new MarketConditions
                {
                    MinDaysToExpiration = 50,
                    MaxDaysToExpiration = 30,  // Invalid: less than min
                    IvRankMin = 25,
                    IvRankMax = 75
                },
                Timing = CreateValidTiming()
            }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxDaysToExpiration"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-05")]
    public void Validate_InvalidIvRank_ReturnsFailure()
    {
        // Arrange - IvRankMin > 100
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            EntryRules = new EntryRules
            {
                MarketConditions = new MarketConditions
                {
                    MinDaysToExpiration = 30,
                    MaxDaysToExpiration = 45,
                    IvRankMin = 150,  // Invalid: > 100
                    IvRankMax = 75
                },
                Timing = CreateValidTiming()
            }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("IvRankMin"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-06")]
    public void Validate_InvalidStrategyType_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            Position = CreateValidPosition() with { Type = "InvalidType" }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Position.Type"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-07")]
    public void Validate_NoLegs_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            Position = CreateValidPosition() with { Legs = Array.Empty<OptionLeg>() }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Legs"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-08")]
    public void Validate_InvalidLegAction_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            Position = CreateValidPosition() with
            {
                Legs = new[]
                {
                    new OptionLeg
                    {
                        Action = "INVALID",  // Invalid action
                        Right = "PUT",
                        StrikeSelectionMethod = "DELTA",
                        StrikeValue = -0.30m,
                        Quantity = 1
                    }
                }
            }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Action"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-09")]
    public void Validate_DeltaMethodWithoutStrikeValue_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            Position = CreateValidPosition() with
            {
                Legs = new[]
                {
                    new OptionLeg
                    {
                        Action = "SELL",
                        Right = "PUT",
                        StrikeSelectionMethod = "DELTA",
                        StrikeValue = null,  // Missing required value for DELTA
                        Quantity = 1
                    }
                }
            }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("StrikeValue") && e.Contains("DELTA"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-10")]
    public void Validate_OffsetMethodWithoutStrikeOffset_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            Position = CreateValidPosition() with
            {
                Legs = new[]
                {
                    new OptionLeg
                    {
                        Action = "BUY",
                        Right = "PUT",
                        StrikeSelectionMethod = "OFFSET",
                        StrikeOffset = null,  // Missing required offset for OFFSET
                        Quantity = 1
                    }
                }
            }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("StrikeOffset") && e.Contains("OFFSET"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-11")]
    public void Validate_InvalidMaxPositions_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            Position = CreateValidPosition() with { MaxPositions = 0 }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxPositions"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-12")]
    public void Validate_InvalidProfitTarget_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            ExitRules = CreateValidExitRules() with { ProfitTarget = -0.5m }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("ProfitTarget"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-13")]
    public void Validate_InvalidMaxDrawdownPercent_ReturnsFailure()
    {
        // Arrange
        StrategyDefinition strategy = CreateValidStrategy() with
        {
            RiskManagement = CreateValidRiskManagement() with { MaxDrawdownPercent = 150 }
        };

        // Act
        ValidationResult result = _validator.Validate(strategy);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxDrawdownPercent"));
    }

    [Fact]
    [Trait("TestId", "TEST-04-14")]
    public void Validate_AllValidStrategyTypes_ReturnsSuccess()
    {
        // Arrange - Test all valid strategy types
        string[] validTypes = { "BullPutSpread", "BearCallSpread", "IronCondor", "Straddle", "Strangle" };

        foreach (string type in validTypes)
        {
            StrategyDefinition strategy = CreateValidStrategy() with
            {
                Position = CreateValidPosition() with { Type = type }
            };

            // Act
            ValidationResult result = _validator.Validate(strategy);

            // Assert
            Assert.True(result.IsValid, $"Strategy type '{type}' should be valid");
        }
    }

    // Helper methods to create valid test objects

    private static StrategyDefinition CreateValidStrategy()
    {
        return new StrategyDefinition
        {
            StrategyName = "Test Strategy",
            Description = "Test strategy for validation",
            TradingMode = TradingMode.Paper,
            Underlying = new UnderlyingConfig
            {
                Symbol = "SPY",
                Exchange = "SMART",
                Currency = "USD"
            },
            EntryRules = new EntryRules
            {
                MarketConditions = new MarketConditions
                {
                    MinDaysToExpiration = 30,
                    MaxDaysToExpiration = 45,
                    IvRankMin = 25,
                    IvRankMax = 75
                },
                Timing = CreateValidTiming()
            },
            Position = CreateValidPosition(),
            ExitRules = CreateValidExitRules(),
            RiskManagement = CreateValidRiskManagement()
        };
    }

    private static TimingRules CreateValidTiming()
    {
        return new TimingRules
        {
            EntryTimeStart = new TimeOnly(9, 35, 0),
            EntryTimeEnd = new TimeOnly(15, 30, 0),
            DaysOfWeek = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }
        };
    }

    private static PositionConfig CreateValidPosition()
    {
        return new PositionConfig
        {
            Type = "BullPutSpread",
            Legs = new[]
            {
                new OptionLeg
                {
                    Action = "SELL",
                    Right = "PUT",
                    StrikeSelectionMethod = "DELTA",
                    StrikeValue = -0.30m,
                    Quantity = 1
                },
                new OptionLeg
                {
                    Action = "BUY",
                    Right = "PUT",
                    StrikeSelectionMethod = "OFFSET",
                    StrikeOffset = -5,
                    Quantity = 1
                }
            },
            MaxPositions = 3,
            CapitalPerPosition = 1000
        };
    }

    private static ExitRules CreateValidExitRules()
    {
        return new ExitRules
        {
            ProfitTarget = 0.50m,
            StopLoss = 2.00m,
            MaxDaysInTrade = 21,
            ExitTimeOfDay = new TimeOnly(15, 45, 0)
        };
    }

    private static RiskManagement CreateValidRiskManagement()
    {
        return new RiskManagement
        {
            MaxTotalCapitalAtRisk = 5000,
            MaxDrawdownPercent = 10.0m,
            MaxDailyLoss = 500
        };
    }
}
