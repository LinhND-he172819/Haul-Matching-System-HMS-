using HMS.Modules.Transport.Application.DTOs;
using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Application.Services;

public interface ITripService
{
    Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<TripResponse>> ListAsync(Guid? driverId, TripStatus? status, CancellationToken cancellationToken = default);
    Task<TripResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TripResponse?> UpdateAsync(Guid id, UpdateTripRequest request, CancellationToken cancellationToken = default);
    Task<TripResponse?> ChangeStatusAsync(Guid id, ChangeTripStatusRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all non-deleted shipments currently linked to a trip (any link status,
    /// not just Matched). Used by the admin trip detail view.
    /// Returns null if the trip does not exist.
    /// </summary>
    Task<IReadOnlyList<TripShipmentResponse>?> GetTripShipmentsAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a single shipment from a trip (unlinks it).
    /// Use case: vehicle breakdown — operator needs to move some shipments to another trip.
    /// Only shipments in 'Matched' status can be unlinked (those that have not yet been
    /// picked up by the driver). In_Transit shipments must be resolved via the delivery
    /// workflow (Delivery_Failed → Returned_To_Hub → In_Warehouse).
    /// </summary>
    /// <returns>Result with the unlinked shipment QR + updated trip, or null if trip not found.</returns>
    Task<UnlinkTripShipmentResult?> UnlinkTripShipmentAsync(
        Guid tripId,
        Guid shipmentId,
        CancellationToken cancellationToken = default);
}
