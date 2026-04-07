namespace SharedKernel.Strategy;

/// <summary>
/// Result of strategy validation, containing success status and error messages.
/// Immutable record representing validation outcome.
/// </summary>
public sealed record ValidationResult
{
    /// <summary>
    /// True if validation passed with no errors.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// List of validation error messages. Empty if IsValid is true.
    /// </summary>
    public required IReadOnlyList<string> Errors { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new()
    {
        IsValid = true,
        Errors = Array.Empty<string>()
    };

    /// <summary>
    /// Creates a failed validation result with error messages.
    /// </summary>
    public static ValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };

    /// <summary>
    /// Creates a failed validation result from a list of error messages.
    /// </summary>
    public static ValidationResult Failure(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}
