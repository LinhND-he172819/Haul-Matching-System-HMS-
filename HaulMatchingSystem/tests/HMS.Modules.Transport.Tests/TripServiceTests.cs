using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Application.Services;
using HMS.Modules.Transport.Infrastructure.Repositories;
using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Tests;

public sealed class TripServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesActiveTrip()
    {
        var service = CreateService();
        var request = ValidCreateRequest();

        var trip = await service.CreateAsync(request);

        Assert.NotEqual(Guid.Empty, trip.Id);
        Assert.Equal(TripStatus.Active, trip.Status);
        Assert.Equal(request.DriverId, trip.DriverId);
        Assert.Equal(request.DestHubId, trip.DestHubId);
        Assert.Equal("LINESTRING (106.7 10.8, 105.8 21.0)", trip.RouteLineString);
        Assert.Equal(1, trip.Version);
    }

    [Fact]
    public async Task CreateAsync_RequiresRouteLineString()
    {
        var service = CreateService();
        var request = ValidCreateRequest() with
        {
            RouteLineString = string.Empty
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));

        Assert.Equal("routeLineString", exception.ParamName);
    }

    [Fact]
    public async Task CreateAsync_UsesGeneratedRouteLineStringWhenRequestOmitsRoute()
    {
        const string generatedRoute = "LINESTRING (106.7 10.8, 107.2 13.4, 108.22 16.07)";
        var service = CreateService(new StubTripRoutePlanner(generatedRoute));
        var request = ValidCreateRequest() with
        {
            RouteLineString = null
        };

        var trip = await service.CreateAsync(request);

        Assert.Equal(generatedRoute, trip.RouteLineString);
    }

    [Fact]
    public async Task ListAsync_FiltersByDriverAndStatus()
    {
        var service = CreateService();
        var driverId = Guid.NewGuid();

        await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });
        await service.CreateAsync(ValidCreateRequest() with { DriverId = Guid.NewGuid() });

        var trips = await service.ListAsync(driverId, TripStatus.Active);

        var trip = Assert.Single(trips);
        Assert.Equal(driverId, trip.DriverId);
        Assert.Equal(TripStatus.Active, trip.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesActiveTripAndVersion()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());
        var newDestHubId = Guid.NewGuid();
        var update = ValidUpdateRequest() with
        {
            DestHubId = newDestHubId,
            CurrentLoadWeightKg = 500
        };

        var updated = await service.UpdateAsync(created.Id, update);

        Assert.NotNull(updated);
        Assert.Equal(newDestHubId, updated.DestHubId);
        Assert.Equal(500, updated.CurrentLoadWeightKg);
        Assert.Equal(2, updated.Version);
    }

    [Fact]
    public async Task ChangeStatusAsync_CompletesActiveTrip()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());
        var occurredAt = created.StartedAt!.Value.AddHours(8);

        var completed = await service.ChangeStatusAsync(
            created.Id,
            new ChangeTripStatusRequest(TripStatus.Completed, occurredAt));

        Assert.NotNull(completed);
        Assert.Equal(TripStatus.Completed, completed.Status);
        Assert.Equal(occurredAt, completed.FinishedAt);
        Assert.Equal(2, completed.Version);
    }

    [Fact]
    public async Task ChangeStatusAsync_MarksActiveTripAsBreakdown()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());

        var breakdown = await service.ChangeStatusAsync(
            created.Id,
            new ChangeTripStatusRequest(TripStatus.Breakdown, created.StartedAt!.Value.AddHours(2)));

        Assert.NotNull(breakdown);
        Assert.Equal(TripStatus.Breakdown, breakdown.Status);
        Assert.Null(breakdown.FinishedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_RejectsTerminalStatusTransition()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());
        var occurredAt = created.StartedAt!.Value.AddHours(8);

        await service.ChangeStatusAsync(created.Id, new ChangeTripStatusRequest(TripStatus.Completed, occurredAt));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChangeStatusAsync(created.Id, new ChangeTripStatusRequest(TripStatus.Breakdown, occurredAt.AddHours(1))));

        Assert.Equal("Trip cannot transition from Completed to Breakdown.", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_RejectsCompletedTrip()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());

        await service.ChangeStatusAsync(
            created.Id,
            new ChangeTripStatusRequest(TripStatus.Completed, created.StartedAt!.Value.AddHours(8)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(created.Id, ValidUpdateRequest()));

        Assert.Equal("Only active trips can be updated.", exception.Message);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTrip()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());

        var deleted = await service.DeleteAsync(created.Id);
        var loaded = await service.GetByIdAsync(created.Id);

        Assert.True(deleted);
        Assert.Null(loaded);
    }

    private static TripService CreateService(ITripRoutePlanner? routePlanner = null)
    {
        return new TripService(new InMemoryTripRepository(), routePlanner ?? new StubTripRoutePlanner());
    }

    private static CreateTripRequest ValidCreateRequest()
    {
        return new CreateTripRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LINESTRING (106.7 10.8, 105.8 21.0)");
    }

    private static UpdateTripRequest ValidUpdateRequest()
    {
        return new UpdateTripRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LINESTRING (106.7 10.8, 105.8 21.0)",
            0,
            0);
    }

    private sealed class StubTripRoutePlanner(string? generatedRouteLineString = null) : ITripRoutePlanner
    {
        public Task<string> ResolveRouteLineStringAsync(
            Guid originHubId,
            Guid destHubId,
            string? requestedRouteLineString,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(requestedRouteLineString))
            {
                return Task.FromResult(requestedRouteLineString.Trim());
            }

            if (!string.IsNullOrWhiteSpace(generatedRouteLineString))
            {
                return Task.FromResult(generatedRouteLineString);
            }

            throw new ArgumentException("RouteLineString is required.", "routeLineString");
        }
    }
}
