namespace OptionsExecutionService.Common;

/// <summary>
/// Abstraction for time operations to enable testability.
/// </summary>
public interface ITimeProvider
{
    /// <summary>
    /// Gets the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }
}
