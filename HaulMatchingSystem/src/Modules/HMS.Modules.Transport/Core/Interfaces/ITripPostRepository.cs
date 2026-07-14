using HMS.Modules.Transport.Application.DTOs.TripPosts;

namespace HMS.Modules.Transport.Core.Interfaces;

public interface ITripPostRepository
{
    Task<bool> HasOpenPostForTripAsync(Guid tripId, CancellationToken ct = default);
    Task<Guid> CreatePostAsync(TripPostRecord post, CancellationToken ct = default);
    Task<TripPostRecord?> GetPostByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdatePostAsync(TripPostRecord post, CancellationToken ct = default);
    Task<int> CountPostsAsync(string? status, Guid? hubId, DateTimeOffset? fromDate, DateTimeOffset? toDate, CancellationToken ct = default);
    Task<IReadOnlyList<TripPostListItemResponse>> ListPostsAsync(TripPostFilterRequest filter, Guid? resolvedHubId, CancellationToken ct = default);
    Task<IReadOnlyList<PublicTripPostResponse>> ListPublicPostsAsync(PublicTripPostFilterRequest filter, CancellationToken ct = default);
    Task<TripPostKpiResponse> GetKpiAsync(Guid? hubId, CancellationToken ct = default);
    Task<int> CountOpenPostsForHubAsync(Guid? hubId, CancellationToken ct = default);
    Task<int> ExpireOpenPostsAsync(CancellationToken ct = default);
}

public sealed class TripPostRecord
{
    public Guid Id { get; set; }
    public Guid TripId { get; set; }
    public Guid CreatedBy { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTimeOffset AcceptUntil { get; set; }
    public string Status { get; set; } = null!;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
