namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record CreateTripPostRequest(
    Guid TripId,
    string? Description,
    DateTimeOffset AcceptUntil);
