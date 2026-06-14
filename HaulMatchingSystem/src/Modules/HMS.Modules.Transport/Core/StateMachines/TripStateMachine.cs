using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Core.StateMachines;

public static class TripStateMachine
{
    private static readonly IReadOnlyDictionary<TripStatus, TripStatus[]> AllowedTransitions =
        new Dictionary<TripStatus, TripStatus[]>
        {
            [TripStatus.Active] = [TripStatus.Completed, TripStatus.Breakdown],
            [TripStatus.Completed] = [],
            [TripStatus.Breakdown] = []
        };

    public static bool CanTransition(TripStatus currentStatus, TripStatus targetStatus)
    {
        return AllowedTransitions.TryGetValue(currentStatus, out var allowedStatuses) &&
            allowedStatuses.Contains(targetStatus);
    }

    public static void EnsureCanTransition(TripStatus currentStatus, TripStatus targetStatus)
    {
        if (currentStatus == targetStatus)
        {
            throw new InvalidOperationException($"Trip is already {currentStatus}.");
        }

        if (!CanTransition(currentStatus, targetStatus))
        {
            throw new InvalidOperationException($"Trip cannot transition from {currentStatus} to {targetStatus}.");
        }
    }
}
