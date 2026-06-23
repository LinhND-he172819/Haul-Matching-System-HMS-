namespace HMS.Modules.Transport.Application.DTOs;

public sealed record VehicleResponse(
    Guid Id,
    string Code,
    string LicensePlate,
    Guid HubId,
    string VehicleType,
    decimal MaxWeightKg,
    decimal MaxVolumeCbm,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
