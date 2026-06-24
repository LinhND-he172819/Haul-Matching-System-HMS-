namespace HMS.Modules.Transport.Application.DTOs;

public sealed record CreateHubRequest(
    string Name,
    string Address,
    double Latitude,
    double Longitude);
