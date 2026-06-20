namespace HMS.Modules.Transport.Application.Services;

public interface ITripRoutePlanner
{
    Task<string> ResolveRouteLineStringAsync(
        Guid originHubId,
        Guid destHubId,
        string? requestedRouteLineString,
        CancellationToken cancellationToken = default);
}
