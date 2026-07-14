namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record PublicTripPostFilterRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? OriginHubId { get; init; }
    public Guid? DestinationHubId { get; init; }
    public string? Keyword { get; init; }
    public DateTimeOffset? DepartureFrom { get; init; }
    public DateTimeOffset? DepartureTo { get; init; }
}
