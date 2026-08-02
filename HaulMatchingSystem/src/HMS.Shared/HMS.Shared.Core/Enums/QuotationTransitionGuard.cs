namespace HMS.Shared.Core.Enums;

/// <summary>
/// Centralised state machine for quotation status transitions.
/// Every module MUST use this to validate transitions.
/// </summary>
public static class QuotationTransitionGuard
{
    private static readonly IReadOnlyDictionary<QuotationStatus, IReadOnlySet<QuotationStatus>> AllowedTransitions =
        new Dictionary<QuotationStatus, IReadOnlySet<QuotationStatus>>
        {
            [QuotationStatus.Draft] = new HashSet<QuotationStatus>
            {
                QuotationStatus.Sent,
                QuotationStatus.Cancelled
            },
            [QuotationStatus.Sent] = new HashSet<QuotationStatus>
            {
                QuotationStatus.Accepted,
                QuotationStatus.Expired,
                QuotationStatus.Cancelled
            },
            [QuotationStatus.Accepted] = new HashSet<QuotationStatus>(),
            [QuotationStatus.Expired] = new HashSet<QuotationStatus>(),
            [QuotationStatus.Cancelled] = new HashSet<QuotationStatus>()
        };

    public static bool CanTransition(QuotationStatus from, QuotationStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    public static void EnsureCanTransition(QuotationStatus from, QuotationStatus to)
    {
        if (from == to)
            throw new InvalidOperationException($"Quotation is already in status {to}.");

        if (!CanTransition(from, to))
        {
            var allowed = AllowedTransitions.TryGetValue(from, out var t)
                ? string.Join(", ", t)
                : "none";
            throw new InvalidOperationException(
                $"Quotation cannot transition from {from} to {to}. Allowed transitions: {allowed}.");
        }
    }

    public static IReadOnlySet<QuotationStatus> GetAllowedTransitions(QuotationStatus from)
    {
        if (AllowedTransitions.TryGetValue(from, out var targets))
            return targets;
        return new HashSet<QuotationStatus>();
    }
}
