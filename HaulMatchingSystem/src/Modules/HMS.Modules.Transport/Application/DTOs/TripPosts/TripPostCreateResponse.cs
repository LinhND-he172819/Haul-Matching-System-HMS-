namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record TripPostCreateResponse(
    Guid Id,
    Guid TripId,
    string Title,
    string Status,
    string Message);
