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
}
