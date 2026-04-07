namespace SharedKernel.Strategy;

using Microsoft.Extensions.Logging;
using SharedKernel.Domain;

/// <summary>
/// Validates strategy definitions for correctness, safety, and business rule compliance.
/// Implements comprehensive validation including required fields, numeric ranges, and logic consistency.
/// </summary>
public sealed class StrategyValidator : IStrategyValidator
{
    private readonly ILogger<StrategyValidator> _logger;

    // Valid values for enums/constants
    private static readonly string[] ValidActions = { "BUY", "SELL" };
    private static readonly string[] ValidRights = { "PUT", "CALL" };
    private static readonly string[] ValidStrikeSelectionMethods = { "DELTA", "OFFSET", "ABSOLUTE" };
    private static readonly string[] ValidStrategyTypes = { "BullPutSpread", "BearCallSpread", "IronCondor", "Straddle", "Strangle" };

    public StrategyValidator(ILogger<StrategyValidator> logger)
    {
        _logger = logger;
    }

    public ValidationResult Validate(StrategyDefinition strategy)
    {
        // Validate input
        if (strategy == null)
        {
            return ValidationResult.Failure("Strategy cannot be null");
        }

        List<string> errors = new();

        // Validate required string fields
        ValidateRequiredString(errors, strategy.StrategyName, nameof(strategy.StrategyName));
        ValidateRequiredString(errors, strategy.Description, nameof(strategy.Description));

        // Validate underlying config
        if (strategy.Underlying == null)
        {
            errors.Add("Underlying configuration is required");
        }
        else
        {
            ValidateUnderlyingConfig(errors, strategy.Underlying);
        }

        // Validate entry rules
        if (strategy.EntryRules == null)
        {
            errors.Add("Entry rules are required");
        }
        else
        {
            ValidateEntryRules(errors, strategy.EntryRules);
        }

        // Validate position config
        if (strategy.Position == null)
        {
            errors.Add("Position configuration is required");
        }
        else
        {
            ValidatePositionConfig(errors, strategy.Position);
        }

        // Validate exit rules
        if (strategy.ExitRules == null)
        {
            errors.Add("Exit rules are required");
        }
        else
        {
            ValidateExitRules(errors, strategy.ExitRules);
        }

        // Validate risk management
        if (strategy.RiskManagement == null)
        {
            errors.Add("Risk management configuration is required");
        }
        else
        {
            ValidateRiskManagement(errors, strategy.RiskManagement);
        }

        // Return result
        if (errors.Count == 0)
        {
            _logger.LogDebug("Strategy '{StrategyName}' validation passed", strategy.StrategyName);
            return ValidationResult.Success();
        }

        _logger.LogWarning("Strategy '{StrategyName}' validation failed with {ErrorCount} errors",
            strategy.StrategyName, errors.Count);
        return ValidationResult.Failure(errors);
    }

