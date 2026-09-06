namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record PublicTripPostResponse(
    Guid Id,
    Guid TripId,
    string Title,
    string? Description,
    string OriginHubName,
    string DestinationHubName,
    DateTimeOffset? DepartureTime,
    DateTimeOffset? ScheduledDepartureAt,
    DateTimeOffset AcceptUntil,
    decimal RemainingWeightKg,
    decimal RemainingVolumeCbm,
    decimal MaxWeightKg,
    decimal MaxVolumeCbm,
    string TruckType,
    string LicensePlate,
    string DriverName,
    string PickupMode);
