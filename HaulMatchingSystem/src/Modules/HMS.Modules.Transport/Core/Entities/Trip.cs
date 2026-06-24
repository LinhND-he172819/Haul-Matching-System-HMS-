using HMS.Shared.Core.Enums;
using HMS.Modules.Transport.Core.StateMachines;

namespace HMS.Modules.Transport.Core.Entities;

public sealed class Trip
{
    private Trip()
    {
    }

    private Trip(
        Guid id,
        Guid driverId,
        Guid vehicleId,
        Guid originHubId,
        Guid destHubId,
        string routeLineString,
        decimal currentLoadWeightKg,
        decimal currentLoadVolumeCbm)
    {
        Id = id;
        DriverId = driverId;
        VehicleId = vehicleId;
        OriginHubId = originHubId;
        DestHubId = destHubId;
        RouteLineString = routeLineString;
        CurrentLoadWeightKg = currentLoadWeightKg;
        CurrentLoadVolumeCbm = currentLoadVolumeCbm;
        Status = TripStatus.Active;
        Version = 1;
        CreatedAt = DateTimeOffset.UtcNow;
        StartedAt = CreatedAt;
        UpdatedAt = CreatedAt;
    }

    public Guid Id { get; }
    public Guid DriverId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid OriginHubId { get; private set; }
    public Guid DestHubId { get; private set; }
    public string RouteLineString { get; private set; }
    public decimal CurrentLoadWeightKg { get; private set; }
    public decimal CurrentLoadVolumeCbm { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public int Version { get; private set; }
    public TripStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Trip Create(
        Guid driverId,
        Guid vehicleId,
        Guid originHubId,
        Guid destHubId,
        string routeLineString,
        decimal currentLoadWeightKg,
        decimal currentLoadVolumeCbm)
    {
        Validate(
            driverId,
            vehicleId,
            originHubId,
            destHubId,
            routeLineString,
            currentLoadWeightKg,
            currentLoadVolumeCbm);

        return new Trip(
            Guid.NewGuid(),
            driverId,
            vehicleId,
            originHubId,
            destHubId,
            NormalizeRouteLineString(routeLineString),
            currentLoadWeightKg,
            currentLoadVolumeCbm);
    }

    public static Trip Rehydrate(
        Guid id,
        Guid driverId,
        Guid vehicleId,
        Guid originHubId,
        Guid destHubId,
        string routeLineString,
        decimal currentLoadWeightKg,
        decimal currentLoadVolumeCbm,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        int version,
        TripStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var trip = new Trip(
            id,
            driverId,
            vehicleId,
            originHubId,
            destHubId,
            NormalizeRouteLineString(routeLineString),
            currentLoadWeightKg,
            currentLoadVolumeCbm)
        {
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Version = version,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        return trip;
    }

    public void UpdateDetails(
        Guid driverId,
        Guid vehicleId,
        Guid originHubId,
        Guid destHubId,
        string routeLineString,
        decimal currentLoadWeightKg,
        decimal currentLoadVolumeCbm)
    {
        if (Status != TripStatus.Active)
        {
            throw new InvalidOperationException("Only active trips can be updated.");
        }

        Validate(
            driverId,
            vehicleId,
            originHubId,
            destHubId,
            routeLineString,
            currentLoadWeightKg,
            currentLoadVolumeCbm);

        DriverId = driverId;
        VehicleId = vehicleId;
        OriginHubId = originHubId;
        DestHubId = destHubId;
        RouteLineString = NormalizeRouteLineString(routeLineString);
        CurrentLoadWeightKg = currentLoadWeightKg;
        CurrentLoadVolumeCbm = currentLoadVolumeCbm;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeStatus(TripStatus targetStatus, DateTimeOffset occurredAt)
    {
        TripStateMachine.EnsureCanTransition(Status, targetStatus);

        if (StartedAt.HasValue && occurredAt < StartedAt.Value)
        {
            throw new ArgumentException("OccurredAt cannot be before StartedAt.", nameof(occurredAt));
        }

        Status = targetStatus;
        if (targetStatus == TripStatus.Completed)
        {
            FinishedAt = occurredAt;
        }

        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void Validate(
        Guid driverId,
        Guid vehicleId,
        Guid originHubId,
        Guid destHubId,
        string routeLineString,
        decimal currentLoadWeightKg,
        decimal currentLoadVolumeCbm)
    {
        if (driverId == Guid.Empty)
        {
            throw new ArgumentException("DriverId is required.", nameof(driverId));
        }

        if (vehicleId == Guid.Empty)
        {
            throw new ArgumentException("VehicleId is required.", nameof(vehicleId));
        }

        if (originHubId == Guid.Empty)
        {
            throw new ArgumentException("OriginHubId is required.", nameof(originHubId));
        }

        if (destHubId == Guid.Empty)
        {
            throw new ArgumentException("DestHubId is required.", nameof(destHubId));
        }

        if (string.IsNullOrWhiteSpace(routeLineString))
        {
            throw new ArgumentException("RouteLineString is required.", nameof(routeLineString));
        }

        if (!routeLineString.TrimStart().StartsWith("LINESTRING", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("RouteLineString must be a WKT LINESTRING.", nameof(routeLineString));
        }

        if (currentLoadWeightKg < 0)
        {
            throw new ArgumentException("CurrentLoadWeightKg cannot be negative.", nameof(currentLoadWeightKg));
        }

        if (currentLoadVolumeCbm < 0)
        {
            throw new ArgumentException("CurrentLoadVolumeCbm cannot be negative.", nameof(currentLoadVolumeCbm));
        }
    }

    private static string NormalizeRouteLineString(string routeLineString)
    {
        return routeLineString.Trim();
    }
}
