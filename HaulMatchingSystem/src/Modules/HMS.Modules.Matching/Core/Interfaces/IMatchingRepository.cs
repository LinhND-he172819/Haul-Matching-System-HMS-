using HMS.Modules.Matching.Core.Models;

namespace HMS.Modules.Matching.Core.Interfaces
{
    public interface IMatchingRepository
    {
        Task<Trip?> GetActiveTripForDriverAsync(Guid driverId, CancellationToken ct);

        Task<List<TripShipment>> GetSuggestedTripShipmentsAsync(Guid tripId, CancellationToken ct);

        Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken ct);

        Task<List<Shipment>> GetShipmentsByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

        Task<List<SpatialShipmentCandidate>> GetSpatialShipmentCandidatesAsync(
            Guid tripId,
            decimal remainingWeightCapacity,
            decimal remainingVolumeCapacity,
            double routeBufferMeters,
            int limit,
            CancellationToken ct);

        Task AddTripShipmentSuggestionsAsync(IEnumerable<TripShipment> suggestions, CancellationToken ct);

        Task SaveChangesAsync(CancellationToken ct);

        Task BeginTransactionAsync(CancellationToken ct);

        Task CommitTransactionAsync(CancellationToken ct);

        Task RollbackTransactionAsync(CancellationToken ct);

        /// <summary>
        /// Returns the raw DbConnection for sharing with external services (e.g. ShipmentStateService).
        /// Returns null when the provider is non-relational (e.g. InMemory in tests).
        /// </summary>
        System.Data.Common.DbConnection? GetUnderlyingConnection();

        /// <summary>
        /// Returns the raw DbTransaction for sharing with external services.
        /// Returns null if no transaction is active.
        /// </summary>
        System.Data.Common.DbTransaction? GetUnderlyingTransaction();
    }
}
