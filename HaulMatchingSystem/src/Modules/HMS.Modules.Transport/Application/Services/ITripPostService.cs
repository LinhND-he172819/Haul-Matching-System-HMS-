using HMS.Modules.Transport.Application.DTOs.TripPosts;

namespace HMS.Modules.Transport.Application.Services;

public interface ITripPostService
{
    Task<IReadOnlyList<EligibleTripResponse>> GetEligibleTripsAsync(
        Guid currentUserId, string role, Guid? hubId, string? keyword, CancellationToken ct = default);

    Task<TripPostCreateResponse> CreateAsync(
        Guid currentUserId, string role, Guid? jwtHubId, CreateTripPostRequest request, CancellationToken ct = default);

    Task<PagedTripPostResponse<TripPostListItemResponse>> ListAsync(
        Guid currentUserId, string role, Guid? jwtHubId, TripPostFilterRequest filter, CancellationToken ct = default);

    Task<TripPostDetailResponse?> GetByIdAsync(
        Guid currentUserId, string role, Guid? jwtHubId, Guid id, CancellationToken ct = default);

    Task<TripPostDetailResponse?> UpdateAsync(
        Guid currentUserId, string role, Guid? jwtHubId, Guid id, UpdateTripPostRequest request, CancellationToken ct = default);

    Task<bool> CloseAsync(
        Guid currentUserId, string role, Guid? jwtHubId, Guid id, CancellationToken ct = default);

    Task<bool> CancelAsync(
        Guid currentUserId, string role, Guid? jwtHubId, Guid id, CancelTripPostRequest? request, CancellationToken ct = default);

    Task<PagedTripPostResponse<PublicTripPostResponse>> ListPublicAsync(
        PublicTripPostFilterRequest filter, CancellationToken ct = default);

    Task<TripPostKpiResponse> GetKpiAsync(
        Guid currentUserId, string role, Guid? jwtHubId, CancellationToken ct = default);
}
