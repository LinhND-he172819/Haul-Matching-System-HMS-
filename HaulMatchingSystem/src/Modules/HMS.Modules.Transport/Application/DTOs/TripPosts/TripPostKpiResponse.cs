namespace HMS.Modules.Transport.Application.DTOs.TripPosts;

public sealed record TripPostKpiResponse(
    int TotalPosts,
    int OpenPosts,
    int ClosedPosts,
    int ExpiredPosts,
    int CancelledPosts,
    int EligibleTrips);
