namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record TripPostFilterRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public string? Status { get; init; }
    public Guid? HubId { get; init; }
    public DateTimeOffset? FromDate { get; init; }
    public DateTimeOffset? ToDate { get; init; }
    public string SortBy { get; init; } = "CreatedAt";
    public string SortDirection { get; init; } = "desc";
}
