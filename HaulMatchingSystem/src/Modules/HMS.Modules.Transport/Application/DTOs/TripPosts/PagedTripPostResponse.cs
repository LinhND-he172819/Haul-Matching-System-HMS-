namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record PagedTripPostResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems,
    int TotalPages);
