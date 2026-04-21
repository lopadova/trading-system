namespace SharedKernel.Observability;

/// <summary>
/// Contract for collecting the current health snapshot of a service.
/// Implementations MUST be thread-safe (the health endpoint can be polled concurrently).
/// The implementation should be cheap — called at least once per polling interval (typically 5-15s).
/// </summary>
public interface IHealthState
{
    /// <summary>
    /// Returns the current health snapshot. Never throws — wraps internal errors
    /// into a "degraded" or "down" status so the caller always gets a response.
    /// </summary>
    HealthReport Current();
}
