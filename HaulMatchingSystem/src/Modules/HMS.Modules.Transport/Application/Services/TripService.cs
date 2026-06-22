using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;
using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Application.Services;

public sealed class TripService(ITripRepository repository, ITripRoutePlanner routePlanner) : ITripService
{
    public async Task<TripResponse> CreateAsync(CreateTripRequest request, CancellationToken cancellationToken = default)
    {
        var routeLineString = await routePlanner.ResolveRouteLineStringAsync(
            request.OriginHubId,
            request.DestHubId,
            request.RouteLineString,
            cancellationToken);

        var trip = Trip.Create(
            request.DriverId,
            request.VehicleId,
            request.OriginHubId,
            request.DestHubId,
            routeLineString,
            request.CurrentLoadWeightKg,
            request.CurrentLoadVolumeCbm);

        await repository.AddAsync(trip, cancellationToken);

        return ToResponse(trip);
    }

    public async Task<IReadOnlyCollection<TripResponse>> ListAsync(
        Guid? driverId,
        TripStatus? status,
        CancellationToken cancellationToken = default)
    {
        var trips = await repository.ListAsync(driverId, status, cancellationToken);

        return trips
            .OrderBy(trip => trip.CreatedAt)
            .Select(ToResponse)
            .ToArray();
    }

    public async Task<TripResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);

        return trip is null ? null : ToResponse(trip);
    }

    public async Task<TripResponse?> UpdateAsync(
        Guid id,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        var routeLineString = await routePlanner.ResolveRouteLineStringAsync(
            request.OriginHubId,
            request.DestHubId,
            request.RouteLineString,
            cancellationToken);

        trip.UpdateDetails(
            request.DriverId,
            request.VehicleId,
            request.OriginHubId,
            request.DestHubId,
            routeLineString,
            request.CurrentLoadWeightKg,
            request.CurrentLoadVolumeCbm);

        await repository.UpdateAsync(trip, cancellationToken);

        return ToResponse(trip);
    }

    public async Task<TripResponse?> ChangeStatusAsync(
        Guid id,
        ChangeTripStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var trip = await repository.GetByIdAsync(id, cancellationToken);
        if (trip is null)
        {
            return null;
        }

        trip.ChangeStatus(request.Status, request.OccurredAt ?? DateTimeOffset.UtcNow);

        await repository.UpdateAsync(trip, cancellationToken);

        return ToResponse(trip);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return repository.DeleteAsync(id, cancellationToken);
    }

    private static TripResponse ToResponse(Trip trip)
    {
        return new TripResponse(
            trip.Id,
            trip.DriverId,
            trip.VehicleId,
            trip.OriginHubId,
            trip.DestHubId,
            trip.RouteLineString,
            trip.CurrentLoadWeightKg,
            trip.CurrentLoadVolumeCbm,
            trip.StartedAt,
            trip.FinishedAt,
            trip.Version,
            trip.Status,
            trip.CreatedAt,
            trip.UpdatedAt);
    }
}
