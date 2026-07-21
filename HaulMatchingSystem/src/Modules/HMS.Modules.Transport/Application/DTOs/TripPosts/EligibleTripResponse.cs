namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record EligibleTripResponse(
    Guid TripId,
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
    decimal CurrentLoadWeightKg,
    decimal RemainingWeightKg,
    decimal MaxVolumeCbm,
    decimal CurrentLoadVolumeCbm,
    decimal RemainingVolumeCbm,
    DateTimeOffset? StartedAt,
    string Status);
