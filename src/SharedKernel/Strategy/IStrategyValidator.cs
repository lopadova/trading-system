namespace SharedKernel.Strategy;

using SharedKernel.Domain;

/// <summary>
/// Validates strategy definitions for correctness and safety.
/// Enforces business rules, data constraints, and safety checks.
/// </summary>
public interface IStrategyValidator
{
    /// <summary>
    /// Validates a strategy definition.
    /// </summary>
    /// <param name="strategy">Strategy to validate.</param>
    /// <returns>Validation result with errors if validation fails.</returns>
    ValidationResult Validate(StrategyDefinition strategy);
}
