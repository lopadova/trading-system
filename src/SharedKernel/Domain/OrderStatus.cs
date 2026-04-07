namespace SharedKernel.Domain;

/// <summary>
/// Represents the current status of an order in its lifecycle.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order validation failed, not submitted to IBKR.
    /// </summary>
    ValidationFailed = 0,

    /// <summary>
    /// Order created locally, not yet submitted to IBKR.
    /// </summary>
    PendingSubmit = 1,

    /// <summary>
    /// Order submitted to IBKR, waiting for acknowledgement.
    /// </summary>
    Submitted = 2,

    /// <summary>
    /// Order accepted by IBKR and is active.
    /// </summary>
    Active = 3,

    /// <summary>
    /// Order partially filled, still active for remaining quantity.
    /// </summary>
    PartiallyFilled = 4,

    /// <summary>
    /// Order completely filled.
    /// </summary>
    Filled = 5,

    /// <summary>
    /// Order cancelled by user or system.
    /// </summary>
    Cancelled = 6,

    /// <summary>
    /// Order rejected by IBKR (e.g., insufficient margin, invalid contract).
    /// </summary>
    Rejected = 7,

    /// <summary>
    /// Order submission failed due to technical error (connection, timeout, etc.).
    /// </summary>
    Failed = 8
}
