namespace SharedKernel.Configuration;

/// <summary>
/// Validates application configuration sections at startup.
/// Provides fail-fast behavior for critical configuration errors.
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// Validates all configuration sections.
    /// Returns validation result with critical and non-critical errors.
    /// </summary>
    ValidationResult Validate();
}
