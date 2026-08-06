namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record TripPostListItemResponse(
    Guid Id,
    Guid TripId,
    string Title,
    string? Description,
    string OriginHubName,
    string DestinationHubName,
    string DriverName,
    string LicensePlate,
    decimal RemainingWeightKg,
    decimal RemainingVolumeCbm,
    string Status,
    DateTimeOffset AcceptUntil,
    DateTimeOffset? PublishedAt,
    string CreatedByName);
