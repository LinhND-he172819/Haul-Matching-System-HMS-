using HMS.Modules.Matching.Core.Models;

namespace HMS.Modules.Matching.Core.Interfaces
{
    public interface IMatchingRepository
    {
        Task<Trip?> GetActiveTripForDriverAsync(Guid driverId, CancellationToken ct);

        Task<List<TripShipment>> GetSuggestedTripShipmentsAsync(Guid tripId, CancellationToken ct);

        Task<Vehicle?> GetVehicleAsync(Guid vehicleId, CancellationToken ct);

        Task<List<Shipment>> GetShipmentsByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);

        Task SaveChangesAsync(CancellationToken ct);

        Task BeginTransactionAsync(CancellationToken ct);

        Task CommitTransactionAsync(CancellationToken ct);

        Task RollbackTransactionAsync(CancellationToken ct);
    }
}
