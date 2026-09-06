using HMS.Modules.Transport.Application.DTOs;
using HMS.Modules.Transport.Application.Services;
using HMS.Modules.Transport.Core.Entities;
using HMS.Modules.Transport.Core.Interfaces;
using HMS.Modules.Transport.Infrastructure.Repositories;
using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Interfaces;
using HMS.Shared.Core.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace HMS.Modules.Transport.Tests;

public sealed class TripServiceTests
{
    [Fact]
    public async Task CreateAsync_CreatesScheduledTrip()
    {
        var service = CreateService();
        var request = ValidCreateRequest();

        var trip = await service.CreateAsync(request);

        Assert.NotEqual(Guid.Empty, trip.Id);
        // New lifecycle: trips start as Scheduled (not legacy Active)
        Assert.Equal(TripStatus.Scheduled, trip.Status);
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
    public async Task CreateAsync_RequiresScheduledDepartureAt()
    {
        var service = CreateService();
        var request = ValidCreateRequest() with { ScheduledDepartureAt = null };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));

        Assert.Contains("Ngày khởi hành dự kiến là bắt buộc", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_RejectsPastScheduledDepartureAt()
    {
        var service = CreateService();
        var request = ValidCreateRequest() with { ScheduledDepartureAt = DateTimeOffset.UtcNow.AddHours(-1) };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateAsync(request));

        Assert.Contains("phải lớn hơn thời điểm hiện tại", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_RequiresScheduledDepartureAt()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(created.Id, ValidUpdateRequest() with { ScheduledDepartureAt = null }));

        Assert.Contains("Ngày khởi hành dự kiến là bắt buộc", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_RejectsPastScheduledDepartureAt()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => service.UpdateAsync(created.Id, ValidUpdateRequest() with { ScheduledDepartureAt = DateTimeOffset.UtcNow.AddHours(-1) }));

        Assert.Contains("phải lớn hơn thời điểm hiện tại", exception.Message);
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

        var trips = await service.ListAsync(driverId, TripStatus.Scheduled);

        var trip = Assert.Single(trips);
        Assert.Equal(driverId, trip.DriverId);
        Assert.Equal(TripStatus.Scheduled, trip.Status);
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

        // Walk new lifecycle: Scheduled → Ready → InProgress → Completed
        await service.ChangeStatusAsync(created.Id,
            new ChangeTripStatusRequest(TripStatus.Ready, created.StartedAt!.Value.AddHours(1)));
        await service.ChangeStatusAsync(created.Id,
            new ChangeTripStatusRequest(TripStatus.InProgress, created.StartedAt!.Value.AddHours(2)));

        var occurredAt = created.StartedAt!.Value.AddHours(8);

        var completed = await service.ChangeStatusAsync(
            created.Id,
            new ChangeTripStatusRequest(TripStatus.Completed, occurredAt));

        Assert.NotNull(completed);
        Assert.Equal(TripStatus.Completed, completed.Status);
        Assert.Equal(occurredAt, completed.FinishedAt);
        Assert.Equal(4, completed.Version);
    }

    [Fact]
    public async Task ChangeStatusAsync_MarksActiveTripAsBreakdown()
    {
        var repository = new InMemoryTripRepository();
        var service = CreateService(repository: repository);

        // Breakdown is only reachable from legacy Active status (new lifecycle
        // flows Scheduled → Ready → InProgress → Completed), so seed an
        // Active trip directly via Rehydrate.
        var now = DateTimeOffset.UtcNow;
        var trip = Trip.Rehydrate(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "LINESTRING (106.7 10.8, 105.8 21.0)", 100, 5,
            startedAt: now, finishedAt: null, version: 1,
            status: TripStatus.Active, createdAt: now, updatedAt: now);
        await repository.AddAsync(trip);

        var breakdown = await service.ChangeStatusAsync(
            trip.Id,
            new ChangeTripStatusRequest(TripStatus.Breakdown, now.AddHours(2)));

        Assert.NotNull(breakdown);
        Assert.Equal(TripStatus.Breakdown, breakdown.Status);
        Assert.Null(breakdown.FinishedAt);
    }

