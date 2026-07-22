namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record PublicTripPostResponse(
    Guid Id,
    string Title,
    string? Description,
    string OriginHubName,
    string DestinationHubName,
    DateTimeOffset? DepartureTime,
    DateTimeOffset AcceptUntil,
    decimal RemainingWeightKg,
    decimal RemainingVolumeCbm,
    string TruckType,
    string LicensePlate,
    string DriverName,
    string PickupMode);
