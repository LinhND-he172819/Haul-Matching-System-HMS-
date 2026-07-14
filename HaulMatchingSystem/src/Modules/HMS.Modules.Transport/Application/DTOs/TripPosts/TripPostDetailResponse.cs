namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record TripPostDetailResponse(
    Guid Id,
    Guid TripId,
    string Title,
    string? Description,
    // Trip info
    Guid OriginHubId,
    string OriginHubName,
    Guid DestinationHubId,
    string DestinationHubName,
    Guid DriverId,
    string DriverName,
    Guid VehicleId,
    string LicensePlate,
    string TruckType,
    decimal MaxWeightKg,
    decimal MaxVolumeCbm,
    decimal CurrentLoadWeightKg,
    decimal CurrentLoadVolumeCbm,
    decimal RemainingWeightKg,
    decimal RemainingVolumeCbm,
    DateTimeOffset? TripStartedAt,
    string TripStatus,
    // Post info
    string Status,
    DateTimeOffset AcceptUntil,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ClosedAt,
    Guid CreatedById,
    string CreatedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
