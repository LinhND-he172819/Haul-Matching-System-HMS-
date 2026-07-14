namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record UpdateTripPostRequest(
    string? Description,
    DateTimeOffset? AcceptUntil);
