namespace HMS.Modules.Transport.Application.DTOs;

public sealed record UpdateHubRequest(
    string Name,
    string Address,
    double Latitude,
    double Longitude);