    [Fact]
    public async Task ChangeStatusAsync_RejectsTerminalStatusTransition()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());

        // Drive to Completed via Scheduled → Ready → InProgress → Completed
        await service.ChangeStatusAsync(created.Id,
            new ChangeTripStatusRequest(TripStatus.Ready, created.StartedAt!.Value.AddHours(1)));
        await service.ChangeStatusAsync(created.Id,
            new ChangeTripStatusRequest(TripStatus.InProgress, created.StartedAt!.Value.AddHours(2)));
        await service.ChangeStatusAsync(created.Id,
            new ChangeTripStatusRequest(TripStatus.Completed, created.StartedAt!.Value.AddHours(8)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ChangeStatusAsync(created.Id, new ChangeTripStatusRequest(TripStatus.Breakdown, created.StartedAt!.Value.AddHours(9))));

        Assert.Equal("Trip cannot transition from Completed to Breakdown.", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_RejectsCompletedTrip()
    {
        var service = CreateService();
        var created = await service.CreateAsync(ValidCreateRequest());

        // Drive to Completed via Scheduled → Ready → InProgress → Completed
        await service.ChangeStatusAsync(created.Id,
            new ChangeTripStatusRequest(TripStatus.Ready, created.StartedAt!.Value.AddHours(1)));
        await service.ChangeStatusAsync(created.Id,
            new ChangeTripStatusRequest(TripStatus.InProgress, created.StartedAt!.Value.AddHours(2)));
        await service.ChangeStatusAsync(
            created.Id,
            new ChangeTripStatusRequest(TripStatus.Completed, created.StartedAt!.Value.AddHours(8)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(created.Id, ValidUpdateRequest()));

        Assert.Equal("Only active or scheduled trips can be updated.", exception.Message);
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

    // ── Active-trip validation tests ──

    [Fact]
    public async Task CreateAsync_RejectsDriverWith2ActiveTrips()
    {
        var service = CreateService();
        var driverId = Guid.NewGuid();

        // First two trips succeed
        await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });
        await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });

        // Third trip with same driver fails
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(ValidCreateRequest() with { DriverId = driverId }));

        Assert.Contains("Tài xế", exception.Message);
        Assert.Contains("tối đa 2", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_AllowsDriverWith1ActiveTrip()
    {
        var service = CreateService();
        var driverId = Guid.NewGuid();

        // First trip succeeds
        await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });

        // Second trip with same driver also succeeds (max 2 allowed)
        var trip2 = await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });
        Assert.Equal(driverId, trip2.DriverId);
    }

    [Fact]
    public async Task CreateAsync_RejectsVehicleWith2ActiveTrips()
    {
        var service = CreateService();
        var vehicleId = Guid.NewGuid();

        await service.CreateAsync(ValidCreateRequest() with { VehicleId = vehicleId });
        await service.CreateAsync(ValidCreateRequest() with { VehicleId = vehicleId });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(ValidCreateRequest() with { VehicleId = vehicleId }));

        Assert.Contains("Xe", exception.Message);
        Assert.Contains("tối đa 2", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_AllowsVehicleWith1ActiveTrip()
    {
        var service = CreateService();
        var vehicleId = Guid.NewGuid();

        await service.CreateAsync(ValidCreateRequest() with { VehicleId = vehicleId });

        var trip2 = await service.CreateAsync(ValidCreateRequest() with { VehicleId = vehicleId });
        Assert.Equal(vehicleId, trip2.VehicleId);
    }

    [Fact]
    public async Task CreateAsync_AllowsDriverAfterTripCompleted()
    {
        var service = CreateService();
        var driverId = Guid.NewGuid();

        var trip1 = await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });
        // Drive to Completed via Scheduled → Ready → InProgress → Completed
        await service.ChangeStatusAsync(trip1.Id,
            new ChangeTripStatusRequest(TripStatus.Ready, trip1.StartedAt!.Value.AddHours(1)));
        await service.ChangeStatusAsync(trip1.Id,
            new ChangeTripStatusRequest(TripStatus.InProgress, trip1.StartedAt!.Value.AddHours(2)));
        await service.ChangeStatusAsync(trip1.Id,
            new ChangeTripStatusRequest(TripStatus.Completed, trip1.StartedAt!.Value.AddHours(4)));

        // Driver is free now — should succeed
        var trip2 = await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });
        Assert.Equal(driverId, trip2.DriverId);
    }

    [Fact]
    public async Task CreateAsync_AllowsVehicleAfterTripCompleted()
    {
        var service = CreateService();
        var vehicleId = Guid.NewGuid();

        var trip1 = await service.CreateAsync(ValidCreateRequest() with { VehicleId = vehicleId });
        // Drive to Completed via Scheduled → Ready → InProgress → Completed
        await service.ChangeStatusAsync(trip1.Id,
            new ChangeTripStatusRequest(TripStatus.Ready, trip1.StartedAt!.Value.AddHours(1)));
        await service.ChangeStatusAsync(trip1.Id,
            new ChangeTripStatusRequest(TripStatus.InProgress, trip1.StartedAt!.Value.AddHours(2)));
        await service.ChangeStatusAsync(trip1.Id,
            new ChangeTripStatusRequest(TripStatus.Completed, trip1.StartedAt!.Value.AddHours(4)));

        var trip2 = await service.CreateAsync(ValidCreateRequest() with { VehicleId = vehicleId });
        Assert.Equal(vehicleId, trip2.VehicleId);
    }

    [Fact]
    public async Task CreateAsync_AllowsDriverAfterTripBreakdown()
    {
        var repository = new InMemoryTripRepository();
        var service = CreateService(repository: repository);
        var driverId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        // Seed 2 legacy Active trips for the same driver (at the max limit)
        var seeded = new List<Trip>();
        for (var i = 0; i < 2; i++)
        {
            var t = Trip.Rehydrate(
                Guid.NewGuid(), driverId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "LINESTRING (106.7 10.8, 105.8 21.0)", 100, 5,
                startedAt: now, finishedAt: null, version: 1,
                status: TripStatus.Active, createdAt: now, updatedAt: now);
            await repository.AddAsync(t);
            seeded.Add(t);
        }

        // At the limit → creating a 3rd trip must fail
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateAsync(ValidCreateRequest() with { DriverId = driverId }));

        // Break down one trip → driver has capacity again
        await service.ChangeStatusAsync(seeded[0].Id,
            new ChangeTripStatusRequest(TripStatus.Breakdown, now.AddHours(2)));

        var trip2 = await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });
        Assert.Equal(driverId, trip2.DriverId);
    }

    [Fact]
    public async Task UpdateAsync_RejectsDriverAssignedToAnotherActiveTrip()
    {
        var service = CreateService();
        var sharedDriverId = Guid.NewGuid();

        // Create 2 active trips with sharedDriverId
        var trip1 = await service.CreateAsync(ValidCreateRequest() with { DriverId = sharedDriverId });
        var trip2 = await service.CreateAsync(ValidCreateRequest() with { DriverId = sharedDriverId });

        // Create a 3rd trip with a different driver
        var trip3 = await service.CreateAsync(ValidCreateRequest() with { DriverId = Guid.NewGuid() });

        // Try to assign the sharedDriver to trip3 → already has 2 active → should fail
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(trip3.Id, ValidUpdateRequest() with { DriverId = sharedDriverId }));

        Assert.Contains("Tài xế", exception.Message);
        Assert.Contains("tối đa 2", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_RejectsVehicleAssignedToAnotherActiveTrip()
    {
        var service = CreateService();
        var sharedVehicleId = Guid.NewGuid();

        // Create 2 active trips with sharedVehicleId
        var trip1 = await service.CreateAsync(ValidCreateRequest() with { VehicleId = sharedVehicleId });
        var trip2 = await service.CreateAsync(ValidCreateRequest() with { VehicleId = sharedVehicleId });

        // Create a 3rd trip with a different vehicle
        var trip3 = await service.CreateAsync(ValidCreateRequest() with { VehicleId = Guid.NewGuid() });

        // Try to assign sharedVehicle to trip3 → already has 2 active → should fail
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateAsync(trip3.Id, ValidUpdateRequest() with { VehicleId = sharedVehicleId }));

        Assert.Contains("Xe", exception.Message);
        Assert.Contains("tối đa 2", exception.Message);
    }

    [Fact]
    public async Task UpdateAsync_AllowsSameDriverOnSameTrip()
    {
        var service = CreateService();
        var driverId = Guid.NewGuid();

        var trip = await service.CreateAsync(ValidCreateRequest() with { DriverId = driverId });

        // Updating the same trip with same driver should be fine
        var updated = await service.UpdateAsync(trip.Id, ValidUpdateRequest() with { DriverId = driverId });
        Assert.NotNull(updated);
        Assert.Equal(driverId, updated.DriverId);
    }

    [Fact]
    public async Task UpdateAsync_AllowsSameVehicleOnSameTrip()
    {
        var service = CreateService();
        var vehicleId = Guid.NewGuid();

        var trip = await service.CreateAsync(ValidCreateRequest() with { VehicleId = vehicleId });

        var updated = await service.UpdateAsync(trip.Id, ValidUpdateRequest() with { VehicleId = vehicleId });
        Assert.NotNull(updated);
        Assert.Equal(vehicleId, updated.VehicleId);
    }

    // ── Cancel → unlink matched shipments (state machine guard tests) ─
    //
    // Note: full end-to-end tests for the cancel→unlink flow require a real
    // PostgreSQL connection (TripService opens Npgsql directly). The
    // integration coverage is provided by the PowerShell smoke-test script.
    // Here we only cover the pre-flight validation paths that fail before
    // any SQL is executed.

    [Fact]
    public async Task UnlinkTripShipmentAsync_ReturnsNullForUnknownTrip()
    {
        var service = CreateService();

        var result = await service.UnlinkTripShipmentAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task UnlinkTripShipmentAsync_RejectsCompletedTrip()
    {
        var repository = new InMemoryTripRepository();
        var service = CreateService(repository: repository);

        var now = DateTimeOffset.UtcNow;
        var tripId = Guid.NewGuid();
        var trip = Trip.Rehydrate(
            tripId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "LINESTRING (106.7 10.8, 105.8 21.0)", 100, 5,
            startedAt: now, finishedAt: now.AddHours(1), version: 1,
            status: TripStatus.Completed, createdAt: now, updatedAt: now);
        await repository.AddAsync(trip);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UnlinkTripShipmentAsync(tripId, Guid.NewGuid()));

        Assert.Contains("Completed", exception.Message);
    }

    [Fact]
    public async Task UnlinkTripShipmentAsync_RejectsCancelledTrip()
    {
        var repository = new InMemoryTripRepository();
        var service = CreateService(repository: repository);

        var now = DateTimeOffset.UtcNow;
        var tripId = Guid.NewGuid();
        var trip = Trip.Rehydrate(
            tripId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "LINESTRING (106.7 10.8, 105.8 21.0)", 0, 0,
            startedAt: now, finishedAt: now.AddHours(1), version: 1,
            status: TripStatus.Cancelled, createdAt: now, updatedAt: now);
        await repository.AddAsync(trip);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UnlinkTripShipmentAsync(tripId, Guid.NewGuid()));

        Assert.Contains("Cancelled", exception.Message);
    }

    [Fact]
    public async Task GetTripShipmentsAsync_ReturnsNullForUnknownTrip()
    {
        var service = CreateService();

        var result = await service.GetTripShipmentsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    private static TripService CreateService(
        ITripRoutePlanner? routePlanner = null,
        InMemoryTripRepository? repository = null,
        IShipmentStateService? shipmentStateService = null)
    {
        var vehicleRepo = new StubVehicleRepository();
        // Seed a default vehicle so ValidateLoadCapacityAsync can resolve it.
        // Tests use random Guid vehicle IDs; the stub resolves any requested ID
        // to this vehicle's capacity limits.
        vehicleRepo.Seed(Vehicle.Create(
            code: "VH-TEST-001",
            licensePlate: "51A-123.45",
            hubId: Guid.NewGuid(),
            vehicleType: "Truck",
            maxWeightKg: 10_000m,
            maxVolumeCbm: 50m,
            status: Vehicle.AvailableStatus));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=hms_db;Username=postgres;Password=hms_password_123"
            })
            .Build();
        return new TripService(
            repository ?? new InMemoryTripRepository(),
            routePlanner ?? new StubTripRoutePlanner(),
            vehicleRepo,
            shipmentStateService ?? new StubShipmentStateService(),
            configuration,
            NullLogger<TripService>.Instance);
    }

    private static CreateTripRequest ValidCreateRequest()
    {
        return new CreateTripRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "LINESTRING (106.7 10.8, 105.8 21.0)",
            0,
            0,
            DateTimeOffset.UtcNow.AddDays(2));
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
            0,
            DateTimeOffset.UtcNow.AddDays(2));
    }

    /// <summary>
    /// No-op stub: linking paths hit real SQL only when WarehouseShipmentIds is
    /// provided (tests keep it null), so transitions never fire here.
    /// </summary>
    private sealed class StubShipmentStateService : IShipmentStateService
    {
        public Task<ShipmentStatus> TransitionAsync(
            Guid shipmentId,
            ShipmentStatus toStatus,
            object? connection = null,
            object? transaction = null,
            Guid? performedBy = null,
            string? reason = null,
            Func<ShipmentStatusChangedEvent, CancellationToken, Task>? onEventPublished = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(toStatus);

        public Task<ShipmentStatus> GetCurrentStatusAsync(Guid shipmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(ShipmentStatus.Draft);
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

    private sealed class StubVehicleRepository : IVehicleRepository
    {
        private readonly Dictionary<Guid, Vehicle> _vehicles = new();

        /// <summary>
        /// Seeds a vehicle and also registers a fallback so any unknown vehicle ID
        /// resolves to it (tests generate random vehicle IDs).
        /// </summary>
        public void Seed(Vehicle vehicle)
        {
            _vehicles[vehicle.Id] = vehicle;
            _fallback = vehicle;
        }

        private Vehicle? _fallback;

        public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
        {
            _vehicles[vehicle.Id] = vehicle;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<Vehicle>> ListAsync(string? search, string? status, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<Vehicle>>(_vehicles.Values.ToList());
        }

        public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            if (_vehicles.TryGetValue(id, out var vehicle)) return Task.FromResult<Vehicle?>(vehicle);
            return Task.FromResult<Vehicle?>(_fallback);
        }

        public Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
        {
            _vehicles[vehicle.Id] = vehicle;
            return Task.CompletedTask;
        }
    }
}
