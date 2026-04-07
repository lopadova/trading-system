namespace OptionsExecutionService.Common;

/// <summary>
/// System implementation of ITimeProvider that returns actual current time.
/// </summary>
public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
