namespace SharedKernel.Domain;

/// <summary>
/// Severity levels for alerts sent to the notification system.
/// Used to prioritize alerts and determine notification channels (email, SMS, dashboard only).
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational messages: routine operations, confirmations.
    /// Typically logged but not sent to external channels.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warnings: potential issues that don't require immediate action.
    /// May be sent to dashboard or email depending on configuration.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Errors: actionable problems that may affect trading but system continues.
    /// Typically sent via email and dashboard.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical: severe issues requiring immediate attention.
    /// System may halt trading. Sent via all configured channels (email, SMS).
    /// </summary>
    Critical = 3
}
