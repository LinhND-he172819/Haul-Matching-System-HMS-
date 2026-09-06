namespace HMS.Modules.Transport.Application.DTOs;

public sealed record CreateTripRequest(
    Guid DriverId,
    Guid VehicleId,
    Guid OriginHubId,
    Guid DestHubId,
    string? RouteLineString,
    decimal CurrentLoadWeightKg = 0,
    decimal CurrentLoadVolumeCbm = 0,
    DateTimeOffset? ScheduledDepartureAt = null,
    List<Guid>? WarehouseShipmentIds = null);
