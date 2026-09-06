using HMS.Modules.Transport.Core.Entities;
using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Core.Interfaces;

public interface ITripRepository
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken = default);

    /// <summary>Adds a trip inside a caller-owned transaction (connection/transaction may be Npgsql objects).</summary>
    Task AddAsync(Trip trip, object? connection, object? transaction, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Trip>> ListAsync(Guid? driverId, TripStatus? status, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default);

    /// <summary>Updates a trip inside a caller-owned transaction (connection/transaction may be Npgsql objects).</summary>
    Task UpdateAsync(Trip trip, object? connection, object? transaction, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CountActiveTripsForDriverAsync(Guid driverId, Guid? excludeTripId = null, CancellationToken cancellationToken = default);
    Task<int> CountActiveTripsForVehicleAsync(Guid vehicleId, Guid? excludeTripId = null, CancellationToken cancellationToken = default);
    Task<int> CountInProgressTripsForDriverAsync(Guid driverId, Guid? excludeTripId = null, CancellationToken cancellationToken = default);
    Task<int> CountInProgressTripsForVehicleAsync(Guid vehicleId, Guid? excludeTripId = null, CancellationToken cancellationToken = default);
}
