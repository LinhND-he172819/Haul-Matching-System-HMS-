using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;

namespace HMS.Modules.Transport.Application.Services;

public sealed class VehicleService(IVehicleRepository repository) : IVehicleService
{
    public async Task<VehicleResponse> CreateAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        var vehicle = Vehicle.Create(
            request.Code,
            request.LicensePlate,
            request.HubId,
            request.VehicleType,
            request.MaxWeightKg,
            request.MaxVolumeCbm,
            request.Status);

        await repository.AddAsync(vehicle, cancellationToken);

        return ToResponse(vehicle);
    }

    public async Task<IReadOnlyCollection<VehicleResponse>> ListAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var vehicles = await repository.ListAsync(search, status, cancellationToken);

        return vehicles
            .OrderBy(vehicle => vehicle.Code)
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<VehicleResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await repository.GetByIdAsync(id, cancellationToken);

        return vehicle is null ? null : ToResponse(vehicle);
    }

    public async Task<VehicleResponse?> UpdateAsync(
        Guid id,
        UpdateVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await repository.GetByIdAsync(id, cancellationToken);
        if (vehicle is null)
        {
            return null;
        }

        vehicle.Update(
            request.Code,
            request.LicensePlate,
            request.HubId,
            request.VehicleType,
            request.MaxWeightKg,
            request.MaxVolumeCbm,
            request.Status);

        await repository.UpdateAsync(vehicle, cancellationToken);

        return ToResponse(vehicle);
    }

    private static VehicleResponse ToResponse(Vehicle vehicle)
    {
        return new VehicleResponse(
            vehicle.Id,
            vehicle.Code,
            vehicle.LicensePlate,
            vehicle.HubId,
            vehicle.VehicleType,
            vehicle.MaxWeightKg,
            vehicle.MaxVolumeCbm,
            vehicle.Status,
            vehicle.CreatedAt,
            vehicle.UpdatedAt);
    }
}
