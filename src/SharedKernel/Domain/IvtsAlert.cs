namespace SharedKernel.Domain;

/// <summary>
/// Alert triggered by IVTS threshold breach or anomaly detection.
/// Stored in alert_history table and sent to Telegram.
/// </summary>
public sealed record IvtsAlert
{
    /// <summary>
    /// Unique alert identifier (GUID).
    /// </summary>
    public string AlertId { get; init; } = string.Empty;

    /// <summary>
    /// Alert type (e.g., "IvrThresholdBreach", "InvertedCurve", "IvSpike").
    /// </summary>
    public string AlertType { get; init; } = string.Empty;

    /// <summary>
    /// Alert severity: "info", "warning", "critical".
    /// </summary>
    public string Severity { get; init; } = "warning";

    /// <summary>
    /// Underlying symbol that triggered the alert.
    /// </summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable alert message (formatted for Telegram).
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Reference to the IVTS snapshot that triggered this alert.
    /// </summary>
    public string SnapshotId { get; init; } = string.Empty;

    /// <summary>
    /// Additional structured data (JSON) for debugging/analysis.
    /// Example: { "ivr": 0.85, "threshold": 0.80, "iv30d": 0.25 }
    /// </summary>
    public string? DetailsJson { get; init; }

    /// <summary>
    /// Source service that raised this alert.
    /// </summary>
    public string SourceService { get; init; } = "TradingSupervisorService";

    /// <summary>
    /// ISO8601 timestamp when this alert was created.
    /// </summary>
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>
    /// ISO8601 timestamp when this alert was resolved (null if still active).
    /// </summary>
    public string? ResolvedAt { get; init; }

    /// <summary>
    /// How the alert was resolved (e.g., "auto", "manual", "timeout").
    /// </summary>
    public string? ResolvedBy { get; init; }
}
