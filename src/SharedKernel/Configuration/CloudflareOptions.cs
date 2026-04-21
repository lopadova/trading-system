namespace SharedKernel.Configuration;

/// <summary>
/// Cloudflare Worker connection options. Bound from the
/// <c>Cloudflare:*</c> configuration section. Shared across services so both
/// <c>OutboxSyncWorker</c> (TradingSupervisorService) and the new
/// <c>SemaphoreGate</c> / audit-ship path (OptionsExecutionService) reference
/// the exact same keys — eliminating the drift risk of each service binding
/// its own copy.
/// </summary>
public sealed record CloudflareOptions
{
    /// <summary>
    /// Base URL of the Worker (no trailing slash, e.g. <c>https://trading.example.workers.dev</c>).
    /// When blank, every Worker-dependent code path is expected to treat the
    /// feature as disabled (log + no-op) rather than throw — keeps the service
    /// bootable on dev machines without a Worker deployed.
    /// </summary>
    public string WorkerUrl { get; init; } = string.Empty;

    /// <summary>
    /// Shared secret sent as <c>X-Api-Key</c>. Empty string is a supported
    /// "no auth" state; the Worker will reject the request with 401, and the
    /// caller is responsible for classifying that as a Red/fail-closed outcome.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// Convention name for binding from <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>.
    /// </summary>
    public const string SectionName = "Cloudflare";
}
