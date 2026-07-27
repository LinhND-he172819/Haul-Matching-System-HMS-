namespace HMS.Shared.Core.Enums;

/// <summary>
/// Centralised state machine for proposal status transitions.
/// Every module MUST use this to validate transitions.
/// </summary>
public static class ProposalTransitionGuard
{
    private static readonly IReadOnlyDictionary<ProposalStatus, IReadOnlySet<ProposalStatus>> AllowedTransitions =
        new Dictionary<ProposalStatus, IReadOnlySet<ProposalStatus>>
        {
            [ProposalStatus.PendingReview] = new HashSet<ProposalStatus>
            {
                ProposalStatus.Approved,
                ProposalStatus.Rejected,
                ProposalStatus.Cancelled,
                ProposalStatus.Expired
            },
            [ProposalStatus.Approved] = new HashSet<ProposalStatus>
            {
                ProposalStatus.Confirmed,
                ProposalStatus.Cancelled,
                ProposalStatus.Expired
            },
            [ProposalStatus.Confirmed] = new HashSet<ProposalStatus>(),
            [ProposalStatus.Rejected] = new HashSet<ProposalStatus>(),
            [ProposalStatus.Cancelled] = new HashSet<ProposalStatus>(),
            [ProposalStatus.Expired] = new HashSet<ProposalStatus>()
        };

    public static bool CanTransition(ProposalStatus from, ProposalStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    public static void EnsureCanTransition(ProposalStatus from, ProposalStatus to)
    {
        if (from == to)
            throw new InvalidOperationException($"Proposal is already in status {to}.");

        if (!CanTransition(from, to))
        {
            var allowed = AllowedTransitions.TryGetValue(from, out var t)
                ? string.Join(", ", t)
                : "none";
            throw new InvalidOperationException(
                $"Proposal cannot transition from {from} to {to}. Allowed transitions: {allowed}.");
        }
    }

    public static IReadOnlySet<ProposalStatus> GetAllowedTransitions(ProposalStatus from)
    {
        if (AllowedTransitions.TryGetValue(from, out var targets))
            return targets;
        return new HashSet<ProposalStatus>();
    }
}
