namespace HMS.Modules.Transport.Application.DTOs;

public sealed record UpdateTripRequest(
    Guid DriverId,
    Guid VehicleId,
    Guid OriginHubId,
    Guid DestHubId,
    string RouteLineString,
    decimal CurrentLoadWeightKg,
    decimal CurrentLoadVolumeCbm);
