namespace SharedKernel.Configuration;

/// <summary>
/// Result of configuration validation.
/// Separates critical errors (fail-fast) from warnings (log only).
/// Immutable record.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// True if there are no critical errors. Service can start.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Critical errors that prevent service from starting safely.
    /// Examples: missing required fields, invalid port numbers, live trading mode.
    /// </summary>
    public required IReadOnlyList<string> CriticalErrors { get; init; }

    /// <summary>
    /// Non-critical warnings. Service can start but may not function optimally.
    /// Examples: missing optional Telegram config, high thresholds.
    /// </summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>
    /// Creates a successful validation result (no errors or warnings).
    /// </summary>
    public static ValidationResult Success() => new()
    {
        IsValid = true,
        CriticalErrors = Array.Empty<string>(),
        Warnings = Array.Empty<string>()
    };

    /// <summary>
    /// Creates a successful validation result with warnings (non-blocking).
    /// </summary>
    public static ValidationResult SuccessWithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        CriticalErrors = Array.Empty<string>(),
        Warnings = warnings.ToList()
    };

    /// <summary>
    /// Creates a failed validation result with critical errors (blocking).
    /// </summary>
    public static ValidationResult Failure(params string[] criticalErrors) => new()
    {
        IsValid = false,
        CriticalErrors = criticalErrors.ToList(),
        Warnings = Array.Empty<string>()
    };

    /// <summary>
    /// Creates a failed validation result with critical errors and warnings.
    /// </summary>
    public static ValidationResult Failure(
        IEnumerable<string> criticalErrors,
        IEnumerable<string> warnings) => new()
    {
        IsValid = false,
        CriticalErrors = criticalErrors.ToList(),
        Warnings = warnings.ToList()
    };
}
