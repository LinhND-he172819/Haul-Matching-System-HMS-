using HMS.Modules.Transport.Core.Entities;
using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Core.Interfaces;

public interface ITripRepository
{
    Task AddAsync(Trip trip, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Trip>> ListAsync(Guid? driverId, TripStatus? status, CancellationToken cancellationToken = default);
    Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
