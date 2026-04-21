using SharedKernel.Domain;

namespace SharedKernel.Observability;

/// <summary>
/// Severity-aware alerter contract. Multiple implementations (Telegram, Email, ...)
/// can be registered; the composite consumer fans out and calls each in parallel.
/// <para>
/// Routing philosophy (enforced per-implementation, not by this interface):
/// </para>
/// <list type="bullet">
///   <item><description><b>Critical / Error</b>: deliver immediately — pager-like channels.</description></item>
///   <item><description><b>Warning</b>: buffer, flush as a digest every N minutes — avoids noise fatigue.</description></item>
///   <item><description><b>Info</b>: log locally, do not ship remote — keeps signal-to-noise healthy.</description></item>
/// </list>
/// </summary>
public interface IAlerter
{
    /// <summary>
    /// Sends a single alert immediately (or falls through to internal routing, e.g. Warning → digest).
    /// Implementations MUST swallow transient errors (network, SMTP etc.) and log them;
    /// throwing here would crash the caller (often a BackgroundService).
    /// </summary>
    /// <param name="severity">Alert priority; the implementation decides whether to send or buffer based on this.</param>
    /// <param name="title">Short headline (e.g. "IBKR disconnected &gt; 30s").</param>
    /// <param name="message">Free-form body. No PII/secrets — sanitize upstream.</param>
    /// <param name="ct">Cancellation token for graceful shutdown.</param>
    Task SendImmediateAsync(AlertSeverity severity, string title, string message, CancellationToken ct);

    /// <summary>
    /// Explicitly sends a batch of pre-buffered alerts as a digest, regardless of severity.
    /// Typically invoked by a timer flushing buffered Warning-level alerts.
    /// </summary>
    /// <param name="severity">Digest severity (usually Warning).</param>
    /// <param name="entries">Individual (title, message) pairs to concatenate into the digest.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendDigestAsync(AlertSeverity severity, IReadOnlyList<(string title, string message)> entries, CancellationToken ct);
}
