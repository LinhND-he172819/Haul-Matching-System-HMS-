namespace HMS.Modules.Transport.Application.DTOs;

public sealed record HubResponse(
    Guid Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
