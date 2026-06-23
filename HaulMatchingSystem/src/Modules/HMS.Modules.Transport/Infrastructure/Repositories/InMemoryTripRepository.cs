using System.Collections.Concurrent;
using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;
using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Infrastructure.Repositories;

public sealed class InMemoryTripRepository : ITripRepository
{
    private readonly ConcurrentDictionary<Guid, Trip> _trips = new();

    public Task AddAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        if (!_trips.TryAdd(trip.Id, trip))
        {
            throw new InvalidOperationException($"Trip {trip.Id} already exists.");
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<Trip>> ListAsync(
        Guid? driverId,
        TripStatus? status,
        CancellationToken cancellationToken = default)
    {
        var trips = _trips.Values.AsEnumerable();

        if (driverId.HasValue)
        {
            trips = trips.Where(trip => trip.DriverId == driverId.Value);
        }

        if (status.HasValue)
        {
            trips = trips.Where(trip => trip.Status == status.Value);
        }

        return Task.FromResult<IReadOnlyCollection<Trip>>(trips.ToArray());
    }

    public Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _trips.TryGetValue(id, out var trip);

        return Task.FromResult(trip);
    }

    public Task UpdateAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        _trips[trip.Id] = trip;

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_trips.TryRemove(id, out _));
    }
}
