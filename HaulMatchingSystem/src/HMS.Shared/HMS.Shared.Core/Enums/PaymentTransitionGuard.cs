namespace HMS.Shared.Core.Enums;

/// <summary>
/// Centralised state machine for payment status transitions.
/// Every module MUST use this to validate transitions.
/// </summary>
public static class PaymentTransitionGuard
{
    private static readonly IReadOnlyDictionary<PaymentStatus, IReadOnlySet<PaymentStatus>> AllowedTransitions =
        new Dictionary<PaymentStatus, IReadOnlySet<PaymentStatus>>
        {
            [PaymentStatus.Pending] = new HashSet<PaymentStatus>
            {
                PaymentStatus.Paid,
                PaymentStatus.Failed,
                PaymentStatus.Cancelled
            },
            [PaymentStatus.Paid] = new HashSet<PaymentStatus>
            {
                PaymentStatus.PendingRefund
            },
            [PaymentStatus.Failed] = new HashSet<PaymentStatus>
            {
                PaymentStatus.Pending   // Retry: Failed → Pending
            },
            [PaymentStatus.Cancelled] = new HashSet<PaymentStatus>(),
            [PaymentStatus.PendingRefund] = new HashSet<PaymentStatus>
            {
                PaymentStatus.Refunded,
                PaymentStatus.PartiallyRefunded
            },
            [PaymentStatus.Refunded] = new HashSet<PaymentStatus>(),
            [PaymentStatus.PartiallyRefunded] = new HashSet<PaymentStatus>()
        };

    public static bool CanTransition(PaymentStatus from, PaymentStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    public static void EnsureCanTransition(PaymentStatus from, PaymentStatus to)
    {
        if (from == to)
            throw new InvalidOperationException($"Payment is already in status {to}.");

        if (!CanTransition(from, to))
        {
            var allowed = AllowedTransitions.TryGetValue(from, out var t)
                ? string.Join(", ", t)
                : "none";
            throw new InvalidOperationException(
                $"Payment cannot transition from {from} to {to}. Allowed transitions: {allowed}.");
        }
    }
}
