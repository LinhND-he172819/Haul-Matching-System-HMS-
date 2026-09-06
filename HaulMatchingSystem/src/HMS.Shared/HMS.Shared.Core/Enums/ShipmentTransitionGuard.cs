using System.Collections.Concurrent;

namespace HMS.Shared.Core.Enums;

/// <summary>
/// Centralised state machine for shipment status transitions.
/// Every module MUST use this to validate transitions — no module may set shipment.Status directly.
/// </summary>
public static class ShipmentTransitionGuard
{
    private static readonly IReadOnlyDictionary<ShipmentStatus, IReadOnlySet<ShipmentStatus>> AllowedTransitions =
        new Dictionary<ShipmentStatus, IReadOnlySet<ShipmentStatus>>
        {
            [ShipmentStatus.Draft] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.PendingReview,   // Customer submits proposal
                ShipmentStatus.PendingDeposit,  // Staff sends quotation directly
                ShipmentStatus.In_Warehouse,     // Staff intake
                ShipmentStatus.Matched,          // DirectPickup: Draft → Matched
                ShipmentStatus.Cancelled
            },
            [ShipmentStatus.PendingReview] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.PendingDeposit,   // Staff approves proposal + sends quotation
                ShipmentStatus.Cancelled
            },
            [ShipmentStatus.PendingDeposit] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.Matched,          // Customer pays deposit
                ShipmentStatus.PendingReview,    // Quotation cancelled, revert to review
                ShipmentStatus.Cancelled
            },
            [ShipmentStatus.In_Warehouse] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.Matched,
                ShipmentStatus.Cancelled
            },
            [ShipmentStatus.Matched] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.In_Transit,
                ShipmentStatus.In_Warehouse,    // Unlinked from trip (admin removes shipment from trip)
                ShipmentStatus.Cancelled
            },
            [ShipmentStatus.In_Transit] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.Delivered,
                ShipmentStatus.Delivery_Failed,
                ShipmentStatus.Arrived_At_Destination_Hub
            },
            [ShipmentStatus.Delivery_Failed] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.Returned_To_Hub,
                ShipmentStatus.Pending_Rescue,
                ShipmentStatus.Forced_Return
            },
            [ShipmentStatus.Pending_Rescue] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.In_Transit,
                ShipmentStatus.Returned_To_Hub
            },
            [ShipmentStatus.Returned_To_Hub] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.In_Warehouse
            },
            [ShipmentStatus.Forced_Return] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.Returned_To_Hub
            },
            [ShipmentStatus.Arrived_At_Destination_Hub] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.Delivered
            },
            [ShipmentStatus.Cancelled] = new HashSet<ShipmentStatus>(),
            [ShipmentStatus.Delivered] = new HashSet<ShipmentStatus>
            {
                ShipmentStatus.Completed   // Final payment complete
            }
        };

    /// <summary>
    /// Returns true if the transition from <paramref name="from"/> to <paramref name="to"/> is allowed.
    /// </summary>
    public static bool CanTransition(ShipmentStatus from, ShipmentStatus to)
    {
        return AllowedTransitions.TryGetValue(from, out var targets) && targets.Contains(to);
    }

    /// <summary>
    /// Throws <see cref="Exceptions.InvalidShipmentTransitionException"/> if the transition is not allowed.
    /// </summary>
    public static void EnsureCanTransition(ShipmentStatus from, ShipmentStatus to)
    {
        if (from == to)
            throw new Exceptions.InvalidShipmentTransitionException(from, to, "Shipment is already in this status.");

        if (!CanTransition(from, to))
            throw new Exceptions.InvalidShipmentTransitionException(from, to);
    }

    /// <summary>
    /// Returns the set of statuses that <paramref name="from"/> can transition to.
    /// </summary>
    public static IReadOnlySet<ShipmentStatus> GetAllowedTransitions(ShipmentStatus from)
    {
        return AllowedTransitions.TryGetValue(from, out var targets)
            ? targets
            : new HashSet<ShipmentStatus>();
    }
}
