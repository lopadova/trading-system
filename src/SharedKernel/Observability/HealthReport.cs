namespace SharedKernel.Observability;

/// <summary>
/// Snapshot of a service's health.
/// Returned by the /health HTTP endpoint each service exposes.
/// Immutable record — value equality keeps tests simple.
/// </summary>
/// <param name="Service">Logical service name, e.g. "supervisor", "options-execution".</param>
/// <param name="Status">"ok" | "degraded" | "down". "degraded" means partial subsystem failure (e.g. IBKR down but DB ok).</param>
/// <param name="Version">Assembly informational version for quick sanity checks.</param>
/// <param name="Uptime">Time since HealthState was constructed at service start.</param>
/// <param name="Now">Current UTC timestamp at the moment of the call.</param>
/// <param name="Checks">Sub-system name → human-readable status string. Examples: "ibkr" → "connected", "db" → "ok", "disk" → "low".</param>
public sealed record HealthReport(
    string Service,
    string Status,
    string Version,
    TimeSpan Uptime,
    DateTimeOffset Now,
    IReadOnlyDictionary<string, string> Checks);
