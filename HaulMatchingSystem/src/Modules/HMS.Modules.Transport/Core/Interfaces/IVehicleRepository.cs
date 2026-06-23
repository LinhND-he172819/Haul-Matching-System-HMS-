using HMS.Modules.Transport.Core.Entities;

namespace HMS.Modules.Transport.Core.Interfaces;

public interface IVehicleRepository
{
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Vehicle>> ListAsync(string? search, string? status, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
}
