namespace HMS.Shared.Core.Enums;

/// <summary>
/// Status of a payment transaction.
/// </summary>
public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2,
    Cancelled = 3,
    PendingRefund = 4,
    Refunded = 5,
    PartiallyRefunded = 6
}
