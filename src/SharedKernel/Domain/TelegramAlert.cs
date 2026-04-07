namespace SharedKernel.Domain;

/// <summary>
/// Represents a telegram alert message to be sent to configured chat.
/// Immutable record for type safety and queueing.
/// </summary>
public sealed record TelegramAlert
{
    /// <summary>
    /// Unique identifier for this alert (for deduplication and tracking).
    /// </summary>
    public string AlertId { get; init; } = string.Empty;

    /// <summary>
    /// Alert severity level (determines emoji and urgency).
    /// </summary>
    public AlertSeverity Severity { get; init; }

    /// <summary>
    /// Alert category/type for grouping and filtering.
    /// </summary>
    public AlertType Type { get; init; }

    /// <summary>
    /// Main alert message (short, descriptive).
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Optional detailed information (formatted as markdown for Telegram).
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Source service that raised the alert.
    /// </summary>
    public string SourceService { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when alert was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Number of send attempts (for retry logic).
    /// </summary>
    public int RetryCount { get; init; }

    /// <summary>
    /// Next retry time (null if first attempt or already sent).
    /// </summary>
    public DateTime? NextRetryAtUtc { get; init; }
}
