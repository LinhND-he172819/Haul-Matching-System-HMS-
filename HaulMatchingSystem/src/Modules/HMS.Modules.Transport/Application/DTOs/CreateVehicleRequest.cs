namespace HMS.Modules.Transport.Application.DTOs;

public sealed record CreateVehicleRequest(
    string Code,
    string LicensePlate,
    Guid HubId,
    string VehicleType,
    decimal MaxWeightKg,
    decimal MaxVolumeCbm,
    string Status);
