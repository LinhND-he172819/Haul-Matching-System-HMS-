using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Application.DTOs;

public sealed record TripResponse(
    Guid Id,
    Guid DriverId,
    Guid VehicleId,
    Guid OriginHubId,
    Guid DestHubId,
    string RouteLineString,
    decimal CurrentLoadWeightKg,
    decimal CurrentLoadVolumeCbm,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int Version,
    TripStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
