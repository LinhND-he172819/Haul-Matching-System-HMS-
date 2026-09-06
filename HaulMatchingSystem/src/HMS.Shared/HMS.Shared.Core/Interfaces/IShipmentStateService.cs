using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Events;

namespace HMS.Shared.Core.Interfaces;

/// <summary>
/// Central state machine for shipment status transitions.
/// Every module MUST use this instead of setting shipment.Status directly.
/// </summary>
public interface IShipmentStateService
{
    /// <summary>
    /// Validates and executes a status transition atomically.
    /// When called within an existing transaction, the state service joins that transaction
    /// instead of creating its own.
    /// </summary>
    /// <param name="shipmentId">Shipment to transition.</param>
    /// <param name="toStatus">Target status.</param>
    /// <param name="connection">Optional shared NpgsqlConnection (caller's transaction).</param>
    /// <param name="transaction">Optional shared NpgsqlTransaction (caller's transaction).</param>
    /// <param name="performedBy">User who performed the action.</param>
    /// <param name="reason">Optional reason (required for cancellations).</param>
    /// <param name="onEventPublished">
    /// Optional callback invoked after the event is published (outside the DB transaction).
    /// When the caller owns the transaction, it should pass a hook that fires after commit
    /// so that the ShipmentStatusChangedEvent is never dispatched inside an open transaction.
    /// When null (default), the event is published immediately inside TransitionAsync.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<ShipmentStatus> TransitionAsync(
        Guid shipmentId,
        ShipmentStatus toStatus,
        object? connection = null,
        object? transaction = null,
        Guid? performedBy = null,
        string? reason = null,
        Func<ShipmentStatusChangedEvent, CancellationToken, Task>? onEventPublished = null,
        CancellationToken ct = default);

    /// <summary>
    /// Reads the current status of a shipment.
    /// </summary>
    Task<ShipmentStatus> GetCurrentStatusAsync(Guid shipmentId, CancellationToken ct = default);
}