    private static void ValidateRequiredString(List<string> errors, string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{fieldName} is required and cannot be empty");
        }
    }

    private static void ValidateUnderlyingConfig(List<string> errors, UnderlyingConfig underlying)
    {
        ValidateRequiredString(errors, underlying.Symbol, "Underlying.Symbol");
        ValidateRequiredString(errors, underlying.Exchange, "Underlying.Exchange");
        ValidateRequiredString(errors, underlying.Currency, "Underlying.Currency");

        // Validate symbol format (alphanumeric, max 10 chars)
        if (!string.IsNullOrWhiteSpace(underlying.Symbol) && underlying.Symbol.Length > 10)
        {
            errors.Add("Underlying.Symbol must be 10 characters or less");
        }
    }

    private static void ValidateEntryRules(List<string> errors, EntryRules rules)
    {
        // Validate market conditions
        if (rules.MarketConditions == null)
        {
            errors.Add("EntryRules.MarketConditions is required");
        }
        else
        {
            MarketConditions mc = rules.MarketConditions;

            if (mc.MinDaysToExpiration < 0)
            {
                errors.Add("MarketConditions.MinDaysToExpiration must be >= 0");
            }

            if (mc.MaxDaysToExpiration < mc.MinDaysToExpiration)
            {
                errors.Add("MarketConditions.MaxDaysToExpiration must be >= MinDaysToExpiration");
            }

            if (mc.IvRankMin < 0 || mc.IvRankMin > 100)
            {
                errors.Add("MarketConditions.IvRankMin must be between 0 and 100");
            }

            if (mc.IvRankMax < 0 || mc.IvRankMax > 100)
            {
                errors.Add("MarketConditions.IvRankMax must be between 0 and 100");
            }

            if (mc.IvRankMax < mc.IvRankMin)
            {
                errors.Add("MarketConditions.IvRankMax must be >= IvRankMin");
            }
        }

        // Validate timing rules
        if (rules.Timing == null)
        {
            errors.Add("EntryRules.Timing is required");
        }
        else
        {
            TimingRules timing = rules.Timing;

            if (timing.DaysOfWeek == null || timing.DaysOfWeek.Length == 0)
            {
                errors.Add("Timing.DaysOfWeek must contain at least one day");
            }

            // EntryTimeStart and EntryTimeEnd are TimeOnly structs, always valid
            // No need to validate null/empty
        }
    }

    private static void ValidatePositionConfig(List<string> errors, PositionConfig position)
    {
        ValidateRequiredString(errors, position.Type, "Position.Type");

        // Validate strategy type
        if (!string.IsNullOrWhiteSpace(position.Type) && !ValidStrategyTypes.Contains(position.Type))
        {
            errors.Add($"Position.Type '{position.Type}' is not recognized. Valid types: {string.Join(", ", ValidStrategyTypes)}");
        }

        // Validate legs
        if (position.Legs == null || position.Legs.Length == 0)
        {
            errors.Add("Position.Legs must contain at least one leg");
        }
        else
        {
            for (int i = 0; i < position.Legs.Length; i++)
            {
                ValidateOptionLeg(errors, position.Legs[i], i);
            }
        }

        // Validate position sizing
        if (position.MaxPositions <= 0)
        {
            errors.Add("Position.MaxPositions must be > 0");
        }

        if (position.CapitalPerPosition <= 0)
        {
            errors.Add("Position.CapitalPerPosition must be > 0");
        }
    }

    private static void ValidateOptionLeg(List<string> errors, OptionLeg leg, int index)
    {
        string prefix = $"Position.Legs[{index}]";

        // Validate action
        if (string.IsNullOrWhiteSpace(leg.Action))
        {
            errors.Add($"{prefix}.Action is required");
        }
        else if (!ValidActions.Contains(leg.Action))
        {
            errors.Add($"{prefix}.Action must be one of: {string.Join(", ", ValidActions)}");
        }

        // Validate right
        if (string.IsNullOrWhiteSpace(leg.Right))
        {
            errors.Add($"{prefix}.Right is required");
        }
        else if (!ValidRights.Contains(leg.Right))
        {
            errors.Add($"{prefix}.Right must be one of: {string.Join(", ", ValidRights)}");
        }

        // Validate strike selection method
        if (string.IsNullOrWhiteSpace(leg.StrikeSelectionMethod))
        {
            errors.Add($"{prefix}.StrikeSelectionMethod is required");
        }
        else if (!ValidStrikeSelectionMethods.Contains(leg.StrikeSelectionMethod))
        {
            errors.Add($"{prefix}.StrikeSelectionMethod must be one of: {string.Join(", ", ValidStrikeSelectionMethods)}");
        }
        else
        {
            // Validate strike value/offset based on method
            switch (leg.StrikeSelectionMethod)
            {
                case "DELTA":
                    if (!leg.StrikeValue.HasValue)
                    {
                        errors.Add($"{prefix}.StrikeValue is required when StrikeSelectionMethod is DELTA");
                    }
                    else if (leg.StrikeValue.Value < -1 || leg.StrikeValue.Value > 1)
                    {
                        errors.Add($"{prefix}.StrikeValue must be between -1 and 1 for DELTA method");
                    }
                    break;

                case "OFFSET":
                    if (!leg.StrikeOffset.HasValue)
                    {
                        errors.Add($"{prefix}.StrikeOffset is required when StrikeSelectionMethod is OFFSET");
                    }
                    break;

                case "ABSOLUTE":
                    if (!leg.StrikeValue.HasValue)
                    {
                        errors.Add($"{prefix}.StrikeValue is required when StrikeSelectionMethod is ABSOLUTE");
                    }
                    else if (leg.StrikeValue.Value <= 0)
                    {
                        errors.Add($"{prefix}.StrikeValue must be > 0 for ABSOLUTE method");
                    }
                    break;
            }
        }

        // Validate quantity
        if (leg.Quantity <= 0)
        {
            errors.Add($"{prefix}.Quantity must be > 0");
        }
    }

    private static void ValidateExitRules(List<string> errors, ExitRules rules)
    {
        if (rules.ProfitTarget <= 0)
        {
            errors.Add("ExitRules.ProfitTarget must be > 0");
        }

        if (rules.StopLoss <= 0)
        {
            errors.Add("ExitRules.StopLoss must be > 0");
        }

        if (rules.MaxDaysInTrade <= 0)
        {
            errors.Add("ExitRules.MaxDaysInTrade must be > 0");
        }

        // ExitTimeOfDay is TimeOnly struct, always valid
    }

    private static void ValidateRiskManagement(List<string> errors, RiskManagement risk)
    {
        if (risk.MaxTotalCapitalAtRisk <= 0)
        {
            errors.Add("RiskManagement.MaxTotalCapitalAtRisk must be > 0");
        }

        if (risk.MaxDrawdownPercent <= 0 || risk.MaxDrawdownPercent > 100)
        {
            errors.Add("RiskManagement.MaxDrawdownPercent must be between 0 and 100");
        }

        if (risk.MaxDailyLoss <= 0)
        {
            errors.Add("RiskManagement.MaxDailyLoss must be > 0");
        }
    }
}
