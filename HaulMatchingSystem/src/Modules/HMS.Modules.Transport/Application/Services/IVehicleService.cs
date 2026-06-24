using HMS.Modules.Transport.Application.DTOs;

namespace HMS.Modules.Transport.Application.Services;

public interface IVehicleService
{
    Task<VehicleResponse> CreateAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<VehicleResponse>> ListAsync(string? search, string? status, CancellationToken cancellationToken = default);
    Task<VehicleResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<VehicleResponse?> UpdateAsync(Guid id, UpdateVehicleRequest request, CancellationToken cancellationToken = default);
}
